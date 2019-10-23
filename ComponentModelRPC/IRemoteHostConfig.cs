using System.Xml.Serialization;

namespace ComponentModelRPC
{
    public interface IRemoteHostConfig
    {
        [XmlAttribute]
        ushort ListenPort { get; set; }
    }
}
