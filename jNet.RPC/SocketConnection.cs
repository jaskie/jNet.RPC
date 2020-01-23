using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly ConcurrentQueue<SocketMessage> _sendQueue = new ConcurrentQueue<SocketMessage>();
        protected readonly ConcurrentQueue<SocketMessage> _receiveQueue = new ConcurrentQueue<SocketMessage>();
        private readonly object _sendLock = new object();

        private readonly int _maxQueueSize;
        private Thread _readThread;
        private Thread _writeThread;

        private readonly AutoResetEvent _sendAutoResetEvent = new AutoResetEvent(false);
        protected readonly SemaphoreSlim _messageHandlerSempahore = new SemaphoreSlim(0);

        public TcpClient Client { get; }
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

        protected SocketConnection(string address, IReferenceResolver referenceResolver)
        {
            ReferenceResolver = referenceResolver;
            _maxQueueSize = 0x10000;
            var port = 1060;
            var addressParts = address.Split(':');
            if (addressParts.Length > 1)
                int.TryParse(addressParts[1], out port);
            Serializer = JsonSerializer.CreateDefault();
            Serializer.Context = new StreamingContext(StreamingContextStates.Remoting, this);
            Serializer.ReferenceResolver = referenceResolver;
            Serializer.TypeNameHandling = TypeNameHandling.Objects | TypeNameHandling.Arrays;
#if DEBUG
            Serializer.Formatting = Formatting.Indented;
#endif      
            
            Client = new TcpClient
            {
                NoDelay = true,
            };
            Client.Connect(addressParts[0], port);
            Logger.Info("Connection opened to {0}:{1}.", addressParts[0], port);
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
                lock(_sendLock) //to be sure that nothing is added between Count read and Enqueue (can Dequeue though)
                {
                    if (_sendQueue.Count < _maxQueueSize)
                    {
                        _sendQueue.Enqueue(message);
                        _sendAutoResetEvent.Set();
                        return;
                    }
                    Logger.Error("Message queue overflow");
                }                    
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
            Client.Client.Dispose();
            _sendAutoResetEvent.Set();
            _sendAutoResetEvent.Dispose();
            Logger.Info("Connection closed.");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != default(int))
                return;
            OnDispose();
        }

        public void StartThreads()
        {
            _readThread = new Thread(ReadThreadProc)
            {
                IsBackground = true,
                Name = $"TCP read thread for {Client.Client.RemoteEndPoint}"
            };
            _readThread.Start();

            _ = MessageHandlerProc();

            _writeThread = new Thread(WriteThreadProc)
            {
                IsBackground = true,
                Name = $"TCP write thread for {Client.Client.RemoteEndPoint}"
            };
            _writeThread.Start();            
        }

        public event EventHandler Disconnected;
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
                        if (!_sendQueue.TryDequeue(out var message))
                            continue;

                        var serializedData = SerializeDto(message.Value);
                       
                        Client.Client.Send(message.ToByteArray(serializedData));
                        //Logger.Debug($"Message {message.MessageGuid} sent. Type: {message.MessageType}");
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
                        if (stream.Read(sizeBuffer, 0, sizeof(int)) == sizeof(int))
                        {
                            var dataLength = BitConverter.ToUInt32(sizeBuffer, 0);
                            dataBuffer = new byte[dataLength];
                        }
                        dataIndex = 0;
                    }
                    else
                    {
                        var receivedLength = stream.Read(dataBuffer, dataIndex, dataBuffer.Length - dataIndex);
                        dataIndex += receivedLength;
                        if (dataIndex != dataBuffer.Length)
                            continue;

                        var message = new SocketMessage(dataBuffer);
                        //Logger.Debug($"Message {message.MessageGuid} received. Type: {message.MessageType}");

                        _receiveQueue.Enqueue(message);
                        dataBuffer = null;

                        if (_messageHandlerSempahore.CurrentCount == 0)
                            _messageHandlerSempahore.Release();

                        
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
                    Debug.WriteLine("Read thread unexpected excpetion");
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
            Debug.WriteLine("Disconnected");
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
