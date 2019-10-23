using System;

namespace ComponentModelRPC
{
    internal class WrappedEventArgs: EventArgs
    {
        public IDto Dto { get; }
        public EventArgs Args { get; }

        public WrappedEventArgs(IDto dto, EventArgs args)
        {
            Dto = dto;
            Args = args;
        }
    }
}
