namespace ReflowOven.API.Integration.Peripheral.ResourcesRPi.Entities
{
    public interface IStatusRPi
    {
        public enum Status
        {
            WithoutConfig = 0,
            Normal,
            ErrorUnknown,
            UndervoltageAC,
            UndervoltageDC,
            OvervoltageAC,
            OvervoltageDC,
            OvertemperaturePCB,
            OvertemperatureOven,
            LowSpeedFanPCB,
            LowSpeedFanOven,
            BadSignalWifi
        }
    
    }
}
