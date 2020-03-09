using System.Xml.Serialization;

namespace jNet.RPC
{
    public interface IRemoteHostConfig
    {
        [XmlAttribute]
        ushort ListenPort { get; }
    }
}
