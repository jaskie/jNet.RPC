using ClientApp.Helpers;
using SharedInterfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace ClientApp.ViewModel
{
    class RootElementViewModel: INotifyPropertyChanged, IDisposable
    {
        private ChildElementViewModel _selectedChildElement;
        private List<ChildElementViewModel> _childElements;
        private readonly object _listLock = new object();
        public RootElementViewModel(IRootElement root)
        {
            Root = root;
            root.ChildAdded += Root_ChildAdded;
            root.ChildRemoved += Root_ChildRemoved;
            CommandAddChild = new UiCommand(AddChild);
            CommandRemoveChild = new UiCommand(RemoveChild, _ => SelectedChildElement != null);
            _childElements = new List<ChildElementViewModel>(root.GetChildrens().Select(c => new ChildElementViewModel(c)));
            ChildElements = new CollectionViewSource { Source = _childElements }.View;
        }

        public ICollectionView ChildElements { get; }

        public ChildElementViewModel SelectedChildElement
        {
            get => _selectedChildElement; 
            set
            {
                if (_selectedChildElement == value)
                    return;
                _selectedChildElement = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedChildElement)));
            }
        }

        public IRootElement Root { get; }

        public ICommand CommandAddChild { get; }

        public ICommand CommandRemoveChild { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Dispose()
        {
            Root.ChildAdded -= Root_ChildAdded;
            Root.ChildRemoved -= Root_ChildRemoved;
        }

        private void AddChild(object _)
        {
            var child = Root.AddChild();
            ChildElementViewModel vm;
            lock (_listLock)
            {
                // we asssume that the child is not already in the list within event handler
                vm = _childElements.First(c => c.ChildElement == child);
            }
            SelectedChildElement = vm;
        }

        private void RemoveChild(object _)
        {
            Root.RemoveChild(SelectedChildElement.ChildElement);
        }

        private void Root_ChildRemoved(object sender, ChildEventArgs e)
        {
            lock (_listLock)
            {
                var vm = _childElements.FirstOrDefault(c => c.ChildElement == e.ChildElement);
                Debug.Assert(!(vm is null));
                _childElements.Remove(vm);
            }
            RefreshList();
        }

        private void Root_ChildAdded(object sender, ChildEventArgs e)
        {
            var newVm = new ChildElementViewModel(e.ChildElement);
            lock (_listLock)
                _childElements.Add(newVm);
            RefreshList();
        }

        private void RefreshList()
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                lock (_listLock)
                    ChildElements.Refresh();
            }));
        }

    }
}
