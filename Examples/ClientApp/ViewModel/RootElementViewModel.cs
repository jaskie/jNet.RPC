﻿using ClientApp.Helpers;
using SharedInterfaces;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace ClientApp.ViewModel
{
    class RootElementViewModel: INotifyPropertyChanged, IDisposable
    {
        private ChildElementViewModel _selectedChildElement;

        public RootElementViewModel(IRootElement root)
        {
            Root = root;
            root.ChildAdded += Root_ChildAdded;
            root.ChildRemoved += Root_ChildRemoved;
            CommandAddChild = new UiCommand(AddChild);
            CommandRemoveChild = new UiCommand(RemoveChild, _ => SelectedChildElement != null);
            ChildElements = new ObservableCollection<ChildElementViewModel>(root.GetChildrens().Select(c => new ChildElementViewModel(c)));
        }

        public ObservableCollection<ChildElementViewModel> ChildElements { get; }

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
            var vm = ChildElements.FirstOrDefault(c => c.ChildElement == child);
            SelectedChildElement = vm;
        }

        private void RemoveChild(object obj)
        {
            Root.RemoveChild(SelectedChildElement.ChildElement);
        }

        private void Root_ChildRemoved(object sender, ChildEventArgs e)
        {
            var vm = ChildElements.FirstOrDefault(c => c.ChildElement == e.ChildElement);
            Debug.Assert(!(vm is null));
            ChildElements.Remove(vm);
        }

        private void Root_ChildAdded(object sender, ChildEventArgs e)
        {
            ChildElements.Add(new ChildElementViewModel(e.ChildElement));
        }

    }
}
