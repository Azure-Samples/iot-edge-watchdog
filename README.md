---
page_type: sample
languages:
- csharp
products:
- azure-iot
- azure-iot-edge
- azure-iot-hub
- azure-functions
- azure-time-series-insights
- azure-event-hubs
- vs-code
description:
Azure IoT Edge watchdog pattern which uses Azure Functions to send
the watchdog heartbeat response message from the cloud.
---

# Azure IoT Edge Watchdog

This repo introduces the Azure IoT Edge Watchdog pattern which is split into (1) edge and (2) cloud code joined together
by a (3) common message structure.  This pattern demonstrates a roundtrip verification of a heartbeat message sent from
an IoT Edge device to an Azure cloud function.  If the edge device does not receive an acknowledgement of its message,
or if the message is out of the accepted time boundary, the watchdog code will, by default, expand the window of time 
in which it accepts an acknowledgement.  Within the Edge code, there is a stubbed method for what additional behavior 
should happen if the Edge device does not receive an acknowledgement.

The **Edge Watchdog** provides a solution for monitoring and responding to network partitions in IoT systems that leverage
an Azure IoT Edge gateway.  This project has 3 primary components:
- **Share Heartbeat Message Object**: Shared object model (protobuf) between cloud and edge, to ease serialization across
applications. This project can be modified to produce a Nuget package that can be consumed as a package reference,
rather than as a linked/dependent project. 
- **Edge Device Module**: Deploy this module on an Azure IoT Edge device and it will send messages to the corresponding
IoT Hub and listen for a ACK.  If the IoT Hub fails to respond in a user-defined window, the module will enter a state
where it consideres itself disconnected.  The next time the module sends a message and receives a response in time, the
device will move back into online mode. This functionality can be leveraged to have the edge application(s) dynamicall
adapt to offline operation. This component includes an example Azure DevOps `build.yaml` file that can be leveraged for
build and release pipelines. 
- **IoT Hub Listener**: This is an Event Hub triggered Azure Function, where the source Event Hub corresponds to an
Event Hub compatible endpoint for an IoT Hub. Deploy this function to Azure and it will pick up the messages from the
Azure IoT Edge Module as they enter the IoT Hub, log them, process them, and respond (ACK) to the device.  This component
includes an example Azure DevOps `build.yaml` file that can be leveraged for build and release pipelines.

## Quickstart

To run this project, you will need the following Azure resources:
- [Azure IoT Hub](https://azure.microsoft.com/en-us/services/iot-hub/)
- [Azure Event Hubs](https://azure.microsoft.com/en-us/services/event-hubs/)
- [Azure IoT Edge](https://azure.microsoft.com/en-us/services/iot-edge/)
- [Azure Container Registry](https://azure.microsoft.com/en-us/services/container-registry/) or other container registry
- [Azure Time Series Insights](https://azure.microsoft.com/en-us/services/time-series-insights/)

### Azure IoT Edge Module (Simulated Edge Device)

This module has configurable variables set in the Dockerfile. Overwrite these values in the Dockerfile, if desired:
- DEVICE_NAME: name of this device; this should be unique within the IoT Hub.
- START_WINDOW_IN_SECONDS: if the device sends a message to IoT Hub and receives a message before START_WINDOW_IN_SECONDS
seconds, it will consider itself in error.  Set this field to "0" to ignore.
- END_WINDOW_IN_SECONDS: if the device sends a message to IoT Hub and does not receive a response within
END_WINDOW_IN_SECONDS seconds, it will consider itself offline.
- BEAT_FREQUENCY_IN_SECONDS: the device will send messages every BEAT_FREQUENCY_IN_SECONDS seconds.

### Iot Hub Listener

The **IoT Hub Listener** is an Azure Function designed to be intermediate plumbing between an Azure IoT Hub and a
time-series database, such as Azure Time Series Insights (see
[this article](https://docs.microsoft.com/en-us/azure/time-series-insights/time-series-insights-how-to-add-an-event-source-eventhub)
for details). The Azure Function requires an input Event Hub, such as IoT Hub's Event Hub compatible end point, and an
output Event Hub to push data to. The output Event Hub can be mapped to TSI for persistence of watchdog messages. 

An optional consumer group may be added to the output Event Hub where messages are consumed by a streaming
analytics service, such as Azure Stream Analytics. The stream analytics service can provide alerts/alarms/notifications
over a tumbling window, for a specific IoT Hub Device ID, to alert when a device has not sent a ping within a set period.
These empty tumbling window alerts can then allow the cloud-side solution to generate dashboard alerts, or adjust
solution behavior, such as preventing device deployments if the network connect appears unstable.  
