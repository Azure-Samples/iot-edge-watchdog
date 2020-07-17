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
---

# Azure IoT Edge Watchdog

This repo introduces the Azure IoT Edge Watchdog pattern which is split into (1) edge and (2)
cloud code joined together by a (3) common message structure.  This pattern demonstrates a
roundtrip verification of a heartbeat message sent from an IoT Edge device to an Azure cloud
function.  If the edge device does not receive an acknowledgement of its message, or if the
message is out of the accepted time boundary, the watchdog code will, by default, expand the
window of time in which it accepts an acknowledgement.  Within the Edge code, there is a stubbed
method for what additional behavior should happen if the Edge device does not receive an
acknowledgement.

## Contents

| File/folder | Description |
|-|-|
| `Edge` | Contains Azure IoT Edge Watchdog module which sends Heartbeat message |
| `Cloud` | Contains the Azure Function code which responds to Heartbeat message |
| `SharedCode` | Contains Protobuf definition for Heartbeat message |
| `.gitignore`      | Define what to ignore at commit time. |
| `CODE_OF_CONDUCT.md` | Microsoft Open Source Code of Conduct and FAQ |
| `CONTRIBUTING.MD` | Guidelines for contributing to the Sample on Github |
| `ISSUE_TEMPLATE.md`  | Template for submitting issues on GitHub |
| `LICENSE`         | The license for the sample |
| `README.md`       | This README file |
| `SECURITY.md`     | Instructions for filing security issues directly with Microsoft |

The **Edge Watchdog** provides a solution for monitoring and responding to network partitions in
IoT systems that leverage an Azure IoT Edge gateway.  This project has 3 primary components:

- **Edge/SimulatedDevice/Module**: Deploy this module on an Azure IoT Edge device and it will
send messages to the corresponding IoT Hub and listen for a ACK.  If the IoT Hub fails to respond
in a user-defined window, the module will enter a state where it consideres itself disconnected. 
The next time the module sends a message and receives a response in time, the device will move
back into online mode. This functionality can be leveraged to have the edge application(s)
dynamically adapt to offline operation. This component includes an example Azure DevOps
`build.yaml` file that can be leveraged for build and release pipelines. 

- **Cloud/IoTHubListener**: This is an Event Hub triggered Azure Function, where the source Event
Hub corresponds to an Event Hub compatible endpoint for an IoT Hub. Deploy this function to Azure
and it will pick up the messages from the Azure IoT Edge Module as they enter the IoT Hub, log
them, process them, and respond (ACK) to the device.  This component includes an example Azure
DevOps `build.yaml` file that can be leveraged for build and release pipelines.

- **SharedCode/Heartbeat/Message**: Shared object model (protobuf) between cloud and edge, to
ease serialization across applications. This project can be modified to produce a Nuget package
that can be consumed as a package reference, rather than as a linked/dependent project. 

## Deployment
The deployment.debug.template.json and deployment.template.json are currently setup to build and push with Visual Studio Code tooling. 

To build and push the Watchdog solution using Visual Studio, you will need to modify the deployment files to reference the module image in the way the project structure is defined in Visual Studio.
```
            "settings": {
              "image": "${MODULEDIR<./modules/Watchdog>}",
            }
```

## Quickstart

1. Language SDK

