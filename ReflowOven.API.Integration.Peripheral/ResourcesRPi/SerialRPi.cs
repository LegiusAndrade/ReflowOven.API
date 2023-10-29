using Microsoft.Extensions.Options;
using ReflowOven.API.Integration.Peripheral.HostedServices;
using ReflowOven.API.Integration.Peripheral.ResourcesRPi.Checksum;
using ReflowOven.API.Integration.Peripheral.ResourcesRPi.CommuncationProtocols;
using ReflowOven.API.Integration.Peripheral.ResourcesRPi.Interfaces;

using System;
using System.Device.Gpio;
using System.IO.Ports;
using System.Reflection;

namespace ReflowOven.API.Integration.Peripheral.ResourcesRPi;

public class SerialRPi : IDisposable
{

    private UInt128 countErrorTimeoutSend;                                   // Counter for timeout errors
    private UInt128 countErrorTimeoutReceivedACK;                            // Counter for timeout errors


    readonly ILogger<BackgroundWorkerService> _logger;                       // Logging interface 

    private SerialPort? _sp_config;                                          // SerialPort object for communication
    private readonly FullDuplexProtocol? _protocol;                          // Protocol handler object

    private readonly CRC16 _crc16Calculator = new();                         // CRC16 calculator object

    private readonly object syncLock = new();                                // Lock object for thread safety

    // Tokens and tasks for thread control
    private CancellationTokenSource cancellationTokenSource_SendSerialMessageAsync, cancellationTokenSource_TimeoutChecker;
    private Task sendingTask;
    private Task timeoutCheckingTask;

    private readonly RaspConfig _raspConfig;

    private readonly MessageManager messageManager = new();

    // Semaphore para sincronização assíncrona
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public SerialRPi(ILogger<BackgroundWorkerService> logger, FullDuplexProtocol protocol, IOptions<RaspConfig> raspConfigOptions)
    {
        _logger = logger;
        _protocol = protocol;

        _raspConfig = raspConfigOptions.Value;

        messageManager.ProtocolVersion = _protocol.VERSION_PROTOCOL;

        SetCRC("CRC16");

        cancellationTokenSource_SendSerialMessageAsync = new CancellationTokenSource();
        cancellationTokenSource_TimeoutChecker = new CancellationTokenSource();

        // Initialize other fields, or you can make them nullable if that makes more sense
        sendingTask = Task.CompletedTask;  // Initialized with a completed task as a placeholder
        timeoutCheckingTask = Task.CompletedTask; // Initialized with a completed task as a placeholder
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        CloseSerial();
    }

    public bool InitSerial(SerialPort sp_config)
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
        _sp_config.DataReceived += new SerialDataReceivedEventHandler(OnDataReceivedAsync);

