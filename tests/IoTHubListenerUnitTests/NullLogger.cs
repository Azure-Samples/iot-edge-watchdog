using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions.Internal;

namespace IoTHubListenerTests
{
public class NullLogger : ILogger
    {
        public static NullLogger Instance { get; } = new NullLogger();

        private NullLogger()
        {
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Console.WriteLine("WOOT logging");
        }
    }
}