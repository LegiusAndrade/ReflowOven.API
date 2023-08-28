using System.Device.Gpio;
using System.IO.Ports;
using System.Net.NetworkInformation;

namespace ReflowOven.API.Integration.Peripheral.HostedServices;

public class BackgroundWorkerService : BackgroundService
{
    readonly ILogger<BackgroundWorkerService> _logger;
    readonly SerialPort sp;

    public BackgroundWorkerService(ILogger<BackgroundWorkerService> logger)
    {
        _logger = logger;
        sp = new SerialPort("/dev/ttyS0")
        {
            BaudRate = 115200,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            WriteTimeout = TimeSpan.FromSeconds(3).Seconds,
            ReadTimeout = TimeSpan.FromMilliseconds(100).Seconds
        };

        // Handler data received
        sp.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
    }

    private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
    {
        SerialPort sPort = (SerialPort)sender;
        string indata = sPort.ReadExisting();
        _logger.LogInformation("Dados recebidos: {data}", indata);
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            sp.Open();
            _logger.LogInformation("Opened serial from communication PCB");
        }
        catch (Exception e)
        {
            _logger.LogError(" Error in the open serial port: {message}", e.Message);
        }

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        sp.Close();
        _logger.LogInformation("Closed serial from communication PCB");
        return base.StopAsync(cancellationToken);
    }


    bool ledOn = true;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
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
           await Task.Delay(1000,stoppingToken); //TODO: Quando ele dar um delay de 1000 ele vai ativar o stoppingToken, ver se não vai influencia no fechamento da serial
        }
    }
}
