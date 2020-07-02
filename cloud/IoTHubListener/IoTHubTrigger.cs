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
    public static class IoTHubListener
    {
        /// <summary>
        /// Receives device message from IoT Hub and continues respective durable function
        /// orchestration context
        /// </summary>
        /// <param name="message">message from IQRG GW arriving from IoT Hub</param>
        /// <param name="log"></param>
        /// <param name="client">Orchestration client</param>
        /// <returns></returns>
        [FunctionName("IoTHubListener")]
        public static async Task Run(
            [IoTHubTrigger(
                "messages/events",
                Connection = "IoTHubEventHubEndpointConnectionString")]
            EventData message,
            [EventHub(
                "dest",
                Connection="EventHubEndpointConnectionString")]
            IAsyncCollector<string> tsiEventHub,
            DateTime enqueuedTimeUtc,
            ILogger logger)
        {

            // capture current processing time 
            var azFncInitializedTime = DateTime.UtcNow;

            // Get expiration setting in minutes
            int MessageExpirationTimeinMinutes;
            if(!Int32.TryParse(
                        Environment.GetEnvironmentVariable("MessageExpirationTimeinMinutes"),
                        out MessageExpirationTimeinMinutes)) {
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

    }
}
