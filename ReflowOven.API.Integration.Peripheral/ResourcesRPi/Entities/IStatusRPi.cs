namespace ReflowOven.API.Integration.Peripheral.ResourcesRPi.Entities
{
    public interface IStatusRPi
    {
        public enum Status : byte
        {
            Normal = 0,
            WithoutConfig,
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

        public enum Commands : byte
        {
            I_AM_HERE = 1,
            GET_STATUS = 5,
            SEND_PROGRAM = 10,
            SEND_CONFIGURATION,
            SEND_SERIAL_NUMBER,
            SEND_CALIBRATION_DATA,
            
            GET_FAULT = 30,

            SAVE_CALIBRATION_VALUES = 50,

            RESET_HOURMETER = 60,



        }

    }
}
