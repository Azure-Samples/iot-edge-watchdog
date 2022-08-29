---
page_type: sample
languages:
- csharp
products:
- azure-iot-hub
- azure-iot-edge
- vs-code
---

# Watchdog pattern sample - .NET/C# *edge* workspace

This is the README file for the C# *edge* Visual Studio workspace (*edge.code-workspace*),  part of the C#/.NET Core version of the IoT Edge Watchdog Pattern sample.  It is intended to be built and run with the companion code in the *cloud* workspace (*cloud.code-workspace*) located in the same folder.  Both workspaces can be open simultaneously in different instances of Visual Studio Code.

------
The top level [README.md](../../README.md) in this repository provides an overview of the IoT Edge Watchdog sample, including an architecture diagram, along with prerequisites for building and running the sample code.

This sample assumes basic familiarity with Azure IoT Edge Modules and how to build them with Visual Studio Code.  For an introduction to building IoT Edge Modules in C#, refer to 
[Tutorial: Develop a C# IoT Edge module for Linux devices](https://docs.microsoft.com/en-us/azure/iot-edge/tutorial-csharp-module).


## Contents

This workspace contains 2 folders:

| File/folder       | Description                                |
|-------------------|--------------------------------------------|
| `SimulatedEdgeDevice`            | Azure IoT Edge solution                    |
| `tests`          | unit tests  |

- SimulatedEdgeDevice - This folder contains an Azure IoT Edge solution which consists of the a Watchdog IoT Edge modules shown in the architecture diagram - the heartbeat simulator module (*WatchdogModule*).  The *WatchdogModule* generates heartbeat messages to the corresponding IoT Hub and listens for an acknowledge.  If the IoT Hub fails to respond in a user-defined window, the module will enter a state where it consideres itself disconnected. 
The next time the module sends a message and receives a response in time, the device will move back into online mode.

    The *WatchdoModule* is designed to illustrate the IoT Edge Watchdog pattern, It shares logic between Edge components and the Cloud.  The *WatchdogModule* project file references the *HeartbeatMessage* .NET library, located in the *shared/HearbeatMessage* folder. 


- shared - This folder contains a .NET library project which are use in both the *edge* and *cloud* workspaces - *HeartbeatMessage*.  

    *HeartbeatMessage* is a .NET Standard library project, used in both the Azure IoT Edge solution in the *edge* workspace and the Azure Functions project in *cloud* workspace.  The *HeartbeatMessage* library itself is very simple and is intended for demonstration of the Watchdog pattern.  It leverages Protocol Buffers, a free and open-source cross-platform data format used to serialize structured data.. 

    *EdgeModuleTests* is a .NET Core *xUnit.net* unit test project.  The *Unit Testing Watchdog Code* section of this document describes how to incorporate unit tests into the Azure IoT Edge Module local development loop and build process.

## Prerequisites

The prerequisites for this code sample are included in the top level README.md of this repo. 

## Running the sample

This section provides instructions for building and running the sample in the SimulatorEdgeDevice folder, and optionally attaching the Visual Studio Code debugger to the running modules.  The sample can also be pushed to your container registry and deployed to an actual Edge device, by following the instructions in the [Tutorial: Develop a C# IoT Edge module for Linux devices](https://docs.microsoft.com/en-us/azure/iot-edge/tutorial-csharp-module).

1. Build and Run the solution

   With the Explorer icon in the Visual Studio Code Activity Bar selected, select the *deployment.debug.template.json* file in the Explorer pane and right click to display the context menu. Select *Build and Run IoT Solution in Simulator*.

   This command issues *docker build* command for each module in the selected deployment manifest, and then runs the deployment in the Azure IoT Simulator in solution code by issuing an *iotedgehubdev start* command.  These commands run in the Visual Studio Code integrated terminal.

   The Azure IoT Edge Simulator status messages and any console messages from individual heartbeat messages are also shown in the Visual Studio integrated terminal.  Once the *WatchdogModule* has been initialized and started, you should see output indicating that heartbeat messages are being sent to your Azure IoT Hub service:

   ```
    [IoTHubMonitor] [5:21:57 PM] Message received from [<deviceID>/Watchdog]:
    {
    "MsgType": "Heartbeat",
    "DeviceId": "<deviceID>",
    "ModuleId": "Watchdog",
    "Id": "1",
    "HeartbeatCreatedTicksUtc": "637973293173533416"
    }
    [IoTHubMonitor] [5:22:46 PM] Message received from [<deviceID>/Watchdog]:
    {
    "MsgType": "Heartbeat",
    "DeviceId": "<deviceID>",
    "ModuleId": "Watchdog",
    "Id": "2",
    "HeartbeatCreatedTicksUtc": "637973293663767165"
    } 
   ```

   To stop the Azure IoT Edge Simulator after debugging, search for *Azure IoT Edge: Stop IoT Simulator* in the Visual Studio command palette, or simple press Ctrl+C in the Visual Studio Code integrated terminal. 

3. Attach debugger to module (optional)

    To bring up the Debug view, select the Debug icon in the Activity Bar on the side of Visual Studio Code.  In order to start a debug session, first select the configuration for your target module using the Configuration drop-down in the Debug view. The following configurations are provided for debugging the *WatchdogModule* with the Azure IoT Edge Simulator running in solution mode:

    - *WatchdogModule Remote Debug (.NET Core)*
    
    Once you have your launch configuration set, start your debug session by pressing F5 or selecting the play button next to the Configuration drop-down in the Debug view.
    
    Once the *vsdbg* debugger has connected to the selected Azure IoT Edge Module container, execution should stop if any breakpoints were previously selected.
    
    >**Note:** It is not necessary to enable the debugger wait code in order to attach the Visual Studio Code debugger to a debug container module.

## Key concepts

### Understanding Azure IoT Edge solutions

Azure IoT Edge Solutions in Visual Studio Code are organized by a root folder containing the [Azure IoT Edge Deployment manifests](https://docs.microsoft.com/en-us/azure/iot-edge/module-composition) (*deployment.template.json* and *deployment.debug.template.json*) and a *modules* subfolder, which contains a folder for each module built by the solution.  

Each module folder contains a module metadata file (*module.json*), a Dockerfile for each support platform + debug/release combination,  and the language specific code for building the module (C#, Node.js, Python, etc.).  C#/.NET Core Azure IoT Edge modules are built as .NET Core console applications, so each module folder also contains a .NET Core project file.  

The Azure IoT Edge Visual Studio Code extension recognizes this Azure IoT Edge solution folder structure and activates when the root folder is opened in Visual Studio code or as a workspace folder in Visual Studio Code.  

An [Azure IoT Edge deployment manifest](https://docs.microsoft.com/en-us/azure/iot-edge/module-composition) is JSON document which describes:

- The IoT Edge agent module twin, which includes three components.
  - The container image for each module that runs on the device.
  - The credentials to access private container registries that contain module images.
  - Instructions for how each module should be created and managed.
- The IoT Edge hub module twin, which includes how messages flow between modules and eventually to IoT Hub.
- Optionally, the desired properties of any additional module twins.

The structure of the deployment manifest is shown below:

```json
{
    "modulesContent": {
        "$edgeAgent": { // required
            "properties.desired": {
                // desired properties of the Edge agent
                // includes the image URIs of all modules
                // includes container registry credentials
            }
        },
        "$edgeHub": { //required
            "properties.desired": {
                // desired properties of the Edge hub
                // includes the routing information between modules, and to IoT Hub
            }
        },
        "module1": {  // optional
            "properties.desired": {
                // desired properties of module1
            }
        },
        "module2": {  // optional
            "properties.desired": {
                // desired properties of module2
            }
        },
        ...
    }
}
```

Each user module included in the deployment is listed under the *$edgeAgent.[properties.desired].modules* key in the deployment manifest.  This information provides information to the IoT Edge agent as to how to start, monitor and configure each module.

> **Note:** The deployment manifest also provides the same information for IoT Edge system modules (*edgeAgent* and *edgeHub*), but the configuration for the system modules is under the *$edgeAgent.[properties.desired].systemModules* key.

Below is the section of *deployment.debug.template.json* for the *WatchdogModule*:

```json
    ...
    "modules": {
        "$IOTEDGE_MODULEID": {
            "version": "1.0",
            "type": "docker",
            "status": "running",
            "restartPolicy": "always",
            "settings": {
                "image": "${MODULES.Watchdog}",
                "createOptions": {
                    "Env": [
                    "START_WINDOW_IN_SECONDS=$START_WINDOW_IN_SECONDS",
                    "END_WINDOW_IN_SECONDS=$END_WINDOW_IN_SECONDS",
                    "HEARTBEAT_FREQUENCY_IN_SECONDS=$HEARTBEAT_FREQUENCY_IN_SECONDS",
                    "IOTEDGE_DEVICEID=$IOTEDGE_DEVICEID",
                    "IOTEDGE_MODULEID=$IOTEDGE_MODULEID"
                    ]                    
                }
            }
        },
        ...
```

The *image* key under the module *settings* is a special placeholder in the format shown in the *WatchModule* sample above.  The Azure IoT Edge extension uses this special placeholders to determine which module containers need to be built locally in the solution.  

The *${MODULES.WatchdogModule.debug}* placeholder indicates that the Azure IoT Edge extension should look for a *WatchModule* subfolder in the *modules* folder of the solution.  It then loads the module metadata file (*module.json*) in the *WatchModule* subfolder to determine which Dockerfile to build.  Below is the module metadata file for the *WatchdogModule*:

```json
{
    "$schema-version": "0.0.1",
    "description": "",
    "image": {
        "repository": "$CONTAINER_REGISTRY_ADDRESS/watchdog",
        "tag": {
            "version": "0.0.1",
            "platforms": {
                "arm32v7": "./Dockerfile.arm32v7",
                "amd64": "./Dockerfile.amd64",
                "amd64.debug": "./Dockerfile.amd64.debug"   
            }
        },
        "buildOptions": [],
        "contextPath": "../../../../"
    },
    "language": "csharp"
}
```

The Azure IoT Edge extension relies on the currently selected target platform to choose a key under *platforms*.  For this sample, the *amd64* (Linux) and *arm32v7* (Raspberry Pi) targets are supported in *WatchdogModule* module metadata file, however many modules support multiple target platforms.  The *.debug*  suffix on the placeholder in the deployment template (*deployment.debug.template.json*) indicates that the Azure IoT Edge extension should use the Dockerfile listed under the *amd64.debug* key.

The *module.json* file also contains keys that are used to construct the *docker build* command along with the Dockerfile name.  The *contextPath* key is used to set the Docker build context, which is explained more in the next section of this document.  The *buildOptions* key can be used to passed additional parameters to *docker build*. 

> Note:  The Azure IoT Edge Simulator uses Docker Compose when running in solution mode.  The *createOptions* key under the module settings in the deployment manifest can be used to pass [*docker create* options](https://docs.docker.com/engine/api/v1.30/#operation/ContainerCreate), such as exposed ports, volume mounts, host configuration, etc., to the Azure IoT Edge Simulator for use in container creation.

Azure IoT Edge extension creates two deployments manifests, *deployment.template.json* and *deployment.debug.template.json*, when scaffolding a new Azure IoT Edge solution.  The *deployment.debug.template.json* version allows for the creation of separate Docker images for debugging.  C#/.NET Core debug Dockerfiles install the *vsdbg* cross-platform .NET debugger and build debug versions of the module .NET Core console application.  The *WatchdogModule* debug Dockerfile in this sample also includes a step to run unit test code in the container.

Azure IoT Edge routes are also defined in the deployment manifests, under the *$edgeHub* desired properties.  Below are the routes defined for this sample:
```json
    "$edgeHub": {
      "properties.desired": {
        "schemaVersion": "1.0",
        "routes": {
            "HeartbeatToIoTHub": "FROM /messages/modules/$IOTEDGE_MODULEID/outputs/sendHeartbeat INTO $upstream"
        },
        "storeAndForwardConfiguration": {
          "timeToLiveSecs": 7200
        }
      }
    }
```
The *HeartbeatToIoTHub* route instructs the Azure IoT Edge Hub to send all output messages to the parent Azure IoT Hub.  

### Sharing HeartbeatMessage Code in C#/.NET

The method for sharing Heartbeat message code between an Azure IoT Edge module and an Azure Function varies according to the code platform and associated options for publishing and importing code. 

.NET projects can leverage external code via direct references to another project or via references to downloaded NuGet packages. The *WatchdogModule* uses a direct project reference to leverage code in the *HeartbeatMessage* library project, located in the *shared/HeartbeatMessage* folder.  Below is the line from the *Watchdog.csproj* which references the *HeartbeatMessage.csproj*:

```xml
 <ItemGroup>
    <ProjectReference Include="..\..\..\..\shared\HeartbeatMessage\HeartbeatMessage.csproj" />
  </ItemGroup>
```
At build time, the .NET compiler copies the *HeartbeatMessage* library binaries to the binary output folder of the *WatchdogModule*.  However, the .NET compiler must have access to the *HeartbeatMessage* library folder.  When building on the local file system, this isn't a problem. However, Azure IoT Edge modules are not built on the local file system.  They are built in a Docker container, using a Dockerfile.  By default, the *docker build* command issued by the Azure IoT Edge extension passes the module's folder as the PATH (root context) argument. For example, the default Docker context for the *WatchdogModule* is the *WatchdogModule* folder.  Docker can't access files outside of its root context, so, by default, .NET compiler would be unable to resolve the *Heartbeat* library reference when building the *WatchdogModule* Docker container.  

The solution is to raise the docker context so that it has access to folders above the individual module folders.  The module metadata file (*module.json*) supports an optional Docker context setting, *contextPath*, which is passed as the PATH argument to to the *docker build* command to set the context. Below is the line from the *module.json* file which raised the context.

```json
        "contextPath": "../../../../"
```
This setting is used in the *WatchdogModule* to raise the Docker context to the *csharp* root folder of the repo.  This allows the *WatchdogModule* Dockerfile to access the *HeartbeatMessage* library.

### Unit Testing Watchdog Code

As explained in the top level README.md in this repo, the Watchdog pattern enables unit testing of shared code.  For the C#/.NET Core version of the sample, an *EnvArgTwinParserTests* *xUnit.net* unit test project is included in the *shared/EdgeModuleTests* folder.  This sample shows several ways to run the unit tests in this project, both as part of the inner development loop and build pipeline.

The Visual Studio Code C# language extension recognizes *xUnit.net* projects and enables interactive running debugging of individual unit test methods.

![xunit debug image](../../images/xunit.png)

Unfortunately, the C# language extension only activates on the primary folder in a Visual Studio code workspace.  To get the C# language extension to enable the interactive unit test support, switch the primary folder in Visual Studio code from *edge* to *shared* by selecting the folder button ![folder icon](../../images/folder.png)on the Visual Studio Code status bar.

There is also a *test* task in the Visual Studio Code *tasks.json* configuration file in the *edge* folder.  This task invokes the *dotnet test* command on the *EdgeModuleTests* project in the Visual Studio Code interactive terminal.  To run the *test* task, search for *Tasks: Run Task* from the Visual Studio Code command palette and select *test* from the task list.

Finally, the debug version of the *WatchdogModule* Dockerfile (*Dockerfile.amd64.debug*) is setup to build and run the unit tests in *EdgeModuleTests* as part of the Dockerfile build.  This demonstrates how to incorporate unit tests into the Azure IoT Edge module build pipeline, and ensure all relevant unit tests succeed before the Edge module container can be built.

### Debugging Azure IoT Edge Modules in Visual Studio Code

While C#/.NET Core Azure IoT Edge modules are built as .NET Core console applications, they cannot be run or debugged directly because they must instantiate a *ModuleClient* object to start and operate as an Azure IoT Edge module and route messages.  In order to instantiate and use a *ModuleClient* object, the module must be able to connect to either the Azure IoT Edge runtime or the Azure IoT Edge Simulator.

While Visual Studio Code can be used to connect to and debug a module running on the real device under the Azure IoT Edge runtime using Docker *ssh* tunneling, this sample only covers debugging with the Azure IoT Edge Simulator.

The Azure IoT Edge Simulator actually supports two modes for running and debugging modules - single module mode and solution mode.  The single module mode allows a module to be run and debugged as an ordinary .NET Core application outside of a Docker container.  While this simplifies the inner development loop, single module mode only supports limited module functionality.  Neither Module Twins or Direct Methods are supported in single module mode, and each message must be manually passed to the module via a special HTTP interface.  And, as the name implies, only single module can be run at a time, so testing module interactions is not possible. A single module debug configuration named "MessageSimulatorModule Local Debug (.NET Core)" is, however, included in the *launch.json* Visual Studio Code configuration file. To learn more about Azure IoT Edge Simulator debugging in single module mode, refer to [Debug a module without a container (C#, Node.js, Java)](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-vs-code-develop-module#set-up-iot-edge-simulator-for-single-module-app)

This sample is designed to show device-to-cloud message flow and cloud-to-device message flow from an Azure IoT Edge module to an Azure Function, so it is intended to be run and debugged under the Azure IoT Edge Simulator in solution mode.  When running in solution mode, the Azure IoT Edge Simulator uses Docker Compose to deploy all modules in the deployment manifest to the local Docker server.  

If the modules are built with debug Dockerfiles, which include the debug versions of the .NET module code and the *vsdbg* .NET Core cross-platform debugger, Visual Studio Code can then connect to the debugger in the running module container.    

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
