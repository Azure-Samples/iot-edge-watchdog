using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Devices;
using Google.Protobuf;
using HeartbeatProto;

namespace GWManagementFunctions
{
    public static class EventHubListener
    {
        [FunctionName("EventHubListener")]
        public static async Task Run(
                [EventHubTrigger(
                    "messages/events",
                    Connection = "EventHubIngestConnectionString")]
                EventData[] events,
                [EventHub("dest", Connection="EventHubEndpointConnectionString")]
                IAsyncCollector<string> tsiEventHub,
                DateTime enqueuedTimeUtc,
                ILogger logger)
        {
            var exceptions = new List<Exception>();

            foreach (EventData message in events)
            {
                try
                {
                    // capture current processing time 
                    var azFncInitializedTime = DateTime.UtcNow;

                    // Get expiration setting in minutes
                    int MessageExpirationTimeinMinutes;
                    if(!Int32.TryParse(
                                Environment.GetEnvironmentVariable(
                                    "MessageExpirationTimeinMinutes"),
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
                        .ParseIoTHubMessage(
                                azFncInitializedTime,
                                MessageExpirationTimeinMinutes,
                                logger)
                        .AckDeviceMessage(serviceClientWrapper, logger)
                        .SendStatisticsToTSI(
                                tsiEventHub,
                                logger,
                                enqueuedTimeUtc,
                                azFncInitializedTime);
                    await Task.Yield();
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception
                    // and continue. Also, consider capturing details of the message that failed
                    // processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed
            // processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }

    }
}
