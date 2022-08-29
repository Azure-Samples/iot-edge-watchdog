using System;
using System.Collections;
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
		/// <summary>
		/// This function is executed in response to the 
		/// <see cref="EventHubTriggerAttribute">Event Hub Trigger</see>
		/// specified in the parameters. It takes the events from the trigger,
		/// sends messages to TSI, and sends an ACK back to the IoT Hub.
		/// </summary>
		/// <param name="events">
		/// The inbound events from the Event Hub Trigger
		/// </param>
		/// <param name="tsiEventHub">
		/// A destination binding for an EventHub connected to TSI
		/// </param>
		/// <param name="logger">The logger for the function.</param>
		/// <returns></returns>
		[FunctionName("EventHubListener")]
		public static async Task Run(
				[EventHubTrigger(
					"messages/events",
					Connection = "IoTHubEventHubEndpointConnectionString")]
				EventData[] events,
				[EventHub("dest", Connection="EventHubEgressConnectionString")]
				IAsyncCollector<string> tsiEventHub,
				ILogger logger)
		{
			var exceptions = new List<Exception>();
			logger.LogInformation("Received {0} message event(s) from hub", events.Count());
			foreach (EventData message in events)
			{
				try
				{
					// capture current processing time 
					var azFncInitializedTime = DateTime.UtcNow;

					// Get expiration setting in minutes
					int MessageExpirationTimeinMinutes;
					if (!Int32.TryParse(
								Environment.GetEnvironmentVariable(
									"MessageExpirationTimeinMinutes"),
								out MessageExpirationTimeinMinutes))
					{
						MessageExpirationTimeinMinutes = 5;
					}

					// initialize IoT Hub Service CLient, this uses an adapter to wrap the 
					// service client code for testability
					// See the IoT Hub Endpoint Documentation for more information
					// (https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-endpoints)
					var iotHubServiceClient = ServiceClient.CreateFromConnectionString(
							Environment.GetEnvironmentVariable("IoTHubAckConnectionString"),
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
								message.SystemProperties.EnqueuedTimeUtc,
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
