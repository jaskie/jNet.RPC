using jNet.RPC.Client;
using SharedInterfaces;
using System;
using System.ComponentModel;
using System.Configuration;
using System.Threading.Tasks;
using System.Windows;

namespace ClientApp.ViewModel
{
    class MainWindowViewModel : IDisposable, INotifyPropertyChanged
    {
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
            CleanClient();
        }

        private async Task Connect()
        {
            var address = ConfigurationManager.AppSettings["RemoteEndpoint"];
            ConnectionMessage = $"Connecting to {address}";
            IsConnecting = true;
            while (!_isDisposed && _remoteClient?.ClientConnectionState != ClientConnectionState.Connecting)
            {
                if (CleanClient())
                    await Task.Delay(5000);
                _remoteClient = new RemoteClient(address);
            }
            _remoteClient.Disconnected += RemoteClient_Disconnected;
            var root = _remoteClient.GetRootObject<IRootElement>();
            await Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                RootElement = new RootElementViewModel(root);
                IsConnecting = false;
            }));
        }

        private bool CleanClient()
        {
            if (_remoteClient != null)
            {
                _remoteClient.Disconnected -= RemoteClient_Disconnected;
                _remoteClient.Dispose();
                _remoteClient = null;
                return true;
            }
            return false;
        }

        private void RemoteClient_Disconnected(object sender, EventArgs e)
        {
            CleanClient();
            Task.Run(Connect);
        }
    }
}
