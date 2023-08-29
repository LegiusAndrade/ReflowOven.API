using ReflowOven.API.Integration.Peripheral.HostedServices;
using ReflowOven.API.Integration.Peripheral.ResourcesRPi.Checksum;
using ReflowOven.API.Integration.Peripheral.ResourcesRPi.CommuncationProtocols;
using ReflowOven.API.Integration.Peripheral.ResourcesRPi.Interfaces;

using System;
using System.IO.Ports;

namespace ReflowOven.API.Integration.Peripheral.ResourcesRPi;

public class SerialRPi
{

    private UInt128 countErrorTimeout;                                              // Counter for timeout errors

    readonly ILogger<BackgroundWorkerService> _logger;                              // Logging interface 

    private SerialPort? _sp_config;                                                 // SerialPort object for communication
    private readonly FullDuplexProtocol _protocol;                                  // Protocol handler object

    private readonly CRC16 _crc16Calculator = new CRC16();                          // CRC16 calculator object

    private readonly object syncLock = new object();                                // Lock object for thread safety

    // Tokens and tasks for thread control
    private CancellationTokenSource cancellationTokenSource;
    private Task sendingTask;
    private Task timeoutCheckingTask;

    private readonly MessageManager messageManager = new MessageManager();


    public SerialRPi(ILogger<BackgroundWorkerService> logger)
    {
        _logger = logger;
        _protocol = new FullDuplexProtocol();

        messageManager.ProtocolVersion = _protocol.PROTOCOL_VERSION;
        messageManager.TypeCRC = "CRC16"; //Todo poderia fazer o seguinte, um codigo para inicializar o protocolo e nele dizer qual tipo de crc que vai usar

        // Initialize other fields, or you can make them nullable if that makes more sense
        cancellationTokenSource = new CancellationTokenSource();
        sendingTask = Task.CompletedTask;  // Initialized with a completed task as a placeholder
        timeoutCheckingTask = Task.CompletedTask; // Initialized with a completed task as a placeholder
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
        _sp_config.DataReceived += new SerialDataReceivedEventHandler(OnDataReceived);

        try
        {
            _sp_config.Open();

            // Initialize tasks
            cancellationTokenSource = new CancellationTokenSource();
            sendingTask = Task.Run(() => SendSerialMessageAsync(cancellationTokenSource.Token));
            timeoutCheckingTask = Task.Run(() => TimeoutChecker(cancellationTokenSource.Token));

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
        cancellationTokenSource?.Cancel();
        sendingTask?.Wait();
        timeoutCheckingTask?.Wait();

        _sp_config?.Close();
        _sp_config?.Dispose();
        _logger.LogInformation("Closed serial from communication PCB");
        _sp_config = null;
    }

    // Sends a message to the buffer
    public void SendMessage(List<byte> buffer, UInt16 cmd)
    {

        if (buffer.Count > MessageConstants.MaxBuf - _protocol.SizeHeader)
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
                NumTries = 0,
                Cmd = cmd,
                Timeout = MessageConstants.TimeoutAck, // Define a 5-second timeout, for example
                Buffer = _protocol.SendMessageProtocol(buffer, cmd, _crc16Calculator.CalculateCRC16Wrapper),
            };

            messageManager.messageBuffer.Add(newMessage);
        }
    }

    // Task for sending messages from the queue
    private async Task SendSerialMessageAsync(CancellationToken token)
    {
        _logger.LogInformation("SendSerialMessageAsync task started.");
        while (true)
        {
            // Check if there are messages in the buffer
            if (messageManager.messageBuffer.Count == 0)
            {
                await Task.Delay(100, token); // Wait a bit before checking again
                continue;
            }

            MessageInfo? messageToSend = null;

            lock (syncLock)
            {
                foreach (var messageInfo in messageManager.messageBuffer)
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
                _sp_config!.Write(messageToSend.Buffer.ToArray(), 0, messageToSend.Buffer.Count);
                _logger.LogInformation("Message sent successfully.");

                messageToSend.State = MessageState.SENT;

                await Task.Delay(100, token); // Wait for 100 milliseconds before next iteration
            }
        }
    }


    // Adicione este método na sua classe SerialRPi
    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        lock (syncLock) // Bloqueio para garantir a segurança do thread
        {
            try
            {
                SerialPort sp = (SerialPort)sender;
                byte[] buffer = new byte[sp.BytesToRead];
                sp.Read(buffer, 0, buffer.Length);

                // TODO: Decodifique a mensagem aqui usando seu protocolo
                // Exemplo: var decodedMessage = _protocol.ReceiveMessageProtocol(buffer);

                _logger.LogInformation("Received data: {data}", BitConverter.ToString(buffer));

                // TODO: Fazer algo com a mensagem decodificada
                // Exemplo: ProcessReceivedMessage(decodedMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error receiving data: {message}", ex.Message);
            }
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
                foreach (var messageInfo in messageManager.messageBuffer)
                {
                    if (messageInfo.State == MessageState.SENT)
                    {
                        messageInfo.Timeout -= 10; 

                        if (messageInfo.Timeout <= 0)
                        {
                            _logger.LogWarning("Message timeout. Retrying...");
                            messageInfo.State = MessageState.READY_FOR_SEND;
                            messageInfo.NumTries++;

                            if (messageInfo.NumTries > MessageConstants.MaxTentatives)
                            {
                                _logger.LogError("Message failed after maximum retry attempts.");

                                // Increment the error timeout counter
                                countErrorTimeout++;

                                // Safely mark this message for removal or remove immediately if safe
                                messageManager.messageBuffer.Remove(messageInfo);
                            }
                        }
                    }
                }
            }
            await Task.Delay(10, cancellationToken); 
        }
    }

}
