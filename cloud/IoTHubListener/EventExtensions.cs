using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using HeartbeatProto;
using Newtonsoft.Json;

namespace GWManagementFunctions
{
    public static class EventExtensions
    {

        public static Task<HeartbeatMessage> ParseIoTHubMessage(
                this EventData message,
                DateTime azFncInitializedTime,
                Int32 MessageExpirationTimeinMinutes,
                ILogger log)
        {
            var rawMsg = Encoding.UTF8.GetString(message.Body.Array);

            try
            {
				var msg = JsonParser.Default.Parse<HeartbeatMessage>(rawMsg);

				Int64 TimeSinceMessageCreated = (azFncInitializedTime.Ticks -
                                                 msg.HeartbeatCreatedTicksUtc) / TimeSpan.TicksPerMillisecond;

                // Throw away messages older than set time (default 5 minutes)
                if(TimeSinceMessageCreated > (MessageExpirationTimeinMinutes * 60 * 1000)) {
                    throw new MessageExpiredException();
                }
                return Task.FromResult(msg);
            }
            catch(Exception e)
            {
                log.LogError(
                        e,
                        $"Message is not in the format expected by the JSON parser.\n {rawMsg}");
                throw;
            }
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
                var directMethodResult =
                    await serviceClient.InvokeDeviceMethodAsync(deviceId, moduleId, method);

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
                logger.LogError(
                        "Exception: {0}\nError Message: {1}\nStackTrace: {2}\n",
                        e.Source,
                        e.Message,
                        e.StackTrace);
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
