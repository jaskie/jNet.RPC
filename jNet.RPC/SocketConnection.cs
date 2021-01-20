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
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private int _disposed;
        private readonly BlockingCollection<byte[]> _sendQueue;
        private readonly BlockingCollection<SocketMessage> _receiveQueue = new BlockingCollection<SocketMessage>(new ConcurrentQueue<SocketMessage>());

        private Thread _readThread;
        private Thread _writeThread;

        public TcpClient Client { get; private set; }
        internal JsonSerializer Serializer { get; } 
       
        protected IReferenceResolver ReferenceResolver { get; }

        protected CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

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
            _sendQueue = new BlockingCollection<byte[]>(0x10000);
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
                await Client.ConnectAsync(addressParts[0], port).ConfigureAwait(false);
                Logger.Info("Connection opened to {0}:{1}.", addressParts[0], port);
                StartThreads();
                return true;
            }
            catch
            {
                Client.Close();
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
            return false;
        }

        internal void Send(SocketMessage message)
        {
            if (!IsConnected)
                return;
            try
            {
                var serializedData = message.MessageType == SocketMessage.SocketMessageType.EventNotification
                    ? SerializeEventArgs(message.Value)
                    : SerializeDto(message.Value);
                if (!_sendQueue.TryAdd(message.Encode(serializedData)))
                {
                    Logger.Error("Message queue overflow with message {0}", message);
                    NotifyDisconnection();
                    return;
                }
                if (message.MessageType != SocketMessage.SocketMessageType.EventNotification)
                    Logger.Trace("Message queued to send: {0}", message);
            }
            catch (Exception e)
            {
                Logger.Error(e);
                NotifyDisconnection();
            }
        }

        public bool IsConnected { get; private set; } = true;
        
        protected virtual void OnDispose()
        {
            if (!CancellationTokenSource.IsCancellationRequested)
                CancellationTokenSource.Cancel();
            Client.Client?.Dispose();
            _readThread?.Join();
            _writeThread?.Join();
            _sendQueue.Dispose();
            _receiveQueue.Dispose();
            CancellationTokenSource.Dispose();
            IsConnected = false;
            Logger.Info("Connection closed.");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != default)
                return;
            OnDispose();
        }

        protected void StartThreads()
        {
            _readThread = new Thread(ReadThreadProc)
            {
                IsBackground = true,
                Name = $"TCP read thread for {Client.Client.RemoteEndPoint}"
            };
            _readThread.Start();

            Task.Factory.StartNew(MessageHandlerProc, TaskCreationOptions.LongRunning);           

            _writeThread = new Thread(WriteThreadProc)
            {
                IsBackground = true,
                Name = $"TCP write thread for {Client.Client.RemoteEndPoint}"
            };
            _writeThread.Start();            
        }

        public event EventHandler Disconnected;
        protected abstract void MessageHandlerProc();
        protected virtual void WriteThreadProc()
        {
            while (!CancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var serializedMessage = _sendQueue.Take(CancellationTokenSource.Token);
                    Client.Client.Send(serializedMessage);
                }
                catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is SocketException || e is OperationCanceledException)
                {
                    NotifyDisconnection();
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

            while (!CancellationTokenSource.IsCancellationRequested)
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
                catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is SocketException)
                {
                    NotifyDisconnection();
                    return;
                }
                catch (Exception e)
                {
                    dataBuffer = null;
                    Logger.Error(e, "Read thread unexpected exception");
                }
            }
        }

        protected SocketMessage TakeNextMessage()
        {
            return _receiveQueue.Take(CancellationTokenSource.Token);
        }

        protected void NotifyDisconnection()
        {
            if (!IsConnected)
                return;
            IsConnected = false;
            Task.Run(() => Disconnected?.Invoke(this, EventArgs.Empty));  //move the notifier out of calling  thread
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
