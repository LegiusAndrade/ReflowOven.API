using ReflowOven.API.Integration.Peripheral.ResourcesRPi;
using System.Device.Gpio;
using System.IO.Ports;
using System.Net.NetworkInformation;

namespace ReflowOven.API.Integration.Peripheral.HostedServices;

public class BackgroundWorkerService : BackgroundService
{
    readonly ILogger<BackgroundWorkerService> _logger;
    readonly SerialPort sp;

    private readonly SerialRPi _serialRPi;

    private readonly RaspConfig _raspConfig;


    public BackgroundWorkerService(ILogger<BackgroundWorkerService> logger,
        SerialRPi serialRPi, RaspConfig raspConfig)
    {
        _logger = logger;
        _serialRPi = serialRPi;
        _raspConfig = raspConfig;


        sp = new SerialPort(_raspConfig.SerialConfig.SerialName)
        {
            BaudRate = _raspConfig.SerialConfig.BaudRate,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            WriteTimeout = TimeSpan.FromSeconds(3).Milliseconds,
            ReadTimeout = TimeSpan.FromMilliseconds(100).Milliseconds
        };

        _serialRPi.InitSerial(sp);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {

            /* Toogle pin comm */
            using var controller = new GpioController();
            controller.OpenPin(_raspConfig.PinsConfig.LED_STATUS, PinMode.Output);
            controller.Toggle(_raspConfig.PinsConfig.LED_STATUS);

            /* Enviar status de keep alive */


            _logger.LogInformation("Worker running at:{time}", DateTimeOffset.Now);
            await Task.Delay(1000); 
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            /* Verifica se tem mensagem nova */
            //Trata mensagem recebida da PCB

        }
    }
}
