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
            100;
#else
            100000;
#endif
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private int _disposed;
        private readonly BlockingCollection<byte[]> _sendQueue;
        private readonly BlockingCollection<SocketMessage> _receiveQueue = new BlockingCollection<SocketMessage>();

        private readonly Thread _readThread;
        private readonly Thread _writeThread;
        private readonly Thread _messageHandlerThread;
        private CancellationTokenRegistration _disconnectTokenRegistration;

        public TcpClient Client { get; private set; }
        internal JsonSerializer Serializer { get; }
       
        protected IReferenceResolver ReferenceResolver { get; }

        protected CancellationTokenSource DisconnectTokenSource { get; } = new CancellationTokenSource();

        protected SocketConnection(TcpClient client, IReferenceResolver referenceResolver)
        {
            Client = client;
            client.NoDelay = true;
            ReferenceResolver = referenceResolver;
            _sendQueue = new BlockingCollection<byte[]>(0x100000);
            Serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                ContractResolver = new SerializationContractResolver(),
                ReferenceResolverProvider = () => referenceResolver,
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

        protected SocketConnection(string address, IReferenceResolver referenceResolver)
        {
            ReferenceResolver = referenceResolver;
            _sendQueue = new BlockingCollection<byte[]>(MessageQueueCapacity);
            Serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
            {
                ContractResolver = new SerializationContractResolver(),
                Context = new StreamingContext(StreamingContextStates.Remoting, this),
                ReferenceResolverProvider = () => referenceResolver,
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

        protected void Send(SocketMessage message)
        {
            if (_sendQueue.IsAddingCompleted)
                return;
            try
            {
                var serializedData = message.MessageType == SocketMessage.SocketMessageType.EventNotification
                    ? SerializeEventArgs(message.Value)
                    : SerializeDto(message.Value);
                if (!_sendQueue.TryAdd(message.Encode(serializedData)))
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
            var sizeBuffer = new byte[sizeof(int)];
            var dataIndex = 0;
            while (!DisconnectTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (dataBuffer == null)
                    {
                        var bytes = stream.Read(sizeBuffer, 0, sizeof(int));
                        if (bytes == sizeof(int))
                        {
                            var dataLength = BitConverter.ToUInt32(sizeBuffer, 0);
                            dataBuffer = new byte[dataLength];
                        }
                        else { }
                        dataIndex = 0;
                    }
                    else
                    {
                        var receivedLength = stream.Read(dataBuffer, dataIndex, dataBuffer.Length - dataIndex);
                        dataIndex += receivedLength;
                        if (dataIndex != dataBuffer.Length)
                            continue;
                        var message = new SocketMessage(dataBuffer);
                        if (message.MessageType != SocketMessage.SocketMessageType.EventNotification)
                            Logger.Trace("Message received: {0}", message);
                        _receiveQueue.Add(message);
                        dataBuffer = null;
                    }
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
                    Shutdown();
                    Logger.Error(e, "Read thread unexpected exception");
                }
            }
        }

        protected SocketMessage TakeNextMessage(CancellationToken token)
        {
            return _receiveQueue.Take(token);
        }

        private Stream SerializeEventArgs(object eventArgs)
        {
            var serialized = new MemoryStream();
            using (var writer = new StreamWriter(serialized, Encoding.UTF8, 1024, true))
                Serializer.Serialize(writer, eventArgs);
            return serialized;

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

        protected Stream SerializeDto(object dto)
        {
            if (dto == null)
                return null;
            var serialized = new MemoryStream();
            using (var writer = new StreamWriter(serialized, Encoding.UTF8, 1024, true))
                Serializer.Serialize(writer, dto);

            return serialized;
        }

        protected T DeserializeDto<T>(Stream stream)
        {
            if (stream == null)
                return default(T);
            using (var reader = new StreamReader(stream))
            {
                return (T)Serializer.Deserialize(reader, typeof(T));
            }
        }
    }
}
