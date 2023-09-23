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
    private const UInt16 TOOGLE_LED_WITH_ERROR = 200;   //250ms

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


        // Log or print the configuration values to see if they are correctly loaded
        logger.LogInformation($"Serial Name: {_raspConfig.SerialConfig.SerialName}");
        logger.LogInformation($"Baud Rate: {_raspConfig.SerialConfig.BaudRate}");

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
        SetStateRPi(IStatusRPi.Status.LowSpeedFanOven);
        var Task10ms = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                DoTask10ms();
                await Task.Delay(10);
            }
        }, stoppingToken);

        var TaskOther = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                DoTaskOther();
                await Task.Delay(100);  // 100 ms
            }
        }, stoppingToken);

        await Task.WhenAll(Task10ms, TaskOther);


        // while (!stoppingToken.IsCancellationRequested)
        // {



        //     /* Enviar status de keep alive */


        //     _logger.LogInformation("Worker running at:{time}", DateTimeOffset.Now);
        //     await Task.Delay(1000);
        // }
        // while (!stoppingToken.IsCancellationRequested)
        // {
        //     /* Verifica se tem mensagem nova */
        //     //Trata mensagem recebida da PCB

        // }
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
        if (cntSendIAmHere == TIME_I_AM_HERE / 100)
        {
            List<byte> data = new List<byte>() { (byte)0x01, (byte)0x02, (byte)0x03 };
            //_serialRPi.SendMessage(data, (byte)IStatusRPi.Commands.I_AM_HERE);
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
    private LEDPhase currentLEDPhase = LEDPhase.Blinking; // New variable to handle phase (Blinking or Waiting)
    private UInt32 cntDuringWait = 0; // Counter to track the time during the waiting phase

    private enum LEDPhase
    {
        Blinking,
        Waiting
    }

    private void StatusLEDControl(IStatusRPi.Status status)
    {
        cntUsedInStatusLed++;

        if (status == IStatusRPi.Status.Normal)
        {
            HandleNormalStatus();
        }
        else
        {
            HandleErrorStatus(status);
        }

        // Reset the counter when it reaches 2 seconds (considering a 10ms tick)
        if (cntUsedInStatusLed >= 200)
        {
            cntUsedInStatusLed = 0;
        }
    }
    private void HandleNormalStatus()
    {
        // For Normal status: Toggle LED every second (100 ticks of 10ms)
        if (cntUsedInStatusLed % 100 == 0)
        {
            ToggleLED(currentLEDState == LEDState.Off ? PinValue.High : PinValue.Low);
            currentLEDState = (currentLEDState == LEDState.Off) ? LEDState.On : LEDState.Off;
        }
    }
    private void HandleErrorStatus(IStatusRPi.Status status)
    {
        UInt16 onTime = TOOGLE_LED_WITH_ERROR;
        maxBlinkCount = (int)status;

        if (currentLEDPhase == LEDPhase.Blinking)
        {
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
                    else
                    {
                        currentLEDPhase = LEDPhase.Waiting; // Switch to Waiting phase after all blinks are done
                    }
                }
                else
                {
                    ToggleLED(PinValue.Low);
                    currentLEDState = LEDState.Off;
                }
            }
        }
        else if (currentLEDPhase == LEDPhase.Waiting)
        {
            cntDuringWait++;
            if (cntDuringWait >= 100) // If 1 second (assuming your tick is 10ms)
            {
                cntDuringWait = 0;
                blinkCount = 0;  // Reset counter to start blinking again
                currentLEDPhase = LEDPhase.Blinking; // Switch back to Blinking phase after 1 second of waiting
            }
        }
    }
    private void ToggleLED(PinValue value)
    {

         using (var controller = new GpioController())
        {
            controller.OpenPin(_raspConfig.PinsConfig.LED_STATUS, PinMode.Output);
            controller.Write(_raspConfig.PinsConfig.LED_STATUS, value);


            controller.ClosePin(_raspConfig.PinsConfig.LED_STATUS);
        } 
    }


}
