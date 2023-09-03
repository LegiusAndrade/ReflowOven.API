namespace ReflowOven.API.Integration.Peripheral;

public class RaspConfig
{

    public required Pins PinsConfig { get; set; } 
    public required Serial SerialConfig { get; set; }

    public class Pins
    {
        public UInt16 LED_STATUS { get; set; } = 6;
        public UInt16 LED_COMM { get; set; } = 13;
        public UInt16 PG { get; set; } = 19;

    }

    public class Serial
    {
        public Int32 BaudRate { get; set; } = 9600;
        public String SerialName { get; set; } = "/dev/ttyS0"; 
    }

}
