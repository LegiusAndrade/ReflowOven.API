using ReflowOven.API.Integration.Peripheral.HostedServices;
using ReflowOven.API.Integration.Peripheral.ResourcesRPi.Checksum;
using ReflowOven.API.Integration.Peripheral.ResourcesRPi.Interfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using static ReflowOven.API.Integration.Peripheral.ResourcesRPi.CommuncationProtocols.FullDuplexProtocol;

namespace ReflowOven.API.Integration.Peripheral.ResourcesRPi.CommuncationProtocols;

public class FullDuplexProtocol
{
    readonly ILogger<BackgroundWorkerService> _logger;

    private const UInt16 MESSAGE_ID_SEND = 0xBEBE;   // Protocol init message for send
    public byte VERSION_PROTOCOL = 100;              // Protocol version: 1.00

    public UInt16 SizeHeader { get; private set; }

    private UInt16 sequenceNumber = 1;              // Sequence number for the protocol

    private readonly object sequenceLock = new();

    public FullDuplexProtocol(ILogger<BackgroundWorkerService> logger)
    {
        _logger = logger;
    }

    // Method to increment the sequence number
    public void IncrementSequenceNumber()
    {
        lock (sequenceLock)
        {
            if (sequenceNumber == UInt16.MaxValue)
            {
                sequenceNumber = 1; // Reset to 1 if it reaches the maximum value
            }
            else
            {
                sequenceNumber++; // Otherwise, simply increment it
            }
        }
    }

    public void SetSizeHeader (string TypeCRC)
    {
        UInt16 SizeCRCReal = 0;
        CRC16 crc16 = new();

        if(TypeCRC == "CRC16")
        {
            SizeCRCReal = crc16.GetSizeCRC16();
        }
        else if (TypeCRC == "CRC32")
        {
            SizeCRCReal = 0;
        }
        else
        {
            throw new InvalidOperationException("Unsupported CRC type"); // Throw an exception for unsupported types
        }

        // Tamanho total da classe PacketMessage
        int totalSize = Marshal.SizeOf(typeof(PacketMessage));

        // Tamanho do campo Message (que é uma lista)
        int messageSize = Marshal.SizeOf(typeof(List<byte>));

        // Obtém o tipo do campo CRC dinamicamente pelo nome
        Type crcType = typeof(PacketMessage).GetProperty("CRC").PropertyType;

        // Calcula o tamanho do campo CRC com base em seu tipo
        int crcSizeType = Marshal.SizeOf(crcType);

        // Tamanho dos campos da classe PacketMessage exceto a lista Message e CRC
        int size = totalSize - messageSize - crcSizeType + SizeCRCReal;

        SizeHeader = (UInt16)size;
    }
    public List<byte> PacketMessageToBytes(PacketMessage packet)
    {
        List<byte> bytes = new List<byte>();

        // Convert each property to bytes and add to the list
        bytes.AddRange(BitConverter.GetBytes(packet.Header));
        bytes.AddRange(BitConverter.GetBytes(packet.VersionProtocol));
        bytes.AddRange(BitConverter.GetBytes((UInt16)packet.TypeMessage));  // Assuming TypeMessage is an enum of type UInt16
        bytes.AddRange(BitConverter.GetBytes(packet.SequenceNumber));
        bytes.Add(packet.Cmd);
        bytes.AddRange(BitConverter.GetBytes(packet.Len));
        if (packet.CRC.HasValue)
            bytes.AddRange(BitConverter.GetBytes(packet.CRC.Value));
        bytes.AddRange(packet.Message);

        return bytes;
    }


    // Method to send message according to the protocol
    public PacketMessage SendMessageProtocol(List<byte> buf, byte cmd, Func<List<byte>, object> calculateCRC)
    {
        PacketMessage messagePacket = new()
        {
            Header = MESSAGE_ID_SEND,
            VersionProtocol = VERSION_PROTOCOL,
            TypeMessage = TypeMessage.MESSAGE_SEND,
            SequenceNumber = sequenceNumber,
            Cmd = cmd,
            Len = (UInt16)buf.Count,
            CRC = null,
        };
        messagePacket.Message.AddRange(buf); // Add data

        var messagePackettoBytes = PacketMessageToBytes(messagePacket); // Without CRC

        IncrementSequenceNumber(); // Increment the sequence number

        // Calculate the CRC using the provided delegate
        object crcObject = calculateCRC(messagePackettoBytes!);

        // Buffer to store the CRC bytes
        UInt32 crcBytes;

        // Check the type of CRC returned and cast it to appropriate type
        if (crcObject is UInt16 ) // For CRC16
        {
            crcBytes = (UInt32)crcObject;
        }
        else
        {
            throw new InvalidOperationException("Unsupported CRC type"); // Throw an exception for unsupported types
        }
        messagePacket.CRC = crcBytes; // Add the CRC to the message buffer

        /* Set size header */
        // Todo melhorar isso, poderia fazer algo do tipo quando iniciar o SerialRPi e definir o protocolo e o crc
        // Assim que defir deveria alterar essa var uma vez só, ou ter um campo do protocolo que define o tipo de CRC

        return messagePacket; // Return the final message buffer
    }

    public MessageInfo? ReceivedMessageProtocol(List<byte> buf, Func<List<byte>, object> calculateCRC)
    {
        bool CRC_Ok = false;
        if (buf.Count < SizeHeader)
        {
            _logger.LogError("Message corrupted or incomplete, contains only {0} bytes", buf.Count);
            return null;
        }
        MessageInfo receivedMessage = new();

         receivedMessage.PacketMessage = Utils.DeserializeFromBytes<PacketMessage>(buf);



        // Unpack the message bytes into relevant fields
        //UInt16 messageId = (UInt16)((buf[0] << 8) | buf[1]);
       //byte protocolVersion = buf[2];
        //TypeMessage typeMessage = (TypeMessage)buf[3]; // Included the TypeMessage
        //UInt16 sequenceNumber = (UInt16)((buf[4] << 8) | buf[5]);
        //byte cmd = buf[6];
        //UInt16 dataSize = (UInt16)((buf[7] << 8) | buf[8]);

        // Get the last two bytes (received CRC)
        //UInt16 receivedCRC = (UInt16)((buf[buf.Count - 2] << 8) | buf[buf.Count - 1]);

        // Create a sublist containing all bytes except the last two
        //List<byte> bufWithoutCRC = buf.GetRange(0, buf.Count - 2);
        todo aquiii
        // Calculate the CRC of the sublist
        object calculatedCRC = calculateCRC(bufWithoutCRC);

        // Todo se for CRC32 aqui daria merda
        // Check CRC
        if ((UInt16)calculatedCRC == receivedCRC)
        {
            CRC_Ok = true;
        }

        // Check if the message follows the protocol
        if (messageId != MESSAGE_ID_SEND || protocolVersion != VERSION_PROTOCOL)
        {
            _logger.LogError("Message does not follow protocol");
            return null;
        }


        // Create and return a MessageInfo object
        MessageInfo receivedMessage = new MessageInfo
        {
            State = CRC_Ok ? MessageState.RECEIVED_SUCCESSFULL : MessageState.RECEIVED_CRC_ERROR,
            PacketMessage =
            {
                Cmd=cmd,
                SequenceNumber = sequenceNumber,
                TypeMessage = typeMessage,
            }
        };
        receivedMessage.PacketMessage.Message.AddRange(buf);

        return receivedMessage;
    }
}
