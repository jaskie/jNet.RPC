using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace jNet.RPC.UnitTests
{
    [TestClass]
    public class SocketMessageTests
    {
        private SocketMessage sut = new SocketMessage();

        private class DtoMock : IDto
        {
            public Guid DtoGuid { get; } = Guid.NewGuid();

            public event PropertyChangedEventHandler PropertyChanged;

            public void Dispose()
            {
            }
        }

        [TestMethod]
        public void SocketMessage_ToAndFromArray()
        {
            // arrange
            var dto = new DtoMock();
            var memberName = "A_Member_Name";
            var messageType = SocketMessage.SocketMessageType.EventNotification;
            var parameterCount = 123;
            var valueStream = new MemoryStream();
            valueStream.Write(dto.DtoGuid.ToByteArray(), 0, 16);
            var sourceMessage = new SocketMessage(messageType, dto, memberName, parameterCount, null);

            // act
            var encoded = sourceMessage.Encode(valueStream);
            var length = BitConverter.ToUInt32(encoded, 0);
            var encodedWithoutLength = new byte[length];
            Array.Copy(encoded, sizeof(uint), encodedWithoutLength, 0, encodedWithoutLength.Length);
            var receivedMessage = new SocketMessage(encodedWithoutLength);

            // assert
            Assert.AreEqual((uint)encoded.Length, length + sizeof(uint));
            Assert.AreEqual(messageType, receivedMessage.MessageType);
            Assert.AreEqual(memberName, receivedMessage.MemberName);
            Assert.AreEqual(dto.DtoGuid, receivedMessage.DtoGuid);
            Assert.AreEqual(parameterCount, receivedMessage.ParametersCount);
            Assert.IsTrue(valueStream.ToArray().SequenceEqual(receivedMessage.GetValueStream().ToArray()));
        }
    }
}
