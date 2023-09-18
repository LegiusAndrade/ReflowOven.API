namespace ReflowOven.API.Integration.Peripheral.ResourcesRPi.Interfaces;

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


public class PacketMessage
{
    public UInt16 Header { get; set; }
    public UInt16 VersionProtocol { get; set; }
    public TypeMessage TypeMessage { get; set; }
    public UInt16 SequenceNumber { get; set; }
    public Byte Cmd { get; set; }
    public UInt16 Len { get; set; }
    public UInt32 CRC { get; set; }
    public List<Byte> Message { get; private set; } = new List<Byte>();
}

public class MessageInfo
{
    public MessageState State { get; set; }
    public UInt16? CountAttemptsSendTx { get; set; }
    public UInt16? CountAttemptsReceivedACK { get; set; }
    public Int32? Timeout { get; set; }
    public PacketMessage PacketMessage { get; set; } = new PacketMessage();
}
public enum TypeMessage : Byte
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
