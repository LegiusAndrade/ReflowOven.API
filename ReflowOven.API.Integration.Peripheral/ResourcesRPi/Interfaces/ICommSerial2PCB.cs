namespace ReflowOven.API.Integration.Peripheral.ResourcesRPi.Interfaces
{
    public enum MessageState
    {
        NONE,
        READY_FOR_SEND,
        SENT,
        ERROR_TIMEOUT_RX,
        LIMIT_TENTATIVES
    }

    public class MessageInfo
    {
        public MessageState State { get; set; }
        public UInt16 Cmd { get; set; }
        public UInt16 SequenceNumber { get; set; }
        public UInt16 NumTries { get; set; }
        public UInt16 Timeout { get; set; }
        public required List<byte> Buffer { get; set; }
    }

    public class MessageManager
    {
        public List<MessageInfo> messageBuffer { get; set; } = new List<MessageInfo>();
        public UInt16 ProtocolVersion { get; set; }
        public string? TypeCRC { get; set; }
    }

    public static class MessageConstants
    {
        public const UInt16 MaxBuf = 256;
        public const UInt16 TimeoutAck = 5000;
        public const UInt16 MaxTentatives = 5;
    }
}
