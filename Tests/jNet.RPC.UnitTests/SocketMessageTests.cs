using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;
using System.IO;

namespace jNet.RPC.UnitTests
{
    [TestClass]
    public class SocketMessageTests
    {

        private class DtoMock : IDto
        {
            public Guid DtoGuid { get; set; } = Guid.NewGuid();

#pragma warning disable CS0067 // The event 'SocketMessageTests.DtoMock.PropertyChanged' is never used
            public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067 // The event 'SocketMessageTests.DtoMock.PropertyChanged' is never used

            public Guid SomeValue { get; set; } = Guid.NewGuid();
        }

        [TestMethod]
        public void SocketMessage_ToAndFromArray()
        {
            // arrange
            var dto = new DtoMock();
            const string memberName = "A_Member_Name";
            const SocketMessageType messageType = SocketMessageType.PropertyGet;
            const int parameterCount = 123;
            var valueStream = new MemoryStream();
            valueStream.Write(dto.DtoGuid.ToByteArray(), 0, 16);
            var sourceMessage = new SocketMessage(messageType, dto.DtoGuid, memberName, parameterCount, dto);
            var serializer = Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All });

            // act
            var serializedBytes = sourceMessage.SerializeAndEncode(serializer);
            var messageContentLength = BitConverter.ToInt32(serializedBytes, 0);
            var encodedWithoutLength = new byte[messageContentLength];
            Buffer.BlockCopy(serializedBytes, sizeof(int), encodedWithoutLength, 0, encodedWithoutLength.Length);
            var receivedMessage = new SocketMessage(encodedWithoutLength, messageContentLength);

            DtoMock receivedDto = null;
            using (var receivedObject = receivedMessage.GetValueStream())
            using (var reader = new StreamReader(receivedObject, System.Text.Encoding.UTF8))
                receivedDto = (DtoMock)serializer.Deserialize(reader, typeof(DtoMock));

            // assert
            Assert.AreEqual(serializedBytes.Length, messageContentLength + sizeof(int));
            Assert.AreEqual(messageType, receivedMessage.MessageType);
            Assert.AreEqual(memberName, receivedMessage.MemberName);
            Assert.AreEqual(dto.DtoGuid, receivedMessage.DtoGuid);
            Assert.AreEqual(parameterCount, receivedMessage.ParametersCount);
            Assert.AreEqual(dto.DtoGuid, receivedDto.DtoGuid);
            Assert.AreEqual(dto.SomeValue, receivedDto.SomeValue);
        }
    }
}
