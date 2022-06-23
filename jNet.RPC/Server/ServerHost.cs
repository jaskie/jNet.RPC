using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace jNet.RPC.Server
{
    public class ServerHost : IDisposable, IRemoteHostConfig
    {
        private int _disposed;
        private Thread _listenerThread;
        private IDto _rootServerObject;
        private IPrincipalProvider _principalProvider;
        private readonly CancellationTokenSource _shutdownTokenSource;
        private readonly List<ServerSession> _clients = new List<ServerSession>();
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
                
        public ushort ListenPort { get; private set; }

        public ServerHost(ushort listenPort, ServerObjectBase rootObject, IPrincipalProvider principalProvider = null)
        {
            ListenPort = listenPort;
            _rootServerObject = rootObject;
            _principalProvider = principalProvider ?? PrincipalProvider.Default;

            Logger.Trace("Starting TCP listener on port {0}", ListenPort);
            try
            {
                _shutdownTokenSource = new CancellationTokenSource();
                _listenerThread = new Thread(ListenerThreadProc)
                {
                    Name = $"Remote client session listener on port {ListenPort}",
                    IsBackground = true
                };
                _listenerThread.Start();
            }
            catch(Exception e)
            {
                Logger.Error(e, "Initialization of {0} error.", this);
                _shutdownTokenSource.Cancel();
                throw;
            }
        }

        private async void ListenerThreadProc()
        {
            try
            {
                var listener = new TcpListener(IPAddress.Any, ListenPort) { ExclusiveAddressUse = true };
                listener.Start();
                try
                {
                    while (!_shutdownTokenSource.IsCancellationRequested)
                    {
                        TcpClient client = null;
                        try
                        {
                            client = await Task.Run(() => listener.AcceptTcpClientAsync(), _shutdownTokenSource.Token);
                            var sessionUser = _principalProvider.GetPrincipal(client);
                            if (sessionUser == null)
                            {
                                Logger.Warn($"Remote client {client.Client.RemoteEndPoint} not allowed");
                                client.Close();
                            }
                            else
                                AddClient(client, sessionUser);
                        }
                        catch (Exception e) when (e is SocketException || e is ThreadAbortException || e is ThreadInterruptedException)
                        {
                            Logger.Trace("{0} shutdown.", this);
                            break;
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "{0} unexpected listener thread exception", this);
                        }
                    }
                }
                finally
                {
                    listener.Stop();
                    List<ServerSession> serverSessionsCopy;
                    lock (((IList) _clients).SyncRoot)
                        serverSessionsCopy = _clients.ToList();
                    serverSessionsCopy.ForEach(s => s.Dispose());
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "{0} general error", this);
            }
        }

        private void AddClient(TcpClient client, IPrincipal user)
        {
            var clientSession = new ServerSession(client, _rootServerObject, user);
            clientSession.Disconnected += ClientSessionDisconnected;
            lock (((IList)_clients).SyncRoot)
                _clients.Add(clientSession);
        }

        private void ClientSessionDisconnected(object sender, EventArgs e)
        {
            var serverSession = sender as ServerSession ?? throw new ArgumentException(nameof(sender));
            lock (((IList) _clients).SyncRoot)
                _clients.Remove(serverSession);
            serverSession.Disconnected -= ClientSessionDisconnected;
            serverSession.Dispose();
        }

        public int ClientCount
        {
            get
            {
                lock (((IList) _clients).SyncRoot)
                    return _clients.Count;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == default)
                UnInitialize();
        }

        private void UnInitialize()
        {
            _shutdownTokenSource.Cancel();
        }

        public override string ToString()
        {
            return $"ServerHost on {ListenPort}";
        }
    }
}
