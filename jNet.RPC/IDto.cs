using System;
using System.ComponentModel;

namespace jNet.RPC
{
    public interface IDto: INotifyPropertyChanged
    {
        Guid DtoGuid { get; }
    }
}
