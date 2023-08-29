using System;
using System.Collections.Generic;
using static ReflowOven.API.Integration.Peripheral.ResourcesRPi.CommuncationProtocols.FullDuplexProtocol;

namespace ReflowOven.API.Integration.Peripheral.ResourcesRPi.CommuncationProtocols;

public class FullDuplexProtocol
{
    const UInt16 MESSAGE_ID_SEND = 0xBEBE;           // Protocol init message for send
    public byte PROTOCOL_VERSION = 100;               // Protocol version: 1.00

    public UInt16 SizeHeader { get; private set; }

    private UInt16 sequenceNumber = 1;              // Sequence number for the protocol

    private object sequenceLock = new object();

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
        List<byte> messageBuffer = new List<byte>();

        messageBuffer.Add((byte)((MESSAGE_ID_SEND & 0xFF00) >> 8));
        messageBuffer.Add((byte)(MESSAGE_ID_SEND & 0x00FF));
        messageBuffer.Add(PROTOCOL_VERSION);
        messageBuffer.Add((byte)((sequenceNumber & 0xFF00) >> 8));
        messageBuffer.Add((byte)(sequenceNumber & 0x00FF));
        messageBuffer.Add((byte)((cmd & 0xFF00) >> 8));
        messageBuffer.Add((byte)(cmd & 0x00FF));
        messageBuffer.Add((byte)((buf.Count & 0xFF00) >> 8));
        messageBuffer.Add((byte)(buf.Count & 0x00FF));

        IncrementSequenceNumber(); // Increment the sequence number

        // Calculate the CRC using the provided delegate
        object crcObject = calculateCRC(messageBuffer);

        // Buffer to store the CRC bytes
        byte[] crcBytes;

        // Check the type of CRC returned and cast it to appropriate type
        if (crcObject is ushort) // For CRC16
        {
            crcBytes = BitConverter.GetBytes((ushort)crcObject);
        }
        else if (crcObject is uint) // For CRC32
        {
            crcBytes = BitConverter.GetBytes((uint)crcObject);
        }
        else
        {
            throw new InvalidOperationException("Unsupported CRC type"); // Throw an exception for unsupported types
        }

        messageBuffer.AddRange(crcBytes); // Add the CRC to the message buffer

        /* Set size header */
        SizeHeader = (UInt16)(messageBuffer.Count - buf.Count);

        return messageBuffer; // Return the final message buffer
    }

    public string ReceiveMessage()
    {
        // Implemente a lógica de recebimento aqui.
        return "";
    }
}
