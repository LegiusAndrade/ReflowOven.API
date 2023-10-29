using ReflowOven.API.Integration.Peripheral.ResourcesRPi.Interfaces;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;

namespace ReflowOven.API.Integration.Peripheral.ResourcesRPi;

public class Utils
{
    public static byte[] GetBytes(bool value, bool littleEndian = false)
    {
        return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
    }

    public static byte[] GetBytes(char value, bool littleEndian = false)
    {
        return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
    }
    public static byte[] GetBytes(double value, bool littleEndian = false)
    {
        return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
    }
    public static byte[] GetBytes(float value, bool littleEndian = false)
    {
        return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
    }
    public static byte[] GetBytes(int value, bool littleEndian = false)
    {
        return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
    }
    public static byte[] GetBytes(long value, bool littleEndian = false)
    {
        return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
    }
    public static byte[] GetBytes(short value, bool littleEndian = false)
    {
        return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
    }
    public static byte[] GetBytes(uint value, bool littleEndian = false)
    {
        return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
    }
    public static byte[] GetBytes(ulong value, bool littleEndian = false)
    {
        return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
    }
    public static byte[] GetBytes(ushort value, bool littleEndian = false)
    {
        return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
    }

    private static byte[] ReverseAsNeeded(byte[] bytes, bool wantsLittleEndian)
    {
        if (wantsLittleEndian == BitConverter.IsLittleEndian)
            return bytes;
        else
            return (byte[])bytes.Reverse().ToArray();
    }

    public static int AddToOffset<T>(T value)
    {
        Type type = typeof(T);

        if (type == typeof(byte))
        {
            return 1;
        }
        if (type == typeof(ushort) || type == typeof(char))
        {
            return 2;
        }
        if (type == typeof(uint))
        {
            return 4;
        }
        // ... Adicione outros tipos conforme necessário

        throw new InvalidOperationException("Tipo não suportado.");
    }
    public static string ToHexString(byte[] bytes)
    {
        StringBuilder result = new StringBuilder(bytes.Length * 4); // 4 para incluir o "0x" e espaço
        foreach (byte b in bytes)
        {
            result.AppendFormat("0x{0:x2} ", b);
        }
        return result.ToString().Trim(); // Trim para remover o último espaço
    }
}
