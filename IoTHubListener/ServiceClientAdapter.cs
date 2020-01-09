using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Logging;

namespace GWManagementFunctions
{
    public interface IIoTHubServiceClient{
        Task<CloudToDeviceMethodResult> InvokeDeviceMethodAsync(
            string str, 
            string str2, 
            CloudToDeviceMethod method);
    }

    public class IoTHubServiceClient : IIoTHubServiceClient
    {

        private ServiceClient ServiceClient {get; set;}
        private ILogger Logger {get; set;}

        public IoTHubServiceClient(ServiceClient serviceClient, ILogger logger){
            ServiceClient = serviceClient;
            Logger = logger;
        }

        public async Task<CloudToDeviceMethodResult> InvokeDeviceMethodAsync(
            string deviceId, 
            string moduleId, 
            CloudToDeviceMethod method)
        {
            this.Logger.LogTrace($"Calling into IoT Hub Service client with direct method invocation on device:{deviceId}, module:{moduleId}");
            return await this.ServiceClient.InvokeDeviceMethodAsync(deviceId, moduleId, method);
        }
    }
}