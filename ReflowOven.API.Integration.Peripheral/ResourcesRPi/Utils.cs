using System.Text.Json;

namespace ReflowOven.API.Integration.Peripheral.ResourcesRPi;

public class Utils
{

    public static byte[] SerializeToBytes<T>(T obj)
    {
        return JsonSerializer.SerializeToUtf8Bytes(obj);
    }

    public static T DeserializeFromBytes<T>(byte[] bytes)
    {
        return JsonSerializer.Deserialize<T>(bytes)!;
    }
}
