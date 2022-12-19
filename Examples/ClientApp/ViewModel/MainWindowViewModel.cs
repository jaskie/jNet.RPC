using jNet.RPC.Client;
using SharedInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientApp.ViewModel
{
    class MainWindowViewModel : IDisposable, INotifyPropertyChanged
    {
        const string RemoteAddress = "127.0.0.1:1356";

        private bool _isConnecting;
        private RemoteClient _remoteClient;
        private string _connectionMessage;
        private RootElementViewModel _rootElement;
        private bool _isDisposed;

        public MainWindowViewModel()
        {
            Task.Run(Connect);
        }

        public bool IsConnecting
        {
            get => _isConnecting;
            private set
            {
                if (_isConnecting == value)
                    return;
                _isConnecting = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnecting)));
            }
        }

        public string ConnectionMessage
        {
            get => _connectionMessage;
            private set
            {
                if (_connectionMessage == value)
                    return;
                _connectionMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConnectionMessage)));
            }
        }

        public RootElementViewModel RootElement
        {
            get => _rootElement; 
            private set
            {
                if (_rootElement == value)
                    return;
                _rootElement?.Dispose();
                _rootElement = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RootElement)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Dispose()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            _rootElement?.Dispose();
            if (_remoteClient is null)
                return;
            _remoteClient.Disconnected -= RemoteClient_Disconnected;
            _remoteClient.Dispose();
            _remoteClient = null;
        }

        private void Connect()
        {
            var address = RemoteAddress;
            ConnectionMessage = $"Connecting to {address}";
            IsConnecting = true;
            while (!_isDisposed && _remoteClient is null)
                try
                {
                    _remoteClient = new RemoteClient(address);
                }
                catch { }
            if (_remoteClient is null)
                return;
            _remoteClient.Disconnected += RemoteClient_Disconnected;
            var root = _remoteClient.GetRootObject<IRootElement>();
            RootElement = new RootElementViewModel(root);
            IsConnecting = false;
        }

        private void RemoteClient_Disconnected(object sender, EventArgs e)
        {
            _remoteClient.Disconnected -= RemoteClient_Disconnected;
            _remoteClient.Dispose();
            _remoteClient = null;
            Connect();
        }
    }
}
