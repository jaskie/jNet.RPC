using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            1000;
#else
            100000;
#endif
        private const uint MaxMessageSize = 0x8000000; // 128 MB
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private int _disposed;
        private readonly BlockingCollection<byte[]> _sendQueue;
        private readonly BlockingCollection<SocketMessage> _receiveQueue = new BlockingCollection<SocketMessage>();

        private readonly Thread _readThread;
        private readonly Thread _writeThread;
        private readonly Thread _messageHandlerThread;
        protected readonly CancellationTokenSource DisconnectTokenSource = new CancellationTokenSource();
        private CancellationTokenRegistration _disconnectTokenRegistration;

        public TcpClient Client { get; private set; }
        protected JsonSerializer _serializer;

        protected SocketConnection(TcpClient client)
        {
            Client = client;
            client.NoDelay = true;
            _sendQueue = new BlockingCollection<byte[]>(0x100000);
            _serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                ContractResolver = new SerializationContractResolver(),
                ReferenceResolverProvider = GetReferenceResolver,
                TypeNameHandling = TypeNameHandling.Objects,
                Context = new StreamingContext(StreamingContextStates.Remoting),
#if DEBUG
                Formatting = Formatting.Indented
#endif
            });
            _readThread = CreateThread(ReadThreadProc, $"jNet.RPC read thread for {Client.Client.RemoteEndPoint}");
            _writeThread = CreateThread(WriteThreadProc, $"jNet.RPC write thread for {Client.Client.RemoteEndPoint}");
            _messageHandlerThread = CreateThread(MessageHandlerProc, $"jNet.RPC message handler thread for {Client.Client.RemoteEndPoint}");
        }

        protected SocketConnection(string address)
        {
            _sendQueue = new BlockingCollection<byte[]>(MessageQueueCapacity);
            _serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
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
                Logger.Info("Connecting to {0}:{1}", addressParts[0], port);
                Client = new TcpClient(addressParts[0], port) { NoDelay = true };
            }
            catch (Exception e)
            {
                _sendQueue.Dispose();
                _receiveQueue.Dispose();
                DisconnectTokenSource.Dispose();
                Logger.Info(e, "Unable to connect to  {0}:{1}", addressParts[0], port);
                throw e;
            }
            Logger.Info("Connected to {0}:{1}", addressParts[0], port);
            _readThread = CreateThread(ReadThreadProc, $"jNet.RPC read thread for {address}");
            _writeThread = CreateThread(WriteThreadProc, $"jNet.RPC write thread for {address}");
            _messageHandlerThread = CreateThread(MessageHandlerProc, $"jNet.RPC message handler thread for {address}");
        }

        protected abstract IReferenceResolver GetReferenceResolver();

        protected void Send(SocketMessage message)
        {
            if (_sendQueue.IsAddingCompleted)
                return;
            try
            {
                var serializedData = SerializeEncodeMessage(message);
                if (!_sendQueue.TryAdd(serializedData))
                {
                    Logger.Error("Message queue overflow with message {0}", message);
                    Shutdown();
                    return;
                }
                if (message.MessageType != SocketMessage.SocketMessageType.EventNotification)
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
            if (DisconnectTokenSource.IsCancellationRequested)
                return;
            Logger.Info("Disconnected from {0}", Client.Client.RemoteEndPoint);
            Client.Client.Close();
            DisconnectTokenSource.Cancel();
        }

        protected virtual void OnDispose()
        {
            _disconnectTokenRegistration.Dispose();
            Shutdown();
            _sendQueue.CompleteAdding();
            _receiveQueue.CompleteAdding();
            if (Thread.CurrentThread.ManagedThreadId != _readThread.ManagedThreadId)
                _readThread.Join();
            if (Thread.CurrentThread.ManagedThreadId != _writeThread.ManagedThreadId)
                _writeThread.Join();
            if (Thread.CurrentThread.ManagedThreadId != _messageHandlerThread.ManagedThreadId)
                _messageHandlerThread.Join();
            _sendQueue.Dispose();
            _receiveQueue.Dispose();
            DisconnectTokenSource.Dispose();
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != default)
                return;
            OnDispose();
        }

        protected void StartThreads()
        {
            _readThread.Start();
            _writeThread.Start();
            _messageHandlerThread.Start();
            _disconnectTokenRegistration = DisconnectTokenSource.Token.Register(Dispose);
        }

        public event EventHandler Disconnected;
        protected abstract void MessageHandlerProc();
        protected virtual void WriteThreadProc()
        {
            while (!DisconnectTokenSource.IsCancellationRequested)
            {
                try
                {
                    var serializedMessage = _sendQueue.Take(DisconnectTokenSource.Token);
                    Client.Client.Send(serializedMessage);
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
            var stream = Client.GetStream();
            byte[] dataBuffer = null;
            var sizeBuffer = new byte[sizeof(uint)];
            int dataIndex = 0;
            int receivedBytesCount;
            while (!DisconnectTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (dataBuffer is null)
                    {
                        receivedBytesCount = stream.Read(sizeBuffer, dataIndex, sizeof(uint) - dataIndex);
                        if (receivedBytesCount == 0)
                        {
                            Shutdown();
                            break;
                        }
                        dataIndex += receivedBytesCount;
                        if (dataIndex != sizeof(uint))
                            continue;
                        var dataLength = BitConverter.ToUInt32(sizeBuffer, 0);
                        if (dataLength > MaxMessageSize)
                        {
                            throw new ApplicationException($"Too large message ({dataLength} bytes) received.");
                        }
                        dataBuffer = new byte[dataLength];
                        dataIndex = 0;
                    }
                    else
                    {
                        receivedBytesCount = stream.Read(dataBuffer, dataIndex, dataBuffer.Length - dataIndex);
                        if (receivedBytesCount == 0)
                        {
                            Shutdown();
                            break;
                        }
                        dataIndex += receivedBytesCount;
                        if (dataIndex != dataBuffer.Length)
                            continue;
                        var message = new SocketMessage(dataBuffer);
                        if (message.MessageType != SocketMessage.SocketMessageType.EventNotification)
                            Logger.Trace("Message received: {0}", message);
                        _receiveQueue.Add(message);
                        dataBuffer = null;
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
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Read thread unexpected exception. Data buffer was {0}. Original exception:", dataBuffer is null ? "null" : BitConverter.ToString(dataBuffer));
                    Shutdown();
                    break;
                }
            }
        }

        protected SocketMessage TakeNextMessage()
        {
            return _receiveQueue.Take(DisconnectTokenSource.Token);
        }

        private Thread CreateThread(ThreadStart threadStart, string threadName)
        {
            return  new Thread(threadStart)
            {
                IsBackground = true,
                Name = threadName,
                Priority = ThreadPriority.AboveNormal
            };
        }

        private byte[] SerializeEncodeMessage(SocketMessage message)
        {
            using (var serialized = new MemoryStream())
            {
                using (var writer = new StreamWriter(serialized, Encoding.UTF8, 1024, true))
                {
                    _serializer.Serialize(writer, message.Value);
                }
                return message.Encode(serialized);
            }
        }

        protected T DeserializeValue<T>(SocketMessage message)
        {
            using (var stream = message.GetValueStream())
            {
                if (stream is null)
                    return default(T);
                using (var reader = new StreamReader(stream, false))
                    return (T)_serializer.Deserialize(reader, typeof(T));
            }
        }
    }
}
