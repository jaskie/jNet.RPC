using System.ComponentModel;
using System.Diagnostics;

namespace jNet.RPC
{
    [DebuggerDisplay("{PropertyName} = {Value}")]
    internal class PropertyChangedWithValueEventArgs : PropertyChangedEventArgs
    {
        public PropertyChangedWithValueEventArgs(string propertyName, object value) : base(propertyName) 
        {
            Value = value;
        }

        public object Value { get; }
    }


}
