namespace ReflowOven.API.Integration.Peripheral
{
    public class Settings
    {
        public int MaxRecordsInRequest { get; set; } = 30;
        public int MaxPageSize { get; set; } = 100;
        public string LocalLogFile { get; set; } = string.Empty;
    }
}
