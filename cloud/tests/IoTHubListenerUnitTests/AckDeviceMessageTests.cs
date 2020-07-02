using System;
using Xunit;
using Moq;
using Microsoft.Azure.Devices;
using GWManagementFunctions;
using System.Threading.Tasks;
using HeartbeatProto;

namespace IoTHubListenerTests
{
    public class AckDeviceMessageTests
    {
        public Task<HeartbeatMessage> GetEventData(string messageType, string deviceId, string moduleId, Int64 msgId) {
            HeartbeatMessage obj = new HeartbeatMessage
                {
                    MsgType = messageType,
                    DeviceId = deviceId,
                    ModuleId = moduleId,
                    Id = msgId,
                    HeartbeatCreatedTicksUtc = DateTime.UtcNow.Ticks
                };
            return Task.FromResult(obj);
        }

        public Mock<IIoTHubServiceClient> GetIoTServiceClientMock() {
            var serviceClientMock = new Mock<IIoTHubServiceClient>(); 
            var result = new CloudToDeviceMethodResult();
            result.Status = 200;
            serviceClientMock.Setup(
                    foo => foo.InvokeDeviceMethodAsync(
                        It.IsAny<string>(), 
                        It.IsAny<string>(), 
                        It.IsAny<CloudToDeviceMethod>())
            ).Returns(Task.FromResult(result));

            return serviceClientMock;
        }

        [Fact]
        public void TestMessageAckDirectMethodResponse()
        {
            var serviceClientMock = GetIoTServiceClientMock();
            var loggerMock = LoggerUtils.LoggerMock<AckDeviceMessageTests>();

            var messageId = (Int64)1000;
            var deviceId = "deviceId";
            var moduleId = "moduleId";
            var data = GetEventData("Heartbeat", deviceId, moduleId, messageId);
            var directMethodResult = 
                data.AckDeviceMessage(
                    serviceClientMock.Object,
                    loggerMock.Object);   

            // if this line throws from a bad count check the test will implicitly fail.
            serviceClientMock.Verify(
                x => x.InvokeDeviceMethodAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CloudToDeviceMethod>()),
                Times.Once);
        }
    }
}