- [.NET Core SDK (3.1 or above)](https://www.microsoft.com/net/download)
- [Node.js (8.5 or above)](https://nodejs.org) - required for local development of the Azure
Functions Core Tools.

2. Docker

[Docker Community Edition](https://docs.docker.com/install/) - required for Azure IoT Edge
module development, deployment and debugging. Docker CE is free, but may require registration
with Docker account to download.  Docker on Windows requires Hyper-V support.  Please make sure
your Windows version supports Hyper-V.  For Windows 10, Hyper-V is available with the Pro or
Enterprise versions.

3. Azure Resources

To run this project, you will need the following Azure resources:
- [Azure IoT Hub](https://azure.microsoft.com/en-us/services/iot-hub/)
- [Azure Event Hubs](https://azure.microsoft.com/en-us/services/event-hubs/)
- [Azure IoT Edge](https://azure.microsoft.com/en-us/services/iot-edge/)
- [Azure Container Registry](https://azure.microsoft.com/en-us/services/container-registry/) or
other container registry
- [Azure Time Series Insights](https://azure.microsoft.com/en-us/services/time-series-insights/)

4. IDE and extensions
- Visual Studio Code
    
    > **Note**: Extensions can be installed either via links to the Visual Studio Code Marketplace
    below or by searching extensions by name in the Marketplace from the Extensions tab in Visual
    Studio Code.

    Install [Visual Studio Code](https://code.visualstudio.com/) first and then add the following
    extensions:

    - [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp) (only
    required for C# version of sample) - provides C# syntax checking, build and debug support
    - [Azure IoT Tools](https://marketplace.visualstudio.com/items?itemName=vsciot-vscode.azure-iot-tools) - provides Azure IoT Edge development tooling


    > **Note**: Azure IoT Tools is an extension pack that installs 3 extensions that will show up in the Extensions pane in Visual Studio Code - *Azure IoT Hub Toolkit*, *Azure IoT Edge* and *Azure IoT Workbench*.

    - [Azure Functions](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azurefunctions)

- Visual Studio
    > **Note**: Extensions can be installed either via links to the Visual Studio Marketplace
    below or by searching for the extension by name from the Extensions menu in Visual Studio.
    - Install [Visual Studio](https://docs.microsoft.com/en-us/visualstudio/install/install-visual-studio?view=vs-2019) first and then add the following extensions:
    - [Azure IoT Edge Tools](https://marketplace.visualstudio.com/items?itemName=vsc-iot.vs16iotedgetools) - provides Azure IoT Edge development tooling for Visual Studio
    - [Azure Function](https://marketplace.visualstudio.com/items?itemName=VisualStudioWebandAzureTools.AzureFunctionsandWebJobsTools) - skip if using Visual Studio 2019 


5. Azure IoT EdgeHub Dev Tool

 [Azure IoT EdgeHub Dev Tool](https://pypi.org/project/iotedgehubdev/) is a version of the Azure
 IoT Edge runtime for local development machine.  After verifying Python and Pip (2.7/3.6 or
 above) are installed and in the path, install **[iotedgehubdev](https://pypi.org/project/iotedgehubdev/)** with Pip:
    ```bash
    pip install --upgrade iotedgehubdev
    ```

6. Azure Functions Core Tools

 [Azure Functions Core Tools](https://github.com/Microsoft/vscode-azurefunctions/blob/master/README.md) is a version of the Azure Functions runtime for local development machine. It also provides commands to create functions, connect to Azure, and deploy Azure Function projects.  After verifying Node.js (8.5 or above) is installed and in the path, install **[azure-functions-core-tools](https://www.npmjs.com/package/azure-functions-core-tools)** with
    npm:

    ```bash
    npm install -g azure-functions-core-tools
    ```

7. Azure Storage Emulator (optional, Windows only)

 [AzureStorageEmulator](https://docs.microsoft.com/en-us/azure/storage/commonstorage-use-emulator)  provides a local environment that emulates the AzureBlob, Queue, and Table services for development purposes.  Use the link for thestandalone installer.

 Azure Blob Storage is required for the Azure Functions runtime for internalstate management.  The Azure Function in this sample also writes decompressedmessages to an Azure Storage account. 

 When running this sample locally on **Windows**, the Azure Storage Emulator can be used instead of creating an Azure Storage account.  The emulator will not work with **WSL** or **Linux** and a real storage account will be needed.

### Azure IoT Edge Module (Simulated Edge Device)

In the provided `env` file (remove the `.temp` extension) there are many configurable
variables.  At a minimum, you will need to fill in the container registry settings.
If you are using `localhost` are your registry, then you can leave username and
password blank.

This Watchdog module also has configurable variables set in the Dockerfile.
The default value embedded in the code is in parantheses () below.
- START_WINDOW_IN_SECONDS (1): if the device sends a message to IoT Hub and receives a message before START_WINDOW_IN_SECONDS
seconds, it will consider itself in error.  Set this field to "0" to ignore.
- END_WINDOW_IN_SECONDS (5): if the device sends a message to IoT Hub and does not receive a response within
END_WINDOW_IN_SECONDS seconds, it will consider itself offline.
- HEARTBEAT_FREQUENCY_IN_SECONDS (10): the device will send messages every HEARTBEAT_FREQUENCY_IN_SECONDS seconds.

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


### Environment settings and use/development

There are four settings to configure for the IoT HuB listener.  These are in the
`local.settings.json.temp` file.  After changing the settings, remove the `.temp`
extension.

First set the IoT Hub Connection string, then the IoT Hub Event Hub Endpoint Connection String.  This second string is what triggers the IoT Hub Listener and
where it pulls the message data from. The Event Hub Endpoint is where the processed data gets sent and which enables Azure Time Series Insight or another service 
to access the processed message.  Finally, AzureWebJobsStorage is a required backing
store for Azure Functions.  This can be a local Azure Storage Emulator with the
setting `UseDevelopmentStorage=true`.

`IoTHubEventHubEndpointConnectionString`
`IoTHubConnectionString`
`EventHubEndpointConnectionString`
`AzureWebJobsStorage`

### HeartMessage

The heartbeat message is [protocol buffer](https://developers.google.com/protocol-buffers/).  Simply define it in the `.proto` 
file and include the `csproj` file.  The underlying C# will be generated for you.


