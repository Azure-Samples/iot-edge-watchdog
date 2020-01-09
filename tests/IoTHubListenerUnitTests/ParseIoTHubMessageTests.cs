using System;
using Xunit;
using Moq;
using Microsoft.Azure.Devices;
using GWManagementFunctions;
using Microsoft.Azure.WebJobs;
using System.Threading.Tasks;
using System.Threading;
using EdgeHeartbeartMessage;

namespace IoTHubListenerTests
{
    public class ParseIoTHubMessageTests
    {
        [Fact]
        public void TestSendStatisticsToTSI()
        {
            Assert.True(true);
        }
    }

}