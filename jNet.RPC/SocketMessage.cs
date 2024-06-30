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
            Exception
        }
        private const int GUID_LENGTH = 0x10;
        private readonly byte[] _valueData;
        private static readonly byte[] ContentLengthPlaceholder = new byte[sizeof(int)] { 0, 0, 0, 0 };

        // used to prepare response on server side
        internal SocketMessage(Guid messageGuid, SocketMessageType messageType, Guid dtoGuid, string memberName, object value)
        {
            MessageGuid = messageGuid;
            MessageType = messageType;
            DtoGuid = dtoGuid;
            MemberName = memberName;
            Value = value;
        }

        internal SocketMessage (SocketMessageType messageType, Guid dtoGuid, string memberName, int paramsCount, object value)
        {
            MessageGuid = Guid.NewGuid();
            Value = value;
            MessageType = messageType;
            DtoGuid = dtoGuid;
            MemberName = memberName;
            ParametersCount = paramsCount;
        }

        // used on receive thread
        internal SocketMessage(byte[] rawData, int dataLength)
        {
            Debug.Assert(rawData.Length >= dataLength);
            var index = 0;
            MessageType = (SocketMessageType) rawData[index];
            byte[] guidBuffer = new byte[GUID_LENGTH];
            Buffer.BlockCopy(rawData, index += 1, guidBuffer, 0, GUID_LENGTH);
            MessageGuid = new Guid(guidBuffer);
            Buffer.BlockCopy(rawData, index += GUID_LENGTH, guidBuffer, 0, GUID_LENGTH);
            DtoGuid = new Guid(guidBuffer);
            var memberNameLength = BitConverter.ToInt32(rawData, index += GUID_LENGTH);
            MemberName = Encoding.ASCII.GetString(rawData, index += sizeof(int), memberNameLength);
            ParametersCount = BitConverter.ToInt32(rawData, index += memberNameLength);
            var valueStartIndex = index + sizeof(int);
            // rest of the data is value
            if (dataLength > valueStartIndex)
            {
                _valueData = new byte[dataLength - valueStartIndex];
                Buffer.BlockCopy(rawData, valueStartIndex, _valueData, 0, _valueData.Length);
            }
            Debug.Assert(valueStartIndex == 41 + memberNameLength, "There are 41 bytes and MemberName string before value starts");
        }

        /// <summary>
        /// Value to serialize
        /// </summary>
        public readonly object Value;

        /// <summary>
        /// Guid of message. Client request and response are expected to have the same MessageGuid
        /// </summary>
        public readonly Guid MessageGuid;

        /// <summary>
        /// Guid of object on which call is invoked
        /// </summary>
        public readonly Guid DtoGuid;

        /// <summary>
        /// Kind of operation to execute
        /// </summary>
        public readonly SocketMessageType MessageType;

        /// <summary>
        /// Object member (method, property or event) name
        /// </summary>
        public readonly string MemberName;

        /// <summary>
        /// count of parameters passed from client to server
        /// only used when calling a method
        /// </summary>
        public readonly int ParametersCount;

        public override string ToString()
        {
            return $"{MessageGuid}:{MessageType}:{MemberName} for {DtoGuid}";
        }

        internal byte[] Encode(Stream valueStream)
        {
            using (var stream = new MemoryStream())
            {
                stream.Write(ContentLengthPlaceholder, 0, sizeof(int));

                stream.WriteByte((byte)MessageType);

                stream.Write(MessageGuid.ToByteArray(), 0, GUID_LENGTH);

                stream.Write(DtoGuid.ToByteArray(), 0, GUID_LENGTH);

                // MemberName
                if (string.IsNullOrEmpty(MemberName))
                    stream.Write(BitConverter.GetBytes(0), 0, sizeof(int));
                else
                {
                    var memberNameBytes = Encoding.ASCII.GetBytes(MemberName);
                    stream.Write(BitConverter.GetBytes(memberNameBytes.Length), 0, sizeof(int));
                    stream.Write(memberNameBytes, 0, memberNameBytes.Length);
                }
                stream.Write(BitConverter.GetBytes(ParametersCount), 0, sizeof(int));
                if (valueStream != null)
                {
                    valueStream.Position = 0;
                    valueStream.CopyTo(stream);
                }
                var contentLength = BitConverter.GetBytes((int)(stream.Length - sizeof(int)));
                stream.Position = 0;
                stream.Write(contentLength, 0, ContentLengthPlaceholder.Length);
                return stream.ToArray();
            }
        }

        public MemoryStream GetValueStream() => _valueData is null ? null : new MemoryStream(_valueData);

#if DEBUG
        public string ValueString => _valueData is null ? "null"  : Encoding.UTF8.GetString(_valueData);
#endif
    }

    public class SocketMessageArrayValue 
    {
        [DtoMember]
        public object[] Value;
    }



}
