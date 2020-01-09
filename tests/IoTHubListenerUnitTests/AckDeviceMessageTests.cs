using System;
using Xunit;
using Moq;
using Microsoft.Azure.Devices;
using GWManagementFunctions;
using System.Threading.Tasks;
using EdgeHeartbeartMessage;

namespace IoTHubListenerTests
{
    public class AckDeviceMessageTests
    {
        public Task<Heartbeat> GetEventData(string messageType, string deviceId, Int64 msgId) {
            Heartbeat obj = new Heartbeat
                {
                    MsgType = messageType,
                    Name = deviceId,
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

            var directMethodResult = 
                IoTHubListener.AckDeviceMessage(
                    GetEventData("Heartbeat", deviceId, messageId),
                    serviceClientMock.Object,
                    loggerMock.Object);   

            // if this line throws from a bad count check the test will implicitly fail.
            serviceClientMock.Verify(
                x => x.InvokeDeviceMethodAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CloudToDeviceMethod>()),
                Times.Once);
        }
    }
}
