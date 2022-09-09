---
page_type: sample
languages:
- csharp
products:
- azure-functions
- vs-code
---

# Azure IoT Edge Watchdog - .NET/C# *cloud* workspace

This is the README file for the C# *cloud* Visual Studio workspace (*cloud.code-workspace*), part of the C#/.NET Core version of the IoT Edge Watchdog Pattern sample.  It is intended to be built and run with the *SimulatedEdgeDevice* code in the *edge* workspace (*edge.code-workspace*) located in the *edge* folder.  Both workspaces can be opened simultaneously in different instances of Visual Studio Code.

The top level [README.md](../../README.md) in this repository provides an overview of the IoT Edge Watchdog Code sample, including an architecture diagram, along with prerequisites for building and running the sample code.

## Contents

This workspace contains 2 folders:

| File/folder       | Description                               |
|-------------------|-------------------------------------------|
| `IoTHubListener`           | Azure Functions project                   |
| `tests`          | IoTHubListener unit tests |


- *IoTHubListener* - This folder contains an Azure Functions project which builds the Azure Function (*EventHubListener*) shown in the architecture diagram.  The *EventHubListener* serves as an event listener for your IoT Hub that is triggered whenever heartbeat messages from the Azure IoT Edge Module (*SimulatedEdgeDevice*) are sent to the IoT Hub.  When triggered the message is logged, parsed for its creation time, and (if the message is within the expiration time window) the function responds with an acknowledgement (ACK) to the edge device. The Azure Function requires an input Event Hub, such as IoT Hub's Event Hub compatible end point, and an output Event Hub to push data to. The output Event Hub can be mapped to TSI for persistence of watchdog messages. 

