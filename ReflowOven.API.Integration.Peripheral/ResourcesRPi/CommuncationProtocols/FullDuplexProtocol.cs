﻿using ReflowOven.API.Integration.Peripheral.HostedServices;
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

    public void SetSizeHeader(string TypeCRC)
    {
        UInt16 SizeCRCReal = 0;
        CRC16 crc16 = new();

        if (TypeCRC == "CRC16")
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

        int SizeHeaderWithoutCRC = 0;
        foreach (var prop in typeof(PacketMessage).GetProperties())
        {
            if (prop.Name == "Header" || prop.Name == "VersionProtocol"
                || prop.Name == "SequenceNumber" || prop.Name == "Cmd" || prop.Name == "Len")
            {
                SizeHeaderWithoutCRC += Marshal.SizeOf(prop.PropertyType);
            }
        }
        SizeHeaderWithoutCRC += sizeof(TypeMessage);

        // Tamanho dos campos da classe PacketMessage exceto a lista Message e CRC
        int size = SizeHeaderWithoutCRC + SizeCRCReal;

        SizeHeader = (UInt16)size;
    }
    public List<byte> PacketMessageToBytes(PacketMessage packet, bool ignoreCRC = true)
    {
        List<byte> bytes = new();

        // Convert each property to bytes and add to the list
        bytes.AddRange(BitConverter.GetBytes(packet.Header));
        bytes.Add((byte)(packet.VersionProtocol));
        bytes.Add((byte)packet.TypeMessage);  // Assuming TypeMessage is an enum of type UInt16
        bytes.AddRange(BitConverter.GetBytes(packet.SequenceNumber));
        bytes.Add(packet.Cmd);
        bytes.AddRange(BitConverter.GetBytes(packet.Len));
        if (packet.CRC.HasValue && !ignoreCRC)
            bytes.AddRange(BitConverter.GetBytes(packet.CRC.Value));
        bytes.AddRange(packet.Message);

        return bytes;
    }


    // Method to send message according to the protocol
    public PacketMessage SendMessageProtocol(List<byte> buf, byte cmd, Func<List<byte>, object> calculateCRC, bool isACK = false, UInt16 sequenceNumberMessageForACK = 0)
    {
        PacketMessage messagePacket = new()
        {
            Header = MESSAGE_ID_SEND,
            VersionProtocol = VERSION_PROTOCOL,
            TypeMessage = isACK ? TypeMessage.MESSAGE_ACK : TypeMessage.MESSAGE_SEND,
            SequenceNumber = isACK ? sequenceNumberMessageForACK : sequenceNumber,
            Cmd = cmd,
            Len = (UInt16)buf.Count,
            CRC = null,
        };
        messagePacket.Message.AddRange(buf); // Add data

        var messagePacketToBytes = PacketMessageToBytes(messagePacket); // Without CRC

        if (!isACK) IncrementSequenceNumber(); // Increment the sequence number

        // Calculate the CRC using the provided delegate
        object crcObject = calculateCRC(messagePacketToBytes!);

        // Buffer to store the CRC bytes
        UInt32 crcBytes;

        // Check the type of CRC returned and cast it to appropriate type
        Console.WriteLine($"O tipo de crcObject é: {crcObject.GetType().Name}");
        if (crcObject is UInt16) // For CRC16
        {
            crcBytes = Convert.ToUInt32(crcObject);
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
        MessageInfo receivedMessage = new();
        if (buf.Count < SizeHeader)
        {
            _logger.LogError("Message corrupted or incomplete, contains only {0} bytes", buf.Count);
            return null;
        }
        try
        {
            receivedMessage.PacketMessage = DeserializeFromBytes(buf.ToArray());
        }
        catch (Exception ex)
        {
            byte[] buffer = buf.ToArray(); // Seu buffer
            string hexString = Utils.ToHexString(buffer);

            _logger.LogError($"Error in deserialize message: {ex.Message} - Buffer = {hexString}");

        }

        if (receivedMessage == null)
            return null;

        if (receivedMessage.PacketMessage == null)
        {
            _logger.LogError("here");
        }

        try
        {
            List<byte> receivedMessagePacketToBytes = PacketMessageToBytes(receivedMessage.PacketMessage ?? new PacketMessage
            {
                Header = 0,
                Cmd = 0,
                CRC = 0,
                Len = 0,
                SequenceNumber = 0,
                TypeMessage = 0,
                VersionProtocol = 0,
            });

            object calculatedCRC = calculateCRC(receivedMessagePacketToBytes);

            // Todo se for CRC32 aqui daria merda
            // Check CRC
            if ((UInt16)calculatedCRC == receivedMessage?.PacketMessage?.CRC)
            {
                CRC_Ok = true;
            }

            // Check if the message follows the protocol
            if (receivedMessage?.PacketMessage?.Header != MESSAGE_ID_SEND || receivedMessage?.PacketMessage?.VersionProtocol != VERSION_PROTOCOL)
            {
                _logger.LogError("Message does not follow protocol");
                return null;
            }

            receivedMessage.State = CRC_Ok ? MessageState.RECEIVED_SUCCESSFULL : MessageState.RECEIVED_CRC_ERROR;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error in calc CRC message: {message}", ex.Message);
        }

        return receivedMessage;
    }

    public static PacketMessage? DeserializeFromBytes(byte[] data)
    {
        int offset = 0;

        //if (data.Length < offset + 2) return null;
        PacketMessage packet = new()
        {
            Header = BitConverter.ToUInt16(data, offset)
        };
        offset += 2;

        //if (data.Length < offset + 1) return null;
        packet.VersionProtocol = data[offset];
        offset += 1;

        //if (data.Length < offset + 1) return null;
        packet.TypeMessage = (TypeMessage)BitConverter.ToChar(data, offset);
        offset += 1;

        //if (data.Length < offset + 2) return null;
        packet.SequenceNumber = BitConverter.ToUInt16(data, offset);
        offset += 2;

        //if (data.Length < offset + 1) return null;
        packet.Cmd = data[offset];
        offset += 1;

        //if (data.Length < offset + 2) return null;
        packet.Len = BitConverter.ToUInt16(data, offset);
        offset += 2;

        //if (packet.Len > MessageConstants.MaxBuf) return null;

        //if (data.Length < offset + packet.Len) return null;
        packet.Message = data.Skip(offset).Take(packet.Len).ToList();
        offset += packet.Len;

        // TODO: Certifique-se de ter espaço suficiente para ler o CRC aqui.
        //if (data.Length < offset + 2) return null; // Supondo que seu CRC é sempre de 2 bytes.
        packet.CRC = BitConverter.ToUInt16(data, offset);
        // Não há necessidade de ajustar o offset depois disso, já que terminamos.

        return packet;
    }
}
