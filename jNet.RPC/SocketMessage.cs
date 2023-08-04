using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace jNet.RPC
{
    public class SocketMessage
    {
        public enum SocketMessageType: byte
        {
            RootQuery,
            MethodExecute,
            PropertyGet,
            PropertySet,
            EventAdd,
            EventRemove,
            EventNotification,
            ProxyFinalized,
            ProxyResurrected,
            ProxyMissing,
            Exception,
        }

        private readonly byte[] _rawData;
        private readonly int _valueStartIndex;
        private static readonly byte[] ContentLengthPlaceholder = new byte[sizeof(uint)] { 0, 0, 0, 0 };

        internal SocketMessage(object value = null)
        {
            MessageGuid = Guid.NewGuid();
            Value = value;
        }

        internal SocketMessage(SocketMessage originalMessage, object value)
        {
            MessageGuid = originalMessage.MessageGuid;
            MessageType = originalMessage.MessageType;
            DtoGuid = originalMessage.DtoGuid;
            MemberName = originalMessage.MemberName;
            Value = value;
        }

        internal SocketMessage(byte[] rawData)
        {
            var index = 0;
            MessageType = (SocketMessageType) rawData[index];
            index += 1;
            byte[] guidBuffer = new byte[16];
            Buffer.BlockCopy(rawData, index, guidBuffer, 0, guidBuffer.Length);
            index += guidBuffer.Length;
            MessageGuid = new Guid(guidBuffer);
            Buffer.BlockCopy(rawData, index, guidBuffer, 0, guidBuffer.Length);
            index += guidBuffer.Length;
            DtoGuid = new Guid(guidBuffer);
            var memberNameLength = BitConverter.ToInt32(rawData, index);
            index += sizeof(int);
            MemberName = Encoding.ASCII.GetString(rawData, index, memberNameLength);
            index += memberNameLength;
            ParametersCount = BitConverter.ToInt32(rawData, index);
            _valueStartIndex = index + sizeof(int);
            _rawData = rawData;
            Debug.Assert(_valueStartIndex == 41 + memberNameLength, "There are 41 bytes and MemberName string before value starts");
        }

        public object Value { get; }

        public readonly Guid MessageGuid;
        public Guid DtoGuid;
        public SocketMessageType MessageType;
        /// <summary>
        /// Object member (method, property or event) name
        /// </summary>
        public string MemberName;

        /// <summary>
        /// count of parameters passed from client to server, 
        /// </summary>
        public int ParametersCount;

        public override string ToString()
        {
            return $"{MessageGuid}:{MessageType}:{MemberName} for {DtoGuid}";
        }
        
        public static SocketMessage Create(SocketMessageType SocketMessageType, IDto dto, string memberName, int paramsCount, object value)
        {
            return new SocketMessage(value)
            {
                MessageType = SocketMessageType,
                DtoGuid = dto?.DtoGuid ?? Guid.Empty,
                MemberName = memberName,
                ParametersCount = paramsCount
            };
        }

        public byte[] Encode(Stream value)
        {
            using (var stream = new MemoryStream())
            {
                stream.Write(ContentLengthPlaceholder, 0, sizeof(uint));

                stream.WriteByte((byte)MessageType);

                stream.Write(MessageGuid.ToByteArray(), 0, 16);

                stream.Write(DtoGuid.ToByteArray(), 0, 16);

                // MemberName
                if (string.IsNullOrEmpty(MemberName))
                    stream.Write(BitConverter.GetBytes(0), 0, sizeof(int));
                else
                {
                    byte[] memberNameBytes = Encoding.ASCII.GetBytes(MemberName);
                    stream.Write(BitConverter.GetBytes(memberNameBytes.Length), 0, sizeof(int));
                    stream.Write(memberNameBytes, 0, memberNameBytes.Length);
                }
                stream.Write(BitConverter.GetBytes(ParametersCount), 0, sizeof(int));
                if (value != null)
                {
                    value.Position = 0;
                    value.CopyTo(stream);
                }
                var contentLength = BitConverter.GetBytes((uint)stream.Length - sizeof(uint));
                stream.Position = 0;
                stream.Write(contentLength, 0, ContentLengthPlaceholder.Length);
                return stream.ToArray();
            }
        }

        public Stream GetValueStream() => _rawData.Length > _valueStartIndex ? new MemoryStream(_rawData, _valueStartIndex, _rawData.Length - _valueStartIndex) : null;

#if DEBUG
        public string ValueString => Encoding.UTF8.GetString(_rawData, _valueStartIndex, _rawData.Length - _valueStartIndex);
#endif
    }

    public class SocketMessageArrayValue 
    {
        [DtoMember]
        public object[] Value;
    }



}
