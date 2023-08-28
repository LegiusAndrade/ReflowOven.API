using System.Device.Gpio;
using System.Net.NetworkInformation;

namespace ReflowOven.API.Integration.Peripheral.HostedServices;

public class BackgroundWorkerService : BackgroundService
{
    readonly ILogger<BackgroundWorkerService> _logger;

    public BackgroundWorkerService(ILogger<BackgroundWorkerService> logger)
    {
        _logger = logger;
    }


    bool ledOn = true;
    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
         while(!stoppingToken.IsCancellationRequested)
        {
            const int Pin1 = 6;
            const int Pin2 = 13;
            using var controller = new GpioController();

            controller.OpenPin(Pin1, PinMode.Output);
            controller.OpenPin(Pin2, PinMode.Output);

            controller.Write(Pin1, ((ledOn) ? PinValue.Low : PinValue.High));
            controller.Write(Pin2, ((ledOn) ? PinValue.High : PinValue.Low));
            ledOn = !ledOn;

            _logger.LogInformation("Worker running at:{time}",DateTimeOffset.Now);
           await Task.Delay(1000,stoppingToken);
        }
    }
}
