namespace ReflowOven.API.Integration.Peripheral.ResourcesRPi.Checksum;

public class CRC16
{
    // Polynomial for CRC-16-CCITT
    const UInt16 CRC_POLY = 0x1021; /* x^16 + x^12 + x^5 + x^0 */

    public object CalculateCRC16Wrapper(List<byte> data)
    {
        return CalculateCRC16_CCITT(data);
    }

    public UInt16 CalculateCRC16_CCITT(List<byte> buf)
    {
        ushort crc = 0xFFFF;    // Initialize CRC value

        // Loop through each byte in the input list
        for (int i = 0; i < buf.Count; i++)
        {
            // XOR the current data byte with the CRC
            crc ^= (ushort)(buf[i] << 8);

            // Loop through each bit of the byte
            for (int j = 0; j < 8; j++)
            {
                // Check if the MSB is set
                if ((crc & 0x8000) != 0)
                    // Shift left and XOR with polynomial
                    crc = (ushort)((crc << 1) ^ CRC_POLY);
                else
                    // Just shift left
                    crc <<= 1;
            }
        }
        // Return the computed CRC value
        return crc;
    }

    public UInt16 GetSizeCRC16 ()
    {
        return sizeof(UInt16);
    }
}
