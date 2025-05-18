using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace jNet.RPC
{

    /// <inheritdoc />
    /// <summary>
    /// Class to ensure non-blocking send and preserving order of messages
    /// </summary>
    public abstract class SocketConnection : IDisposable
    {
        private const int MessageQueueCapacity =
#if DEBUG 
            10000;
#else
            100000;
#endif
#if DEBUG
        private readonly Random _random = new Random();
#endif
        private const int MaxMessageSize = 0x4000000; // 64 MB
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private int _disposed;
        private readonly BlockingCollection<byte[]> _sendQueue;
        private readonly BlockingCollection<SocketMessage> _receiveQueue = new BlockingCollection<SocketMessage>();

        private readonly Thread _readThread;
        private readonly Thread _writeThread;
        private readonly Thread _messageHandlerThread;
        private readonly CancellationTokenSource _disconnectTokenSource = new CancellationTokenSource();
        private readonly TcpClient _client;

        protected readonly JsonSerializer Serializer;
        public string RemoteAddress { get; }

        /// <summary>
        /// Constructor for server-side connections
        /// </summary>
        /// <param name="client">incomming TCPClient from TcpListener</param>
        protected SocketConnection(TcpClient client)
        {
            RemoteAddress = client.Client.RemoteEndPoint.ToString();
            _client = client;
            client.NoDelay = true;
            _sendQueue = new BlockingCollection<byte[]>(0x100000);
            Serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                ContractResolver = new SerializationContractResolver(),
                ReferenceResolverProvider = GetReferenceResolver,
                TypeNameHandling = TypeNameHandling.Objects | TypeNameHandling.Arrays,
                Context = new StreamingContext(StreamingContextStates.Remoting),
#if DEBUG
                Formatting = Formatting.Indented
#endif
            });
            _readThread = CreateThread(ReadThreadProc, $"jNet.RPC read thread for {_client.Client.RemoteEndPoint}");
            _writeThread = CreateThread(WriteThreadProc, $"jNet.RPC write thread for {_client.Client.RemoteEndPoint}");
            _messageHandlerThread = CreateThread(MessageHandlerProc, $"jNet.RPC message handler thread for {_client.Client.RemoteEndPoint}");
        }

        /// <summary>
        /// Constructor for client-side connections
        /// </summary>
        /// <param name="address">address:port to connect to</param>
        protected SocketConnection(string address)
        {
            RemoteAddress = address;
            _sendQueue = new BlockingCollection<byte[]>(MessageQueueCapacity);
            Serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                ContractResolver = new SerializationContractResolver(),
                Context = new StreamingContext(StreamingContextStates.Remoting, this),
                ReferenceResolverProvider = GetReferenceResolver,
                TypeNameHandling = TypeNameHandling.Objects | TypeNameHandling.Arrays,
#if DEBUG
                Formatting = Formatting.Indented
#endif
            });

            var port = 1060;
            var addressParts = address.Split(':');
            if (addressParts.Length > 1)
                int.TryParse(addressParts[1], out port);
            try
            {
                Logger.Info("TCP connection to {0}:{1} starting", addressParts[0], port);
                _client = new TcpClient(addressParts[0], port) { NoDelay = true };
                Logger.Info("TCP connection to {0}:{1} established", addressParts[0], port);
            }
            catch (Exception e)
            {
                Logger.Info(e, "Unable to establish TCP connection to {0}:{1}", addressParts[0], port);
                Shutdown();
                return;
            }
            _readThread = CreateThread(ReadThreadProc, $"jNet.RPC read thread for {address}");
            _writeThread = CreateThread(WriteThreadProc, $"jNet.RPC write thread for {address}");
            _messageHandlerThread = CreateThread(MessageHandlerProc, $"jNet.RPC message handler thread for {address}");
        }

        protected abstract IReferenceResolver GetReferenceResolver();

        private protected virtual void Send(SocketMessage message)
        {
            if (_sendQueue.IsAddingCompleted)
                return;
            try
            {
                var serializedData = message.SerializeAndEncode(Serializer);
                if (!_sendQueue.TryAdd(serializedData))
                {
                    Logger.Error("Message queue overflow with message {0}", message);
                    Shutdown();
                    return;
                }
                if (message.MessageType != SocketMessageType.EventNotification)
                    Logger.Trace("Message queued to send: {0}", message);
            }
            catch (Exception e)
            {
                Logger.Error(e);
                Shutdown();
            }
        }

        protected void Shutdown()
        {
            if (_disconnectTokenSource.IsCancellationRequested)
                return;
            Logger.Info("Disconnected from {0}", RemoteAddress);
            try
            {
                _client?.Close();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error while shutting down connection to {0}", RemoteAddress);
            }
            _disconnectTokenSource.Cancel();
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnDispose()
        {
            Shutdown();
            _sendQueue.CompleteAdding();
            _receiveQueue.CompleteAdding();
            JoinThread(_readThread);
            JoinThread(_writeThread);
            JoinThread(_messageHandlerThread);
            _sendQueue.Dispose();
            _receiveQueue.Dispose();
            _disconnectTokenSource.Dispose();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != default)
                return;
            OnDispose();
        }

        public event EventHandler Disconnected;
        protected abstract void MessageHandlerProc();
        protected virtual void WriteThreadProc()
        {
            while (!IsCancelled)
            {
                try
                {
                    var serializedMessage = _sendQueue.Take(CancellationToken);
#if DEBUG
                    // Simulate network latency
                    Thread.Sleep(_random.Next(5));
#endif
                    _client.Client.Send(serializedMessage);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is SocketException)
                {
                    Shutdown();
                    return;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Write thread unexpected exception");
                }
            }
        }

        protected virtual void ReadThreadProc()
        {
            var stream = _client.GetStream();
            byte[] dataBuffer = new byte[1024];
            var sizeBuffer = new byte[sizeof(int)];
            int dataIndex = 0;
            int receivedBytesCount, messageSize = 0;

            while (!IsCancelled)
            {
                try
                {
                    if (messageSize == 0)
                    {
                        receivedBytesCount = stream.Read(sizeBuffer, dataIndex, sizeof(int) - dataIndex);
                        if (receivedBytesCount == 0)
                        {
                            Shutdown();
                            break;
                        }
                        dataIndex += receivedBytesCount;
                        if (dataIndex != sizeof(int))
                            continue;
                        messageSize = BitConverter.ToInt32(sizeBuffer, 0);
                        if (messageSize > MaxMessageSize)
                        {
                            throw new ApplicationException($"Too large message ({messageSize} bytes) received.");
                        }
                        if (dataBuffer.Length < messageSize)
                        {
                            Array.Resize(ref dataBuffer, messageSize);
                            Logger.Debug("Resized message buffer to {0} bytes", messageSize);
                        }
                        dataIndex = 0;
                    }
                    else
                    {
                        receivedBytesCount = stream.Read(dataBuffer, dataIndex, messageSize - dataIndex);
                        if (receivedBytesCount == 0)
                        {
                            Shutdown();
                            break;
                        }
                        dataIndex += receivedBytesCount;
                        if (dataIndex != messageSize)
                            continue;
                        var message = new SocketMessage(dataBuffer, messageSize);
                        if (message.MessageType != SocketMessageType.EventNotification)
                            Logger.Trace("Message received: {0}", message);
#if DEBUG
                        // Simulate network latency
                        Thread.Sleep(_random.Next(5));
#endif
                        _receiveQueue.Add(message);
                        messageSize = 0;
                        dataIndex = 0;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is SocketException)
                {
                    Shutdown();
                    break;
                }
                catch (ApplicationException e)
                {
                    Logger.Error(e);
                    Shutdown();
                    break;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Read thread unexpected exception. Data buffer was {0}. Original exception:", BitConverter.ToString(dataBuffer, 0, messageSize));
                    Shutdown();
                    break;
                }
            }
        }

        private protected SocketMessage TakeNextMessage()
        {
            return _receiveQueue.Take(CancellationToken);
        }

        protected bool IsCancelled => _disconnectTokenSource.IsCancellationRequested;
        
        protected CancellationToken CancellationToken => _disconnectTokenSource.Token;

        private Thread CreateThread(ThreadStart threadStart, string threadName)
        {
            return new Thread(threadStart)
            {
                IsBackground = true,
                Name = threadName,
                Priority = ThreadPriority.AboveNormal
            };
        }

        private void JoinThread(Thread thread)
        {
            if (thread != null && Thread.CurrentThread.ManagedThreadId != thread.ManagedThreadId)
                thread.Join();
        }

        protected void StartThreads()
        {
            _readThread?.Start();
            _writeThread?.Start();
            _messageHandlerThread?.Start();
        }
    }
}
