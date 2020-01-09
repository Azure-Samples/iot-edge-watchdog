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
using EdgeHeartbeartMessage;
using Newtonsoft.Json;

namespace GWManagementFunctions
{
    public class EdgeHeartbeatLatencyRecord
    {
        public string DeviceId {get; private set;}
        public Int64 MessageId {get; private set;}
        public Int64 EdgeCreatedTimeTicks {get; private set;}
        public Int64 IoTHubEnqueuedTimeTicks {get; private set;}
        public Int64 AzFncInitializedTimeTicks {get; private set;}
        public Int64 EdgeToHubLatencyMs {get; private set;}
        public Int64 EdgeToAzFncLatencyMs {get; private set;}
        public EdgeHeartbeatLatencyRecord(string DeviceId, Int64 msgId ,Int64 EdgeCreatedTime, Int64 IotHubEnqueueTime
            , Int64 AzFncInitializedTime)
        {
            this.DeviceId = DeviceId;
            MessageId = msgId;
            EdgeCreatedTimeTicks = EdgeCreatedTime;
            IoTHubEnqueuedTimeTicks = IotHubEnqueueTime;
            AzFncInitializedTimeTicks= AzFncInitializedTime;
            EdgeToHubLatencyMs = (IoTHubEnqueuedTimeTicks - EdgeCreatedTimeTicks) / TimeSpan.TicksPerMillisecond;
            EdgeToAzFncLatencyMs = (AzFncInitializedTimeTicks - EdgeCreatedTimeTicks) / TimeSpan.TicksPerMillisecond;
        }
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

            // initialize IoT Hub Service CLient, this uses an adapter to wrap the 
            // service client code for testability
            var iotHubServiceClient = ServiceClient.CreateFromConnectionString(
                    Environment.GetEnvironmentVariable("IoTHubConnectionString"),
                    Microsoft.Azure.Devices.TransportType.Amqp);

            var serviceClientWrapper = new IoTHubServiceClient(iotHubServiceClient, logger); 

            // process message
            await message
                .ParseIoTHubMessage(logger)
                .AckDeviceMessage(serviceClientWrapper, logger)
                .SendStatisticsToTSI(tsiEventHub, logger, enqueuedTimeUtc, azFncInitializedTime);
        }

        public static Task<Heartbeat> ParseIoTHubMessage(this EventData message, ILogger log)
        {
            var rawMsg = Encoding.UTF8.GetString(message.Body.Array);
            Heartbeat msg = new Heartbeat();
            
            try
            { 
                msg = JsonParser.Default.Parse<Heartbeat>(rawMsg); 
            }
            catch(Exception e)
            {
                log.LogError(e, "Message is not in the format expected by the JSON parser.\n" + rawMsg);
                throw;
            }
            return Task.FromResult(msg);
        }

        public static async Task<Heartbeat> AckDeviceMessage (
            this Task<Heartbeat> messageTask, 
            IIoTHubServiceClient serviceClient,
            ILogger logger)
        {
            var msg = await messageTask;

            string deviceId = msg.Name;
            msg.MsgType = "Ack";

            var ackMessage = Google.Protobuf.JsonFormatter.Default.Format(msg);

            try
            {

                CloudToDeviceMethod method = new CloudToDeviceMethod("AckMessage");
                method.SetPayloadJson(ackMessage);
                logger.LogInformation("Sending C2D response to {0} with ID: {1}", msg.Name, msg.Id);

                // respond to device
                var directMethodResult = await serviceClient.InvokeDeviceMethodAsync(deviceId, "Heartbeat", method);
               
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

        public static async Task SendStatisticsToTSI(this Task<Heartbeat> messageTask,
                                                 IAsyncCollector<string> tsiEventHub,
                                                 ILogger logger,
                                                 DateTime enqueuedTimeUtc,
                                                 DateTime AzFncInitializedTime)
        {
            var msg = await messageTask;
            var  latencyRecord = 
                new EdgeHeartbeatLatencyRecord(msg.Name,
                    msg.Id,
                    msg.HeartbeatCreatedTicksUtc,
                    enqueuedTimeUtc.Ticks,
                    AzFncInitializedTime.Ticks);

            await tsiEventHub.AddAsync(JsonConvert.SerializeObject(latencyRecord));
        }
    }
}