        try
        {
            _sp_config.Open();

            _sp_config.DiscardInBuffer();
            _sp_config.DiscardOutBuffer();

            // Initialize tasks
            cancellationTokenSource_SendSerialMessageAsync = new CancellationTokenSource();
            cancellationTokenSource_TimeoutChecker = new CancellationTokenSource();
            sendingTask = Task.Run(() => SendSerialMessageAsync(cancellationTokenSource_SendSerialMessageAsync.Token));
            timeoutCheckingTask = Task.Run(() => TimeoutChecker(cancellationTokenSource_TimeoutChecker.Token));

            success = true;
            _logger.LogInformation("Opened serial port for communication with PCB");
        }
        catch (Exception e)
        {
            _logger.LogError(" Error in opening the serial port: {message}", e.Message);
        }
        return success;
    }

    // Closes the serial port and cleans up resources
    public void CloseSerial()
    {
        cancellationTokenSource_SendSerialMessageAsync?.Cancel();
        cancellationTokenSource_TimeoutChecker?.Cancel();
        sendingTask?.Wait();
        timeoutCheckingTask?.Wait();

        _sp_config?.Close();
        _sp_config?.Dispose();
        _logger.LogInformation("Closed serial from communication PCB");
        _sp_config = null;
    }

    // Sends a message to the buffer
    public void SendMessage(List<byte> buffer, byte cmd, bool isACK = false,UInt16 SequenceNumberForACK = 0)
    {

        if (buffer.Count > MessageConstants.MaxBuf - _protocol?.SizeHeader)
        {
            _logger.LogError("The message size exceeds the maximum allowed buffer size.");
            return;
        }
        lock (syncLock)
        {
            // Compile the message with the protocol and set the state to READY_FOR_SEND
            MessageInfo newMessage = new MessageInfo()
            {
                State = MessageState.READY_FOR_SEND,
                CountAttemptsSendTx = 0,
                Timeout = MessageConstants.TimeoutAck, // Define a 5-second timeout, for example
                PacketMessage = _protocol!.SendMessageProtocol(buffer, cmd, _crc16Calculator.CalculateCRC16Wrapper, isACK, (isACK? SequenceNumberForACK : (UInt16)0)),
            };

            messageManager.MessageBuffer.Add(newMessage);
        }
    }

    // Task for sending messages from the queue
    private async Task SendSerialMessageAsync(CancellationToken token)
    {
        try
        {
            _logger.LogInformation("SendSerialMessageAsync task started.");
            while (true)
            {
                // Check if there are messages in the buffer
                if (messageManager.MessageBuffer.Count == 0)
                {
                    await Task.Delay(5); // Wait a bit before checking again
                    continue;
                }

                MessageInfo? messageToSend = null;

                lock (syncLock)
                {
                    foreach (var messageInfo in messageManager.MessageBuffer)
                    {
                        if (messageInfo.State == MessageState.READY_FOR_SEND)
                        {
                            messageToSend = messageInfo;
                            break;
                        }
                    }
                }
                if (messageToSend != null)
                {
                    // Transmit the message here
                    _logger.LogInformation("Sending a message...");
                    await Task.Run(() =>
                    {
                        byte[] byteListHeader, byteListVersionProtocol, byteListTypeMessage, byteListSequenceNumber, byteListCmd, byteListLen, byteListMessage, byteListCRC;

                        byteListHeader = Utils.GetBytes(messageToSend.PacketMessage?.Header ?? 0, true);
                        byteListVersionProtocol = new byte[] { Convert.ToByte(messageToSend.PacketMessage?.VersionProtocol ?? 0) };
                        byteListTypeMessage = new byte[] { Convert.ToByte(messageToSend.PacketMessage?.TypeMessage ?? 0) };
                        byteListSequenceNumber = Utils.GetBytes(messageToSend.PacketMessage?.SequenceNumber ?? 0, true);
                        byteListCmd = new byte[] { Convert.ToByte(messageToSend.PacketMessage?.Cmd ?? 0) };
                        byteListLen = Utils.GetBytes(messageToSend.PacketMessage?.Len ?? 0, true);

                        if (messageManager.TypeCRC == "CRC16")
                        {
                            byteListCRC = Utils.GetBytes((UInt16)(messageToSend.PacketMessage?.CRC!.Value ?? 0), true);

                        }
                        else if (messageManager.TypeCRC == "CRC32")
                        {
                            byteListCRC = Utils.GetBytes((UInt32)(messageToSend.PacketMessage?.CRC!.Value ?? 0), true);
                        }
                        else
                        {
                            throw new InvalidOperationException("Unsupported CRC type"); // Throw an exception for unsupported types
                        }

                        byteListMessage = messageToSend.PacketMessage!.Message.ToArray();

                        byte[] byteList = new byte[][]
                        {
                            byteListHeader,
                            byteListVersionProtocol,
                            byteListTypeMessage,
                            byteListSequenceNumber,
                            byteListCmd,
                            byteListLen,
                            byteListMessage,
                            byteListCRC,
                        }
                        .SelectMany(b => b).ToArray();

                        _sp_config!.Write(byteList, 0, byteList.Length);
                    }, token);
                    _logger.LogInformation("Message sent successfully.");

                    if (messageToSend.PacketMessage?.TypeMessage == TypeMessage.MESSAGE_ACK)
                    {
                        messageManager.MessageBuffer.Remove(messageToSend);
                    }
                    else
                    {
                        messageToSend.State = MessageState.SENT;
                    }

                    await Task.Delay(5); // Wait for 100 milliseconds before next iteration
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Erro em SendSerialMessageAsync: {message}", ex.Message);
        }
    }


    private void OnDataReceivedAsync(object sender, SerialDataReceivedEventArgs e)
    {
        Task.Run(() => ProcessDataAsync(sender, e));
    }

    private async Task ProcessDataAsync(object sender, SerialDataReceivedEventArgs e)
    {
        await _semaphore.WaitAsync();

        try
        {
            SerialPort sp = (SerialPort)sender;
            byte[] buffer = new byte[sp.BytesToRead];
            sp.Read(buffer, 0, buffer.Length);

            List<byte> buf = buffer.ToList();
            MessageInfo? decodedMessage = new();
            try
            {
                decodedMessage = _protocol?.ReceivedMessageProtocol(buf, _crc16Calculator.CalculateCRC16Wrapper);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in decoded message: {message}", ex.Message);
            }

            // Log details of the decoded message only in the development environment (TODO: Ensure this only happens in the dev environment)
            _logger.LogDebug("Decoded Message Details: State={State}, Cmd={Cmd}, SequenceNumber={SequenceNumber}, NumTries={NumTries}, Timeout={Timeout}, Buffer={Buffer}",
                                         decodedMessage?.State,
                                         decodedMessage?.PacketMessage?.Cmd,
                                         decodedMessage?.PacketMessage?.SequenceNumber,
                                         decodedMessage?.CountAttemptsSendTx,
                                         decodedMessage?.Timeout,
                                         decodedMessage?.PacketMessage?.Message);

            if (decodedMessage != null)
            {
                ProcessDecodedMessage(decodedMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error in received message: {message}", ex.Message);
        }
        finally
        {
            //_sp_config?.DiscardInBuffer();
            _semaphore.Release();
        }

    }

    private void ProcessDecodedMessage(MessageInfo? decodedMessage)
    {
        // Attempt to find a matching message from the buffer based on SequenceNumber
        var messageFound = messageManager.MessageBuffer.FirstOrDefault(x => x.PacketMessage?.SequenceNumber == decodedMessage?.PacketMessage?.SequenceNumber);

        // Check the type of the message received
        if (decodedMessage?.PacketMessage?.TypeMessage == TypeMessage.MESSAGE_ACK)
        {
            // If ACK message received successfully, remove the corresponding message from the buffer
            HandleAckMessage(decodedMessage, messageFound);
        }
        else
        {
            // If message new, add buffer reading
            HandleNonAckMessage(decodedMessage, messageFound);
        }
    }


    private void HandleAckMessage(MessageInfo? decodedMessage, MessageInfo? messageFound)
    {
        if (decodedMessage?.State == MessageState.RECEIVED_SUCCESSFULL)
        {
            if (messageFound != null)
            {
                messageManager.MessageBuffer.Remove(messageFound); // Remove the object directly
            }
        }
        else if (decodedMessage?.State == MessageState.RECEIVED_CRC_ERROR)
        {
            if (messageFound != null)
                HandleCrcError(messageFound);

        }
    }

    //ALternative controller.Toggle ## not working in .net 7
    private static void TogglePin(GpioController controller, int pinNumber)
    {
        PinValue currentState = controller.Read(pinNumber);

        if (currentState == PinValue.High)
        {
            controller.Write(pinNumber, PinValue.Low);
        }
        else
        {
            controller.Write(pinNumber, PinValue.High);
        }
    }

    private void HandleNonAckMessage(MessageInfo? decodedMessage, MessageInfo? messageFound)
    {
        if (decodedMessage?.State == MessageState.RECEIVED_SUCCESSFULL) //New message for read
        {

            /* Toogle pin comm */
            try
            {
                using var controller = new GpioController();
                controller.OpenPin(_raspConfig.PinsConfig.LED_COMM, PinMode.Output);
                TogglePin(controller, _raspConfig.PinsConfig.LED_COMM);
                //controller.ClosePin(_raspConfig.PinsConfig.LED_COMM);

            }
            catch (Exception ex)
            {
                _logger.LogError("Error with GPIO: {message}", ex.Message);
            }
            if (decodedMessage == null || decodedMessage?.PacketMessage == null)
            {
                return;
            }

            SendMessage(decodedMessage.PacketMessage.Message, decodedMessage.PacketMessage.Cmd, true, decodedMessage.PacketMessage.SequenceNumber);

            // Signal that a new message is available for reading (TODO: Add the message to a read buffer)
            //TODO : SENd ACK for confirmation message
        }

        else if (decodedMessage?.State == MessageState.RECEIVED_CRC_ERROR && messageFound?.State == MessageState.SENT)
        {
            HandleCrcError(messageFound);
        }
    }

    private void HandleCrcError(MessageInfo messageFound)
    {
        messageFound.State = MessageState.READY_FOR_SEND;

        if (++messageFound.CountAttemptsReceivedACK > MessageConstants.MaxTentativeReceivedACK)
        {
            _logger.LogError("Message failed after maximum retry attempts for receveid ACK.");

            // Increment the error timeout counter
            countErrorTimeoutReceivedACK++;

            // Safely mark this message for removal or remove immediately if safe
            messageManager.MessageBuffer.Remove(messageFound);
        }
    }

    // Task for checking timeouts
    private async Task TimeoutChecker(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TimeoutChecker task started.");
        while (!cancellationToken.IsCancellationRequested)
        {
            lock (syncLock)
            {
                var itemsToRemove = new List<MessageInfo>();
                foreach (var messageInfo in messageManager.MessageBuffer)
                {
                    if (messageInfo.State == MessageState.SENT)
                    {
                        messageInfo.Timeout -= 1;

                        if (messageInfo.Timeout <= 0)
                        {
                            _logger.LogWarning("Message timeout. Retrying...");
                            messageInfo.State = MessageState.READY_FOR_SEND;

                            if (++messageInfo.CountAttemptsSendTx > MessageConstants.MaxTentativeSendMessage)
                            {
                                _logger.LogError("Message failed after maximum retry attempts.");

                                // Increment the error timeout counter
                                countErrorTimeoutSend++;

                                // Safely mark this message for removal or remove immediately if safe
                                itemsToRemove.Add(messageInfo);
                                //messageManager.messageBuffer.Remove(messageInfo);
                            }
                        }
                    }
                }
                foreach (var item in itemsToRemove)
                {
                    messageManager.MessageBuffer.Remove(item);
                }

            }
            await Task.Delay(1);
        }
    }

    private void SetCRC(string TypeCRC)
    {
        if (TypeCRC == "CRC16")
        {
            messageManager.TypeCRC = "CRC16"; //Todo poderia fazer o seguinte, um codigo para inicializar o protocolo e nele dizer qual tipo de crc que vai usar
        }
        else if (TypeCRC == "CRC32")
        {
            messageManager.TypeCRC = "CRC32";
        }
        else
        {
            throw new InvalidOperationException("Unsupported CRC type"); // Throw an exception for unsupported types
        }

        _protocol?.SetSizeHeader(messageManager.TypeCRC);
    }
    public void Dispose()
    {
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;  // Desregistrar o evento

        CloseSerial();
    }


}
