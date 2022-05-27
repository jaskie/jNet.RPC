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
            10000;
#endif
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private int _disposed;
        private readonly BlockingCollection<byte[]> _sendQueue;
        private readonly BlockingCollection<SocketMessage> _receiveQueue = new BlockingCollection<SocketMessage>();

        private Thread _readThread;
        private Thread _writeThread;
        private Thread _messageHandlerThread;
        private CancellationTokenRegistration _disconnectTokenRegistration;

        public TcpClient Client { get; private set; }
        internal JsonSerializer Serializer { get; } 
       
        protected IReferenceResolver ReferenceResolver { get; }

        protected CancellationTokenSource DisconnectTokenSource { get; private set; } 

        protected SocketConnection(TcpClient client, IReferenceResolver referenceResolver)
        {
            Client = client;
            client.NoDelay = true;
            ReferenceResolver = referenceResolver;
            _sendQueue = new BlockingCollection<byte[]>(0x1000);
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
        }

        protected SocketConnection(IReferenceResolver referenceResolver)
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
        }

        public async Task<bool> ConnectAsync(string address)
        {
            var port = 1060;
            var addressParts = address.Split(':');
            if (addressParts.Length > 1)
                int.TryParse(addressParts[1], out port);

            Client = new TcpClient
            {
                NoDelay = true,                
            };

            try
            {
                Logger.Info("Connecting to {0}:{1}", addressParts[0], port);
                await Client.ConnectAsync(addressParts[0], port).ConfigureAwait(false);
                Logger.Info("Connected to {0}:{1}", addressParts[0], port);
                StartThreads();
                return true;
            }
            catch
            {
                Dispose();
            }
            return false;
        }

        protected void Send(SocketMessage message)
        {
            if (_sendQueue.IsAddingCompleted)
                return;
            var disconnectTokenSource = DisconnectTokenSource;
            try
            {
                var serializedData = message.MessageType == SocketMessage.SocketMessageType.EventNotification
                    ? SerializeEventArgs(message.Value)
                    : SerializeDto(message.Value);
                if (!_sendQueue.TryAdd(message.Encode(serializedData)))
                {
                    Logger.Error("Message queue overflow with message {0}", message);
                    Shutdown(disconnectTokenSource);
                    return;
                }
                if (message.MessageType != SocketMessage.SocketMessageType.EventNotification)
                    Logger.Trace("Message queued to send: {0}", message);
            }
            catch (Exception e)
            {
                Logger.Error(e);
                Shutdown(disconnectTokenSource);
            }
        }

        protected void Shutdown(CancellationTokenSource disconnectTokenSource)
        {
            if (!disconnectTokenSource.IsCancellationRequested)
                disconnectTokenSource.Cancel();
        }

        protected virtual void OnDispose()
        {
            _disconnectTokenRegistration.Dispose();
            var tokenSource = DisconnectTokenSource;
            if (tokenSource?.IsCancellationRequested == false)
                tokenSource.Cancel();
            _sendQueue.CompleteAdding();
            _receiveQueue.CompleteAdding();
            _readThread?.Join();
            _writeThread?.Join();
            _messageHandlerThread?.Join();
            _sendQueue.Dispose();
            _receiveQueue.Dispose();
            Client.Client.Close();
            DisconnectTokenSource = null;
            tokenSource?.Dispose();
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
            DisconnectTokenSource = new CancellationTokenSource();
            _readThread = new Thread(ReadThreadProc)
            {
                IsBackground = true,
                Name = $"TCP read thread for {Client.Client.RemoteEndPoint}"
            };
            _readThread.Start();

            _writeThread = new Thread(WriteThreadProc)
            {
                IsBackground = true,
                Name = $"TCP write thread for {Client.Client.RemoteEndPoint}"
            };
            _writeThread.Start();

            _messageHandlerThread = new Thread(MessageHandlerProc)
            {
                IsBackground = true,
                Name = $"Message handler thread for {Client.Client.RemoteEndPoint}"
            };
            _messageHandlerThread.Start();
            _disconnectTokenRegistration = DisconnectTokenSource.Token.Register(Dispose);
        }

        public event EventHandler Disconnected;
        protected abstract void MessageHandlerProc();
        protected virtual void WriteThreadProc()
        {
            var disconnectTokenSource = DisconnectTokenSource;
            while (!(disconnectTokenSource.IsCancellationRequested || _sendQueue.IsCompleted))
            {
                try
                {
                    var serializedMessage = _sendQueue.Take(disconnectTokenSource.Token);
                    Client.Client.Send(serializedMessage);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is SocketException)
                {
                    Shutdown(disconnectTokenSource);
                    return;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Write thread unexpected exception");
                }
            }
        }

        protected virtual async void ReadThreadProc()
        {
            var stream = Client.GetStream();
            byte[] dataBuffer = null;
            var sizeBuffer = new byte[sizeof(int)];
            var dataIndex = 0;
            var disconnectTokenSource = DisconnectTokenSource;
            while (!(disconnectTokenSource.IsCancellationRequested || _receiveQueue.IsAddingCompleted))
            {
                try
                {
                    if (dataBuffer == null)
                    {
                        var bytes = await stream.ReadAsync(sizeBuffer, 0, sizeof(int), disconnectTokenSource.Token).ConfigureAwait(false);
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
                        var receivedLength = await stream.ReadAsync(dataBuffer, dataIndex, dataBuffer.Length - dataIndex, disconnectTokenSource.Token).ConfigureAwait(false);
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
                    Shutdown(disconnectTokenSource);
                    return;
                }
                catch (Exception e)
                {
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
