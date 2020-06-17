using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using IoTHubTrigger = Microsoft.Azure.WebJobs.EventHubTriggerAttribute;
using Google.Protobuf;
using HeartbeatProto;
using Newtonsoft.Json;

namespace GWManagementFunctions
{
    public class EdgeHeartbeatLatencyRecord
    {
        public string DeviceId { get; private set; }
        public string ModuleId { get; private set; }
        public Int64 MessageId { get; private set; }
        public Int64 EdgeCreatedTimeTicks { get; private set; }
        public Int64 IoTHubEnqueuedTimeTicks { get; private set; }
        public Int64 AzFncInitializedTimeTicks { get; private set; }
        public Int64 EdgeToHubLatencyMs { get; private set; }
        public Int64 EdgeToAzFncLatencyMs { get; private set; }
        public EdgeHeartbeatLatencyRecord(string deviceId, string moduleId, Int64 msgId, Int64 EdgeCreatedTime, Int64 IotHubEnqueueTime
            , Int64 AzFncInitializedTime)
        {
            this.DeviceId = deviceId;
            ModuleId = moduleId;
            MessageId = msgId;
            EdgeCreatedTimeTicks = EdgeCreatedTime;
            IoTHubEnqueuedTimeTicks = IotHubEnqueueTime;
            AzFncInitializedTimeTicks = AzFncInitializedTime;
            EdgeToHubLatencyMs = (IoTHubEnqueuedTimeTicks - EdgeCreatedTimeTicks) / TimeSpan.TicksPerMillisecond;
            EdgeToAzFncLatencyMs = (AzFncInitializedTimeTicks - EdgeCreatedTimeTicks) / TimeSpan.TicksPerMillisecond;
        }
    }
    public class MessageExpiredException : System.Exception
    {
        private static readonly string DefaultMessage = "Message is older than the expiration window.";
        public MessageExpiredException() : base(DefaultMessage) { }
        public MessageExpiredException(string message) : base(message) { }
        public MessageExpiredException(string message, System.Exception innerException)
        : base(message, innerException) { }
    }
    public static class IoTHubListener
    {
        /// <summary>
        /// Receives device message from IoT Hub and continues respective durable function orchestration context
        /// </summary>
        /// <param name="message">message from IQRG GW arriving from IoT Hub</param>
        /// <param name="log"></param>
        /// <param name="client">Orchestration client</param>
        /// <returns></returns>
        [FunctionName("IoTHubListener")]
        public static async Task Run(
            [IoTHubTrigger("messages/events", Connection = "IoTHubEventHubEndpointConnectionString")]EventData message,
            [EventHub("dest", Connection="EventHubEndpointConnectionString")] IAsyncCollector<string> tsiEventHub,
            DateTime enqueuedTimeUtc,
            ILogger logger)
        {

            // capture current processing time 
            var azFncInitializedTime = DateTime.UtcNow;

            // Get expiration setting in minutes
            int MessageExpirationTimeinMinutes;
            if(!Int32.TryParse(Environment.GetEnvironmentVariable("MessageExpirationTimeinMinutes"), out MessageExpirationTimeinMinutes)) {
                MessageExpirationTimeinMinutes = 5;
            }

            // initialize IoT Hub Service CLient, this uses an adapter to wrap the 
            // service client code for testability
            var iotHubServiceClient = ServiceClient.CreateFromConnectionString(
                    Environment.GetEnvironmentVariable("IoTHubConnectionString"),
                    Microsoft.Azure.Devices.TransportType.Amqp);

            var serviceClientWrapper = new IoTHubServiceClient(iotHubServiceClient, logger); 

            // process message
            await message
                .ParseIoTHubMessage(azFncInitializedTime, MessageExpirationTimeinMinutes, logger)
                .AckDeviceMessage(serviceClientWrapper, logger)
                .SendStatisticsToTSI(tsiEventHub, logger, enqueuedTimeUtc, azFncInitializedTime);
        }

        public static Task<HeartbeatMessage> ParseIoTHubMessage(this EventData message, DateTime azFncInitializedTime, Int32 MessageExpirationTimeinMinutes, ILogger log)
        {
            var rawMsg = Encoding.UTF8.GetString(message.Body.Array);
            HeartbeatMessage msg = new HeartbeatMessage();
            
            try
            { 
                msg = JsonParser.Default.Parse<HeartbeatMessage>(rawMsg); 
                Int64 TimeSinceMessageCreated = (azFncInitializedTime.Ticks - msg.HeartbeatCreatedTicksUtc) / TimeSpan.TicksPerMillisecond;

                // Throw away messages older than set time (default 5 minutes)
                if(TimeSinceMessageCreated > (MessageExpirationTimeinMinutes * 60 * 1000)) {
                    throw new MessageExpiredException();
                }
            }
            catch(Exception e)
            {
                log.LogError(e, "Message is not in the format expected by the JSON parser.\n" + rawMsg);
                throw;
            }
            return Task.FromResult(msg);
        }

        public static async Task<HeartbeatMessage> AckDeviceMessage (
            this Task<HeartbeatMessage> messageTask, 
            IIoTHubServiceClient serviceClient,
            ILogger logger)
        {
            var msg = await messageTask;

            string deviceId = msg.DeviceId;
            string moduleId = msg.ModuleId;
            msg.MsgType = "Ack";

            var ackMessage = Google.Protobuf.JsonFormatter.Default.Format(msg);

            try
            {

                CloudToDeviceMethod method = new CloudToDeviceMethod("AckMessage");
                method.SetPayloadJson(ackMessage);
                logger.LogInformation("Sending C2D response to {0} with ID: {1}", deviceId, msg.Id);

                // respond to device
                var directMethodResult = await serviceClient.InvokeDeviceMethodAsync(deviceId, moduleId, method);
               
                // ToDo: error handling for failed cast required
                HttpStatusCode code = (HttpStatusCode)directMethodResult.Status;

                switch (code)
                {
                    case HttpStatusCode.OK: 
                        //log.LogInformation("Direct Method Call was successful");
                        break;
                    default:
                        break;
                }
            }
            catch(Exception e)
            {
                logger.LogError("Exception: {0}\nError Message: {1}\nStackTrace: {2}\n", e.Source, e.Message, e.StackTrace);
                throw;
            }
            return msg;
        }

        public static async Task SendStatisticsToTSI(this Task<HeartbeatMessage> messageTask,
                                                 IAsyncCollector<string> tsiEventHub,
                                                 ILogger logger,
                                                 DateTime enqueuedTimeUtc,
                                                 DateTime AzFncInitializedTime)
        {
            var msg = await messageTask;
            var  latencyRecord = 
                new EdgeHeartbeatLatencyRecord(msg.DeviceId,
                    msg.ModuleId,
                    msg.Id,
                    msg.HeartbeatCreatedTicksUtc,
                    enqueuedTimeUtc.Ticks,
                    AzFncInitializedTime.Ticks);

            await tsiEventHub.AddAsync(JsonConvert.SerializeObject(latencyRecord));
        }
    }
}
