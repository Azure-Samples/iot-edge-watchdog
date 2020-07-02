using System;

namespace GWManagementFunctions
{
    public class EdgeHeartbeatLatencyRecord
    {
        public string DeviceId { get; private set; }
        public string ModuleId { get; private set; }
        public Int64 MessageId { get; private set; }
        public Int64 EdgeCreatedTimeTicks { get; private set; }
        public Int64 IoTHubEnqueuedTimeTicks { get; private set; }
        public Int64 AzFncInitializedTimeTicks { get; private set; }
        public Int64 EdgeToHubLatencyMs { get; private set; }
        public Int64 EdgeToAzFncLatencyMs { get; private set; }
        public EdgeHeartbeatLatencyRecord(
                string deviceId,
                string moduleId,
                Int64 msgId,
                Int64 EdgeCreatedTime,
                Int64 IotHubEnqueueTime,
                Int64 AzFncInitializedTime)
        {
            DeviceId = deviceId;
            ModuleId = moduleId;
            MessageId = msgId;
            EdgeCreatedTimeTicks = EdgeCreatedTime;
            IoTHubEnqueuedTimeTicks = IotHubEnqueueTime;
            AzFncInitializedTimeTicks = AzFncInitializedTime;
            EdgeToHubLatencyMs = 
                (IoTHubEnqueuedTimeTicks - EdgeCreatedTimeTicks) / TimeSpan.TicksPerMillisecond;
            EdgeToAzFncLatencyMs = 
                (AzFncInitializedTimeTicks - EdgeCreatedTimeTicks) / TimeSpan.TicksPerMillisecond;
        }
    }
}
