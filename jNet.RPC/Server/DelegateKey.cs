//#undef DEBUG

using System;

namespace jNet.RPC.Server
{
    internal class DelegateKey
    {
        public DelegateKey(Guid dtoGuid, string eventName)
        {
            DtoGuid = dtoGuid;
            EventName = eventName;
        }
        public Guid DtoGuid { get; }

        public string EventName { get; }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == typeof(DelegateKey) && Equals((DelegateKey)obj);
        }

        private bool Equals(DelegateKey other)
        {
            return DtoGuid.Equals(other.DtoGuid) && string.Equals(EventName, other.EventName);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (DtoGuid.GetHashCode() * 397) ^ (EventName != null ? EventName.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return $"{DtoGuid}:{EventName}";
        }
    }
}
