using System;

namespace jNet.RPC.Server
{
    internal readonly struct DelegateKey
    {
        public DelegateKey(Guid dtoGuid, string eventName)
        {
            DtoGuid = dtoGuid;
            EventName = eventName;
        }
        public Guid DtoGuid { get; }

        public string EventName { get; }

        public override string ToString()
        {
            return $"{DtoGuid}:{EventName}";
        }
    }
}
