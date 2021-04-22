using System;
using System.ComponentModel;

namespace jNet.RPC
{
    public interface IDto: INotifyPropertyChanged, IDisposable
    {
        Guid DtoGuid { get; }
    }
}
