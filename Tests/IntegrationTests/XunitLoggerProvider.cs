using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions.Internal;
using Xunit.Abstractions;

namespace IntuneAppBuilder.IntegrationTests
{

#pragma warning disable S3881 // "IDisposable" should be implemented correctly
    public class XunitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper testOutputHelper;
        private readonly IList<XunitLogger> loggers = new List<XunitLogger>();

        public XunitLoggerProvider(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        public ILogger CreateLogger(string categoryName)
        {
            var logger = new XunitLogger(testOutputHelper, categoryName);
            loggers.Add(logger);
            return logger;
        }

        public void Dispose() => loggers.ToList().ForEach(i => i.Dispose());

        private class XunitLogger : ILogger, IDisposable
        {
            private readonly ITestOutputHelper testOutput;
            private readonly string categoryName;
            private readonly IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();
            private bool isDisposed;

            public XunitLogger(ITestOutputHelper testOutput, string categoryName)
            {
                this.testOutput = testOutput;
                this.categoryName = categoryName;
            }

            public IDisposable BeginScope<TState>(TState state) => scopeProvider?.Push(state) ?? NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (isDisposed) return;

                var output = $"{categoryName}[{eventId}]{GetScopeInformation()}: {formatter(state, exception)}";
                testOutput.WriteLine(output);
                Trace.WriteLine(output);
                if (exception != null)
                {
                    testOutput.WriteLine(exception.ToString());
                    Trace.WriteLine(exception.ToString());
                }
            }

            private string GetScopeInformation()
            {
                var stringBuilder = new StringBuilder();
                scopeProvider.ForEachScope((scope, state) => state.Append(" => ").Append(scope), stringBuilder);
                return stringBuilder.ToString();
            }

            public void Dispose() => isDisposed = true;
        }
    }
}
