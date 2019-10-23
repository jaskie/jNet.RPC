using System;
using System.ComponentModel;

namespace ComponentModelRPC
{
    public interface IDto: INotifyPropertyChanged, IDisposable
    {
        Guid DtoGuid { get; }
        event EventHandler Disposed;
    }
}
