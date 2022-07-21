using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Google.Protobuf;
using HeartbeatProto;
using Newtonsoft.Json.Linq;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace HeartbeatModule
{
    public class Program
    {
        static string MODULE_ID = "Watchdog";
        static int START_WINDOW_IN_SECONDS = 1;
        static int END_WINDOW_IN_SECONDS = 5;
        static int HEARTBEAT_FREQUENCY_IN_SECONDS = 10;

        enum MessageStatus { Sent, Acked };
        enum DeviceStatus { Online, Offline };
        static int counter;
        static DeviceStatus connectivityStatus = DeviceStatus.Offline;
        static Dictionary<Int64, MessageStatus> HbStatus = new Dictionary<Int64, MessageStatus>();

        // The follow variables can be updated with the moduleTwin and are updated through the 
        // OnDesiredPropertiesUpdate method
        static uint backoffExp = 1; // used for exponential backoff
        static string deviceId = Environment.GetEnvironmentVariable($"IOTEDGE_DEVICEID");
        static string moduleId = GetStringEnvVar($"IOTEDGE_MODULEID", MODULE_ID);
        static TimeSpan startWindow = GetTimeSpanEnvVar("START_WINDOW_IN_SECONDS", START_WINDOW_IN_SECONDS);
        static TimeSpan endWindow = GetTimeSpanEnvVar("END_WINDOW_IN_SECONDS", END_WINDOW_IN_SECONDS);
        static TimeSpan hartbeatFrequency = GetTimeSpanEnvVar("HEARTBEAT_FREQUENCY_IN_SECONDS", HEARTBEAT_FREQUENCY_IN_SECONDS);
        static TimeSpan defaultEndWindow = endWindow;
        static LoggingLevelSwitch levelSwitch = new LoggingLevelSwitch();

        static async Task Main(string[] args)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Debug;
            Log.Logger = new LoggerConfiguration().MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss K} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            //To enable the debug wait code, pass 'true' to Init here Init(true);
            ModuleClient ioTHubModuleClient = await Init();
            Log.Information("DeviceId: {deviceId}, ModuleId: {moduleId}, Start Window: {StartWindow}, End Window: {EndWindow}, Beat Frequency: {BeatFrequency}"
                , deviceId, moduleId, startWindow, endWindow, hartbeatFrequency);

            var moduleTwin = await ioTHubModuleClient.GetTwinAsync();

            // If any variables are set in the desired properties of the module twin, 
            // update them here
            await OnDesiredPropertiesUpdate(moduleTwin.Properties.Desired, ioTHubModuleClient);

            // Attach a callback for updates to the module twin's desired properties.
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

            // Int64 has a max value of 9,223,372,036,854,775,807 and wraps to -9,223,372,036,854,775,808 
            // if it overflows
            // This number of message at 28,000,000,000 (billion) will not overflow for 
            // ~902,482 years   (MaxValue / 28B / 365)
            Int64 msgId = 0;

            // Send heartbeats to IoTHub at a specified frequency
            while (true)
            {
                await SendHeartbeat(ioTHubModuleClient, Interlocked.Read(ref msgId));
                // delay for window, check internal state for update
                await Task.Delay(endWindow);
                if (HbStatus[msgId] != MessageStatus.Acked)
                {
                    DeviceTimeout();
                }
                else if (connectivityStatus == DeviceStatus.Offline)
                {
                    Log.Information("Device ID: {deviceId} is online.", deviceId);
                    connectivityStatus = DeviceStatus.Online;
                    endWindow = defaultEndWindow;
                }
                HbStatus.Remove(msgId);

                Interlocked.CompareExchange(ref msgId, 0, System.Int64.MaxValue);
                Interlocked.Increment(ref msgId);

                if (hartbeatFrequency - endWindow > TimeSpan.Zero) await Task.Delay(hartbeatFrequency - endWindow);
            }
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        public static TimeSpan GetTimeSpanEnvVar(string varName, double defaultValue)
        {
            var strValue = Environment.GetEnvironmentVariable(varName);
            double doubleValue;
            // If the parse fails, assign the default value
            if (!double.TryParse(strValue, out doubleValue))
            {
                doubleValue = defaultValue;
            }
            return TimeSpan.FromSeconds(doubleValue);
        }

        public static string GetStringEnvVar(string varName, string defaultValue)
        {
            var strValue = Environment.GetEnvironmentVariable(varName);
            if (string.IsNullOrEmpty(strValue) 
                || strValue.Contains('<') || strValue.Contains('>') || strValue.Contains('$'))
                return defaultValue;

            return strValue;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages
        /// </summary>
        static async Task<ModuleClient> Init(bool debug = false)
        {
            //This debug code enables code execution to pause while you attach the debugger
            //See instructions in the Main method to pass true to the debug variable.
            //Otherwise, when debugging, some code will run before you can attach the debugger.
#if DEBUG            
            while (debug && !Debugger.IsAttached)
            {
                Log.Verbose("Module waiting for debugger to attach...");
                await Task.Delay(1000);
            };
#endif
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Log.Information("IoT Hub Heartbeat module client on {deviceId} initialized", deviceId);

            // Register callback to be called when a message is received by the module
            await ioTHubModuleClient.SetMethodDefaultHandlerAsync(AckMessage, ioTHubModuleClient);

            return ioTHubModuleClient;
        }

        private static async Task SendHeartbeat(ModuleClient moduleClient, Int64 msgId)
        {
            Log.Information("{deviceId} sending heartbeat {MsgId}", deviceId, msgId);
            try
            {
                HeartbeatMessage msg = new HeartbeatMessage
                {
                    MsgType = "Heartbeat",
                    DeviceId = deviceId,
                    ModuleId = moduleId,
                    Id = (Int64)msgId,
                    HeartbeatCreatedTicksUtc = DateTime.UtcNow.Ticks
                };
                var json = Google.Protobuf.JsonFormatter.Default.Format(msg);
                Message sendMsg = new Message(Encoding.UTF8.GetBytes(json));
                sendMsg.Properties.Add("msgType", "watchdog");

                HbStatus[msgId] = MessageStatus.Sent;

                await moduleClient.SendEventAsync("sendHeartbeat", sendMsg);
            }
            catch (Exception e)
            {
                Log.Error("Error Sending Heartbeat: {Error}", e);
                Console.WriteLine(e.Message);
            }
        }

        private static void DeviceTimeout()
        {
            // Update this function if you want something else to happen when the device detects
            // it is offline.
            Log.Information("Client Name: {deviceId} timed out and is offline", deviceId);
            connectivityStatus = DeviceStatus.Offline;
            backoffExp++;
            endWindow = TimeSpan.FromSeconds(Math.Pow(defaultEndWindow.TotalSeconds, backoffExp));
        }

        /// <summary>
        /// When the Azure function sends a DirectMethod to the module ACKing the 
        //  message the device sent, the device updates the msgId status to ACKed
        /// </summary>
        static Task<MethodResponse> AckMessage(MethodRequest message, object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain expected values");
            }

            byte[] messageBytes = message.Data;
            string messageString = Encoding.UTF8.GetString(messageBytes);

            if (!string.IsNullOrEmpty(messageString))
            {
                var msg = JsonParser.Default.Parse<HeartbeatMessage>(messageString);
                Log.Information("Heartbeat message {MsgId} on {deviceId} acknowledged.", msg.Id, deviceId);
                if (HbStatus.ContainsKey(msg.Id)) HbStatus[msg.Id] = MessageStatus.Acked;
            }
            else
            {
                Log.Information("Heartbeat acknowledge message failed with no data received on {deviceID}", deviceId);
            }

            return Task.FromResult(new MethodResponse(200));
        }

        static Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                Log.Information("New desired properties requested: {DesiredProperties}", desiredProperties.ToString());
                //The majority of these have to be converted to JValue first because otherwise
                //they are interpreted as dynamics and cannot be converted to Double -> TimeSpan.
                //By converting to JValue, the are represented as objects which then are able 
                //to convert.
                if (desiredProperties.Contains("backoffExp") && desiredProperties["backoffExp"] != null)
                {
                    backoffExp = desiredProperties["backoffExp"];
                }
                if (desiredProperties.Contains("startWindow") && desiredProperties["startWindow"] != null)
                {
                    var rawStartWindow = desiredProperties["startWindow"] as JValue;
                    startWindow = TimeSpan.FromSeconds(Convert.ToDouble(rawStartWindow.Value));
                }
                if (desiredProperties.Contains("endWindow") && desiredProperties["endWindow"] != null)
                {
                    var rawEndWindow = desiredProperties["endWindow"] as JValue;
                    endWindow = TimeSpan.FromSeconds(Convert.ToDouble(rawEndWindow.Value));
                }
                if (desiredProperties.Contains("defaultEndWindow") && desiredProperties["defaultEndWindow"] != null)
                {
                    var rawDefaultEndWindow = desiredProperties["defaultEndWindow"] as JValue;
                    defaultEndWindow = TimeSpan.FromSeconds(Convert.ToDouble(rawDefaultEndWindow.Value));
                }
                if (desiredProperties.Contains("beatFrequency") && desiredProperties["beatFrequency"] != null)
                {
                    var rawBeatFrequency = desiredProperties["beatFrequency"] as JValue;
                    hartbeatFrequency = TimeSpan.FromSeconds(Convert.ToDouble(rawBeatFrequency.Value));
                }
                if (desiredProperties.Contains("logEventLevel") && desiredProperties["logEventLevel"] != null)
                {
                    switch (desiredProperties["logEventLevel"].ToString().ToLower())
                    {
                        case "verbose":
                            levelSwitch.MinimumLevel = LogEventLevel.Verbose;
                            break;
                        case "debug":
                            levelSwitch.MinimumLevel = LogEventLevel.Debug;
                            break;
                        case "information":
                            levelSwitch.MinimumLevel = LogEventLevel.Information;
                            break;
                        case "error":
                            levelSwitch.MinimumLevel = LogEventLevel.Error;
                            break;
                        case "fatal":
                            levelSwitch.MinimumLevel = LogEventLevel.Fatal;
                            break;
                        default:
                            levelSwitch.MinimumLevel = LogEventLevel.Debug;
                            Log.Debug("Level Switch cannot be changed to {DesiredProperty} and is now set to Level:Debug"
                                , desiredProperties["logEventLevel"]);
                            break;
                    }
                }
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Log.Error("Error when receiving desired property: {Exception}", exception);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error when receiving desired property: {Exception}", ex);
            }
            return Task.CompletedTask;
        }
    }
}
