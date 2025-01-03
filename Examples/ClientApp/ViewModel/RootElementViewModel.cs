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
        private readonly List<ChildElementViewModel> _childElements;
        private readonly object _listLock = new object();
        private readonly IRootElement _root;
        public RootElementViewModel(IRootElement root)
        {
            _root = root;
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

        public ICommand CommandAddChild { get; }

        public ICommand CommandRemoveChild { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Dispose()
        {
            _root.ChildAdded -= Root_ChildAdded;
            _root.ChildRemoved -= Root_ChildRemoved;
        }

        private void AddChild(object _)
        {
            var child = _root.AddChild();
            SelectedChildElement = AddChildToCollection(child);
        }

        private void RemoveChild(object _)
        {
            _root.RemoveChild(SelectedChildElement.ChildElement);
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
            AddChildToCollection(e.ChildElement);
        }

        private ChildElementViewModel AddChildToCollection(IChildElement child)
        {
            /// we can't be sure which method (<see cref="Root_ChildAdded(object, ChildEventArgs)"/>  or <see cref="AddChild(object)"/> 
            /// will call this first, but we need to provide correct viewmodel to AddChild to select newly added item.
            /// This is because notifications arives from other thread.
            /// We also don't want to select items created by other client instances.
            ChildElementViewModel vm;
            lock (_listLock)
            {
                vm = _childElements.FirstOrDefault(c => c.ChildElement == child);
                if (vm != null) // already added
                    return vm;
                vm = new ChildElementViewModel(child);
                _childElements.Add(vm);
            }
            RefreshList();
            return vm;
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
