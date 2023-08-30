namespace ReflowOven.API.Integration.Peripheral.ResourcesRPi.Interfaces
{
    public enum MessageState
    {
        NONE,
        READY_FOR_SEND,
        SENT,
        ERROR_TIMEOUT_RX,
        LIMIT_TENTATIVES,
        RECEIVED_SUCCESSFULL,
        RECEIVED_CRC_ERROR
    }

    public class MessageInfo
    {
        public MessageState State { get; set; }
        public UInt16 Cmd { get; set; }
        public UInt16 SequenceNumber { get; set; }
        public UInt16? CountAttemptsSendTx { get; set; }
        public UInt16? CountAttemptsReceivedACK { get; set; }
        public Int32? Timeout { get; set; }
        public TypeMessage TypeMessage { get; set; }
        public required List<byte> Buffer { get; set; }
    }
    public enum TypeMessage
    {
        MESSAGE_SEND,
        MESSAGE_ACK,
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
        public const UInt16 MaxTentativeSendMessage = 5;
        public const UInt16 MaxTentativeReceivedACK = 5;
    }
}
