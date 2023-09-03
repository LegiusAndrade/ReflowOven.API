using Microsoft.Extensions.Options;
using ReflowOven.API.Integration.Peripheral.ResourcesRPi;
using ReflowOven.API.Integration.Peripheral.ResourcesRPi.Entities;
using System.Device.Gpio;
using System.IO.Ports;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace ReflowOven.API.Integration.Peripheral.HostedServices;

public class BackgroundWorkerService : BackgroundService
{

    /// <summary>
    /// DEFINE SECTION
    /// </summary>

    private const UInt16 TOOGLE_LED = 1000;             //1s
    private const UInt16 TOOGLE_LED_WITH_ERROR = 250;   //250ms

    private const UInt16 TIME_I_AM_HERE = 1000;         //1s
        
    /// <summary>
    /// INSTANCES SECTION
    /// </summary>
    readonly ILogger<BackgroundWorkerService> _logger;
    readonly SerialPort sp;

    private readonly SerialRPi _serialRPi;

    private readonly RaspConfig _raspConfig;


    /// <summary>
    /// VARIABLES SECTION
    /// </summary>
    private UInt16 cntSendIAmHere = 0;

    private class SystemRPi
    {
        public static IStatusRPi.Status Status { get; set; } = IStatusRPi.Status.WithoutConfig;
        public static UInt32 Cntblabla { get; set; } = 0;
    } 

    public BackgroundWorkerService(ILogger<BackgroundWorkerService> logger,
        SerialRPi serialRPi, IOptions<RaspConfig> raspConfigOptions)
    {
        _logger = logger;
        _serialRPi = serialRPi;
        _raspConfig = raspConfigOptions.Value;


        sp = new SerialPort(_raspConfig.SerialConfig.SerialName)
        {
            BaudRate = (Int32)_raspConfig.SerialConfig.BaudRate,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            WriteTimeout = TimeSpan.FromMilliseconds(300).Milliseconds,
            ReadTimeout = TimeSpan.FromMilliseconds(100).Milliseconds
        };

        _serialRPi.InitSerial(sp);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SetStateRPi(IStatusRPi.Status.Normal);
        var Task10ms = Task.Run(async () => {
            while (!stoppingToken.IsCancellationRequested)
            {
                DoTask10ms();
                await Task.Delay(10); 
            }
        }, stoppingToken);

        var TaskOther = Task.Run(async () => {
            while (!stoppingToken.IsCancellationRequested)
            {
                DoTaskOther();
                await Task.Delay(100);  // 100 ms
            }
        }, stoppingToken);

        await Task.WhenAll(Task10ms, TaskOther);


        while (!stoppingToken.IsCancellationRequested)
        {

           

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


    
    private void DoTask10ms()
    {
        StatusLEDControl(SystemRPi.Status);
    }

    private void SetStateRPi(IStatusRPi.Status status)
    {
        SystemRPi.Status = status;
    }



    private void DoTaskOther()
    {
        cntSendIAmHere++;
        if(cntSendIAmHere == TIME_I_AM_HERE/100)
        {
            List<byte> data = new List<byte>() { (byte)0x01 };
            _serialRPi.SendMessage(data,(UInt16) IStatusRPi.Commands.I_AM_HERE);
            _logger.LogInformation("Send I Am Here at:{time}", DateTimeOffset.Now);
            cntSendIAmHere = 0;
        }
    }



    private enum LEDState
    {
        Off,
        On
    }

    private UInt32 cntUsedInStatusLed = 0;
    private int blinkCount = 0;
    private int maxBlinkCount = 0;
    private LEDState currentLEDState = LEDState.Off;

    private void StatusLEDControl(IStatusRPi.Status status)
    {
        cntUsedInStatusLed++;

        UInt16 onTime = (status == IStatusRPi.Status.Normal) ? TOOGLE_LED : TOOGLE_LED_WITH_ERROR;
        UInt16 offTime = TOOGLE_LED;
        maxBlinkCount = (status == IStatusRPi.Status.Normal) ? 1 : (int)status;

        if (cntUsedInStatusLed % (onTime / 10) == 0)
        {
            // Handle LED toggling logic
            if (currentLEDState == LEDState.Off)
            {
                if (blinkCount < maxBlinkCount)
                {
                    ToggleLED(PinValue.High);
                    currentLEDState = LEDState.On;
                    blinkCount++;
                }
            }
            else
            {
                ToggleLED(PinValue.Low);
                currentLEDState = LEDState.Off;

                if (blinkCount >= maxBlinkCount)
                {
                    blinkCount = 0;  // Reset counter after reaching max
                }
            }
        }

        // Reset the counter when it reaches the longer period
        if (cntUsedInStatusLed >= offTime / 10)
        {
            cntUsedInStatusLed = 0;
        }

    }
    private void ToggleLED(PinValue value)
    {
        using var controller = new GpioController();
        controller.OpenPin(_raspConfig.PinsConfig.LED_STATUS, PinMode.Output);
        controller.Write(_raspConfig.PinsConfig.LED_STATUS, value);
    }

}
