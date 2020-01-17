using System;
using Xunit;

namespace IoTHubListenerTests
{
    public class EnvArgTwinParserTests
    {
        
        public bool DeleteTestEnvVar(string varName){
            Environment.SetEnvironmentVariable(varName, null);
            // return deletion status.
            return Environment.GetEnvironmentVariable(varName) == null;
        }

        [Fact]
        public void GetTimeSpanEnvVarTest()
        {
            var testEnvVar = "HB_FREQUENCY_IN_SECONDS";
            var testTimeSpan = 10;
            TimeSpan defaultVal = TimeSpan.FromSeconds(testTimeSpan);
            var value = Environment.GetEnvironmentVariable(testEnvVar);
            // Create it if it's empty
            if (value == null) 
            {
                Environment.SetEnvironmentVariable(testEnvVar, testTimeSpan.ToString());
                
                Assert.Equal(defaultVal, HeartbeatModule.Program.GetTimeSpanEnvVar(testEnvVar, testTimeSpan));
            }
            
            DeleteTestEnvVar(testEnvVar);
        }

    }

}