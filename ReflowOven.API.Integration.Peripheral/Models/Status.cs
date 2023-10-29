using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReflowOven.API.Integration.Peripheral.Models
{
    [PrimaryKey(nameof(ID))]
    public class Status : IValidateObject
    {
        [Required]
        public int ID { get; set; }

        [Required]
        public UInt16 PowerVoltage { get; set; }

        [Required]
        public UInt16 ControlVoltage { get; set; }

        [Required]
        public UInt16 OutputVoltage { get; set; }

        [Required]
        public UInt16 CurrentOutput { get; set; }

        [Required]
        public Int16 TemperatureOven { get; set; }

        [Required]
        public Int16 TemperaturePCB { get; set; }

        [Required]
        public UInt16 FanRPMPCB { get; set; }

        [Required]
        public UInt16 FanRPMOven1 { get; set; }

        [Required]
        public UInt16 FanRPMOven2 { get; set; }

        [Required]
        public UInt32 HourmeterHours { get; set; }

        [Required]
        public Byte HourmeterMinutes { get; set; }

        [Required]
        public UInt32 Errors { get; set; }

    }
}