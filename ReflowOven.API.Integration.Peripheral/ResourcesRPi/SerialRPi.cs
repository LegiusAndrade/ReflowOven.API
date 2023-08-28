using ReflowOven.API.Integration.Peripheral.HostedServices;
using ReflowOven.API.Integration.Peripheral.ResourcesRPi.Checksum;
using ReflowOven.API.Integration.Peripheral.ResourcesRPi.CommuncationProtocols;
using System;
using System.IO.Ports;

namespace ReflowOven.API.Integration.Peripheral.ResourcesRPi;

public class SerialRPi
{
    readonly ILogger<BackgroundWorkerService> _logger;
    private SerialPort? _sp_config;
    private readonly CRC16 _crc16Calculator = new CRC16();
    private readonly FullDuplexProtocol _protocol = new FullDuplexProtocol();

    public SerialRPi(ILogger<BackgroundWorkerService> logger)
    {
        _logger = logger;
    }

    public bool InitSerial(SerialPort sp_config, SerialDataReceivedEventHandler data_received)
    {
        bool success = false;

        _sp_config = new SerialPort(sp_config.PortName)
        {
            BaudRate = sp_config.BaudRate,
            DataBits = sp_config.DataBits,
            Parity = sp_config.Parity,
            StopBits = sp_config.StopBits,
            WriteTimeout = sp_config.WriteTimeout,
            ReadTimeout = sp_config.ReadTimeout
        };

        // Handler data received
        _sp_config.DataReceived += new SerialDataReceivedEventHandler(data_received);

        try
        {
            _sp_config.Open();
            success = true;
            _logger.LogInformation("Opened serial port for communication with PCB");
        }
        catch (Exception e)
        {
            _logger.LogError(" Error in opening the serial port: {message}", e.Message);
        }
        return success;
    }
    public void CloseSerial()
    {
        _sp_config?.Close();
        _sp_config?.Dispose();
        _logger.LogInformation("Closed serial from communication PCB");
        _sp_config = null;
    }

    public void SendSerialMessage (List<byte> buffer, UInt16 cmd)
    {
 
        List<byte> data_formatted = _protocol.SendMessageProtocol(buffer, cmd, _crc16Calculator.CalculateCRC16Wrapper);


    }


}
