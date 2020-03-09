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
        private readonly ConcurrentQueue<byte[]> _sendQueue = new ConcurrentQueue<byte[]>();
        
        protected readonly ConcurrentQueue<SocketMessage> _unresolvedQueue = new ConcurrentQueue<SocketMessage>();

        private readonly int _maxQueueSize;
        private Thread _readThread;
        private Thread _writeThread;

        private readonly AutoResetEvent _sendAutoResetEvent = new AutoResetEvent(false);
        protected readonly SemaphoreSlim _messageReceivedSempahore = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public TcpClient Client { get; private set; }
        public JsonSerializer Serializer { get; } = JsonSerializer.CreateDefault();
        protected IReferenceResolver ReferenceResolver { get; }

        protected SocketConnection(TcpClient client, IReferenceResolver referenceResolver)
        {
            Client = client;
            client.NoDelay = true;
            ReferenceResolver = referenceResolver;
            _maxQueueSize = 0x1000;
            Serializer.ReferenceResolver = referenceResolver;
            Serializer.TypeNameHandling = TypeNameHandling.Objects;
            Serializer.Context = new StreamingContext(StreamingContextStates.Remoting);
#if DEBUG
            Serializer.Formatting = Formatting.Indented;
#endif
        }

        protected SocketConnection(IReferenceResolver referenceResolver)
        {
            ReferenceResolver = referenceResolver;
            _maxQueueSize = 0x10000;                        
            Serializer = JsonSerializer.CreateDefault();
            Serializer.Context = new StreamingContext(StreamingContextStates.Remoting, this);
            Serializer.ReferenceResolver = referenceResolver;
            Serializer.TypeNameHandling = TypeNameHandling.Objects | TypeNameHandling.Arrays;
#if DEBUG
            Serializer.Formatting = Formatting.Indented;
#endif                                     
        }

        public async Task<bool> Connect(string address)
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

        protected void SetBinder(ISerializationBinder binder)
        {
            Serializer.SerializationBinder = binder;
        }

        internal void Send(SocketMessage message)
        {
            if (!IsConnected)
                return;
            try
            {
                if (_sendQueue.Count < _maxQueueSize)
                {
                    var serializedData = SerializeDto(message.Value);
                    _sendQueue.Enqueue(message.Encode(serializedData));
                    if (message.MessageType != SocketMessage.SocketMessageType.EventNotification)
                        Logger.Debug("Message queued to send {0}:{1}:{2}", message.MessageGuid, message.DtoGuid, message.MessageType);
                    
                    _sendAutoResetEvent.Set();
                    return;
                }
                Logger.Error("Message queue overflow");
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
            NotifyDisconnection();
        }

        public bool IsConnected { get; private set; } = true;
        
        protected virtual void OnDispose()
        {
            IsConnected = false;
            Client.Client?.Dispose();
            _sendAutoResetEvent.Set();
            _sendAutoResetEvent.Dispose();
            Logger.Info("Connection closed.");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != default(int))
                return;
            OnDispose();
            _cancellationTokenSource.Cancel();
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
        protected abstract void EnqueueMessage(SocketMessage message);
        protected abstract Task MessageHandlerProc();
        protected virtual void WriteThreadProc()
        {
            while (IsConnected)
            {
                try
                {
                    _sendAutoResetEvent.WaitOne();
                    while (!_sendQueue.IsEmpty)
                    {                        
                        if (!_sendQueue.TryDequeue(out var serializedMessage))
                            continue;
                                               
                        Client.Client.Send(serializedMessage);                        
                    }                                        
                }
                catch (Exception e) when (e is IOException || e is ArgumentNullException ||
                                          e is ObjectDisposedException || e is SocketException)
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

            while (IsConnected)
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
                            Logger.Debug("Message received {0}:{1}", message.MessageGuid, message.MessageType);
                        EnqueueMessage(message);
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

        private void NotifyDisconnection()
        {
            if (!IsConnected)
                return;
            IsConnected = false;
            Disconnected?.Invoke(this, EventArgs.Empty);
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