> An optional consumer group may be added to the output Event Hub where messages are consumed by a streaming
analytics service, such as [Azure Stream Analytics](https://docs.microsoft.com/en-us/azure/stream-analytics/stream-analytics-introduction). The stream analytics service can provide alerts/alarms/notifications
over a tumbling window, for a specific IoT Hub Device ID, to alert when a device has not sent a ping within a set period.
These empty tumbling window alerts can then allow the cloud-side solution to generate dashboard alerts, or adjust
solution behavior, such as preventing device deployments if the network connect appears unstable.

- *SendStatisticsToTSITests* and *AckDeviceMessageTests* are .NET Core *xUnit.net* unit test project.  The *Unit Testing IoTHubListener* section of this document describes how to incorporate unit tests into the Azure Functions App local development loop and build process..  

## Prerequisites

The prerequisites for this code sample are included in the top level README.md of this repo. 

## Setup

### Configure Azure Functions development environment

After installing prerequisites, there is one additional step to configure your development environment before building and running the sample. The *IoTHubListener* Azure Function requires 4 connection strings to run - *IoTHubEventHubEndpointConnectionString*, *IoTHubAckConnectionString*, *AzureWebJobsStorage* and *EventHubEgressConnectionString*.  

`IoTHubEventHubEndpointConnectionString` The *IoTHubListener* receives heartbeat messages from your Azure IoT Hub service via the [Azure Functions IoT Hub binding](https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-event-iot).  Azure Functions accesses Azure IoT Hub messages at the the Azure IoT Hub's built-in [Event Hub compatible endpoint](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-read-builtin#read-from-the-built-in-endpoint).  *IoTHubEventHubEndpointConnectionString* is the connection string for the Azure IoT Hub's Event Hub compatible endpoint.  It can be found in the Azure Portal: 

1. Sign in to the [Azure portal](https://portal.azure.com/) and navigate to your IoT hub
2. Click *Built-in endpoints* under Hub settings
3. Copy the value of  *Event Hub-compatible endpoint*

`IoTHubAckConnectionString` Corresponding delivery acknowledgments of heartbeat messages will be sent back to the device via the *service* endpoint of your IoT hub. It can be found in the Azure Portal:

1. Sign in to the [Azure portal](https://portal.azure.com/) and navigate to your IoT hub
2. Click *Shared access policies* under Security settings
3. Click the *service* policy name under *Manage shared access policies*
4. Copy the value of the *Primary connection string*

`AzureWebJobsStorage` is a built-in connection string that the Azure Functions runtime uses to access a special storage account which it uses for state management. It is required for all Azure Function types except HTTP triggered functions. When you create an Azure Function App via the the Azure Portal or the Visual Studio Code Azure Functions extension, an Azure Storage account is automatically created for the Function App and the *AzureWebJobsStorage* connection string is set in the [Azure Functions applications settings](https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-blob).  The *AzureWebJobsStorage* must be set manually when running a function locally with the Azure Functions Core Tools using either the Azure Storage Emulator connection string (*UseDevelopmentStorage=true*) or the connection string for the Azure Storage Account created during the prerequisites setup.

`EventHubEgressConnectionString` The acknowledged message events can be mapped to TSI for persistence of watchdog messages. The last connection string, *EventHubEgressConnectionString* is the connection string to where the processed data gets sent and which enables Azure Time Series Insight or another service to access the processed message.

Connection strings and other secrets should not be stored in application code or any file that is checked into source control.   The recommended way to pass connection string and other secrets to an Azure Function is through environment variables.  

Azure Function bindings can implicitly access a connection string via environment variables, as shown in the *IoTHubListener* code below which references the *IoTHubEventHubEndpointConnectionString*, *IoTHubAckConnectionString*, *AzureWebJobsStorage*, *EventHubEgressConnectionString*, and *MessageExpirationTimeinMinutes* (optional)  environment variables:


```csharp
       ...
       [FunctionName("EventHubListener")]
        public static async Task Run(
            [EventHubTrigger("messages/events", Connection = "IoTHubEventHubEndpointConnectionString")]
            EventData[] events,
            [EventHub("dest", Connection = "EventHubEgressConnectionString")] IAsyncCollector<string> tsiEventHub,
            ILogger logger)
        {
        ...
        if (!Int32.TryParse(
                    Environment.GetEnvironmentVariable(
                        "MessageExpirationTimeinMinutes"),
                    out MessageExpirationTimeinMinutes))
        {
            MessageExpirationTimeinMinutes = 5;
        }
        ...
        var iotHubServiceClient = ServiceClient.CreateFromConnectionString(
                Environment.GetEnvironmentVariable("IoTHubAckConnectionString"),
                Microsoft.Azure.Devices.TransportType.Amqp);  
        ...              
```
User code in an Azure Function can also retrieve environment variables explicitly using language/platform specific API's.  C#/.NET functions can use the *Environment.GetEnvironmentVariable* API.

Environment variables are set differently for local development with the Azure Functions Core Tools vs running in the Azure Functions App service runtime.   

When you deploy your function to your Azure Functions App service, environment variables, including connection strings, are configured as [Azure Functions applications settings](https://docs.microsoft.com/en-us/azure/app-service/configure-common?tabs=portal) in the Azure Portal.  Azure Functions application settings are encrypted, stored and managed by the Azure App Service platform, which hosts Azure Functions.  

When running Azure Functions in your local development environment with the Azure Functions Core Tools, environment variables are set in a special development-only settings file, *local.settings.json*.  Since this file contains secrets, it should always be excluded from source control. Therefore, it is included in *.gitignore* in this sample repo.  A template *local.settings.json.temp* is provided as a template, which can be copied and/or renamed to *local.settings.json*.  After renaming, update the *AzureWebJobsStorage* and *EventHubEgressConnectionString* values to either the Emulator connection string or the connection string for the Azure Storage account or TSI event hub you created during the prerequisite step.  Update the *IoTHubEventHubEndpointConnectionString* and *IoTHubAckConnectionString* to values you copied earlier in this section.

> **Note:** The Visual Studio Code Azure Functions extension can optionally publish your *local.settings.json* values to your Azure Function App after you deploy, using the instructions in [Publish application settings](https://docs.microsoft.com/en-us/azure/azure-functions/functions-develop-vs-code#publish-application-settings).   However, be sure not to publish application settings which use the Azure Storage Emulator.  The Azure Storage Emulator setting (*UseDevelopmentStorage=true*) will cause an error when your function executes in your Azure Function App.   Also, you will get a warning that there is already a *AzureWebJobsStorage* setting that was setup as part of the Azure Function App creation.  If you use different Azure Storage account for local development and your Azure Function App, each will maintain their own cursor reading messages from your Azure IoT Hub.

## Running the sample

This section provides instructions for building and running the sample in the Azure Functions local runtime.  The sample can also be pushed to your Azure Function App by following the instructions in the article [Publish the project to Azure](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-first-function-vs-code#publish-the-project-to-azure).

There are several Visual Studio Code tasks created by the Azure Functions extension - *build*, *clean release*, *publish* and *func*. The *func* task will run your functions locally in the Azure Functions Core Tools.  To run these tasks, search for *Tasks: Run Task* from the Visual Studio Code command palette and select the task from the task list.

The easiest way to run your functions is to simply start your functions in the Visual Studio Code debugger.   Select the Debug icon in the Activity Bar on the side of Visual Studio Code.  You should see *Attach to .NET Functions* in the Debug configuration dropdown. Start your debug session by pressing F5 or selecting the play button next to the Configuration dropdown.

You should see Azure Functions local runtime status messages in the Visual Studio Code integrated terminal.  Once the *IoTHubListener* starts reading messages from your Azure IoT Hub, you should see output showing the heartbeat messages.  The messages will contain the module and device ID's of the edge device along the the creation time of when the message was sent.

The *IoTHubListener* also writes decompressed messages to either the Azure Storage Emulator or your Azure Storage account.  The messages are written to output blobs in the format *"test-out/{sys.randguid}.xml"*.  Azure Storage Explorer can be also used to download and view the output blobs.

## Key concepts

### Sharing Complementary Code in C#/.NET

The method for sharing code between an Azure IoT Edge module and an Azure Function project varies according to the code platform and associated options for publishing and importing code. 

.NET projects can leverage external code via direct references to another project or via references to downloaded NuGet packages. The *IoTHubListener* Azure Functions project uses a direct project reference to leverage code in the *HeartbeatMessage* library project, located in the *shared/HeartbeatMessage* folder.  Below is the line from the *IotHubListener.csproj* which references the *HeartbeatMessage.csproj*:

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\shared\HeartbeatMessage\HeartbeatMessage.csproj" />
  </ItemGroup>
```

At build time, the .NET compiler copies the *HeartbeatMessage* library binaries to the binary output folder of the *IoTHubListener*.  

### Unit Testing Complementary Code

As explained in the top level README.md in this repo, the Complementary Code pattern enables unit testing of shared code.  For the C#/.NET Core version of the sample, *SendStatisticsToTSITests* and *AckDeviceMessageTests* *xUnit.net* unit test projects are included in the *tests/IoTHubListenerUnitTests* folder.  This sample shows several ways to run the unit tests in this project, both as part of the inner development loop and build pipeline.

The Visual Studio Code C# language extension recognizes *xUnit.net* projects and enables interactive running debugging of individual unit test methods.

![xunit debug image](../../images/xunit.png)

Unfortunately, the C# language extension only activates on the primary folder in a Visual Studio code workspace.  To get the C# language extension to enable the interactive unit test support, switch the primary folder in Visual Studio code from *cloud* to *shared* by selecting the folder button ![folder icon](../../images/folder.png) on the Visual Studio Code status bar.

There is also a *test* task in the Visual Studio Code *tasks.json* configuration file in the *cloud* folder. This task invokes the *dotnet test* command on the *IoTHubListenerUnitTests* project in the Visual Studio Code interactive terminal.  To run the *test* task, search for *Tasks: Run Task* from the Visual Studio Code command palette and select *test* from the task list.

This sample demonstrates the WatchDog pattern using a heartbeat message, as shown below:

!['Architecture Diagram of IoT Edge and Cloud interaction'](./images/arch.png)

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.