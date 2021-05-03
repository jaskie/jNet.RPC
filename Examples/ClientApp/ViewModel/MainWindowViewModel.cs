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

        public MainWindowViewModel()
        {
            Connect();
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
            _rootElement?.Dispose();
            _remoteClient.Disconnected -= RemoteClient_Disconnected;
            _remoteClient.Dispose();
        }

        private async void Connect()
        {
            var address = RemoteAddress;
            ConnectionMessage = $"Connecting to {address}";
            IsConnecting = true;
            _remoteClient = new RemoteClient();
            _remoteClient.Disconnected += RemoteClient_Disconnected;
            await _remoteClient.ConnectAsync(address);
            var root = _remoteClient.GetRootObject<IRootElement>();
            RootElement = new RootElementViewModel(root);
            IsConnecting = false;
        }

        private void RemoteClient_Disconnected(object sender, EventArgs e)
        {
            _remoteClient.Disconnected -= RemoteClient_Disconnected;
            _remoteClient.Dispose();
            Connect();
        }
    }
}
