using ReflowOven.API.Integration.Peripheral.HostedServices;
using ReflowOven.API.Integration.Peripheral.ResourcesRPi.Interfaces;
using System;
using System.Collections.Generic;
using static ReflowOven.API.Integration.Peripheral.ResourcesRPi.CommuncationProtocols.FullDuplexProtocol;

namespace ReflowOven.API.Integration.Peripheral.ResourcesRPi.CommuncationProtocols;

public class FullDuplexProtocol
{


    readonly ILogger<BackgroundWorkerService> _logger;

    private const UInt16 MESSAGE_ID_SEND = 0xBEBE;   // Protocol init message for send
    public byte PROTOCOL_VERSION = 100;              // Protocol version: 1.00

    public UInt16 SizeHeader { get; private set; }

    private UInt16 sequenceNumber = 1;              // Sequence number for the protocol

    private object sequenceLock = new object();

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

    // Method to send message according to the protocol
    public List<byte> SendMessageProtocol(List<byte> buf, UInt16 cmd, Func<List<byte>, object> calculateCRC)
    {
        List<byte> messageBuffer = new()
        {
            (byte)((MESSAGE_ID_SEND & 0xFF00) >> 8),
            (byte)(MESSAGE_ID_SEND & 0x00FF),
            PROTOCOL_VERSION,
            (byte)TypeMessage.MESSAGE_SEND,
            (byte)((sequenceNumber & 0xFF00) >> 8),
            (byte)(sequenceNumber & 0x00FF),
            (byte)((cmd & 0xFF00) >> 8),
            (byte)(cmd & 0x00FF),
            (byte)((buf.Count & 0xFF00) >> 8),
            (byte)(buf.Count & 0x00FF)
        };
        messageBuffer.AddRange(buf); // Add data

        IncrementSequenceNumber(); // Increment the sequence number

        // Calculate the CRC using the provided delegate
        object crcObject = calculateCRC(messageBuffer);

        // Buffer to store the CRC bytes
        byte[] crcBytes;

        // Check the type of CRC returned and cast it to appropriate type
        if (crcObject is ushort crc16) // For CRC16
        {
            crcBytes = BitConverter.GetBytes(crc16);
        }
        else if (crcObject is uint crc32) // For CRC32
        {
            crcBytes = BitConverter.GetBytes(crc32);
        }
        else
        {
            throw new InvalidOperationException("Unsupported CRC type"); // Throw an exception for unsupported types
        }
        messageBuffer.AddRange(crcBytes); // Add the CRC to the message buffer

        /* Set size header */
        // Todo melhorar isso, poderia fazer algo do tipo quando iniciar o SerialRPi e definir o protocolo e o crc
        // Assim que defir deveria alterar essa var uma vez só, ou ter um campo do protocolo que define o tipo de CRC

        SizeHeader = (UInt16)(messageBuffer.Count - buf.Count);

        return messageBuffer; // Return the final message buffer
    }

    public MessageInfo? ReceivedMessageProtocol(List<byte> buf, Func<List<byte>, object> calculateCRC)
    {
        bool CRC_Ok = false;
        if (buf.Count < SizeHeader)
        {
            _logger.LogError("Message corrupted or incomplete, contains only {0} bytes", buf.Count);
            return null;
        }

        // Unpack the message bytes into relevant fields
        UInt16 messageId = (UInt16)((buf[0] << 8) | buf[1]);
        byte protocolVersion = buf[2];
        TypeMessage typeMessage = (TypeMessage)buf[3]; // Included the TypeMessage
        UInt16 sequenceNumber = (UInt16)((buf[4] << 8) | buf[5]);
        UInt16 cmd = (UInt16)((buf[6] << 8) | buf[7]);
        UInt16 dataSize = (UInt16)((buf[8] << 8) | buf[9]);

        // Get the last two bytes (received CRC)
        UInt16 receivedCRC = (UInt16)((buf[buf.Count - 2] << 8) | buf[buf.Count - 1]);

        // Create a sublist containing all bytes except the last two
        List<byte> bufWithoutCRC = buf.GetRange(0, buf.Count - 2);

        // Calculate the CRC of the sublist
        object calculatedCRC = calculateCRC(bufWithoutCRC);

        // Todo se for CRC32 aqui daria merda
        // Check CRC
        if ((UInt16)calculatedCRC == receivedCRC)
        {
            CRC_Ok = true;
        }

        // Check if the message follows the protocol
        if (messageId != MESSAGE_ID_SEND || protocolVersion != PROTOCOL_VERSION)
        {
            _logger.LogError("Message does not follow protocol");
            return null;
        }

        // Create and return a MessageInfo object
        MessageInfo receivedMessage = new MessageInfo
        {
            State = CRC_Ok ? MessageState.RECEIVED_SUCCESSFULL : MessageState.RECEIVED_CRC_ERROR,
            Cmd = cmd,
            SequenceNumber = sequenceNumber,
            Buffer = buf,
            TypeMessage = typeMessage
        };

        return receivedMessage;
    }
}
