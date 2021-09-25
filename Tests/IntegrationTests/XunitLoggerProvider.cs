using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Karambolo.Extensions.Logging.File;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace IntuneAppBuilder.IntegrationTests
{
#pragma warning disable S3881 // "IDisposable" should be implemented correctly
#pragma warning disable CA1063 // Implement IDisposable Correctly
    internal sealed class XunitLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private static readonly ConcurrentDictionary<ITestOutputHelper, Lazy<XunitLoggerProvider>> Providers = new ConcurrentDictionary<ITestOutputHelper, Lazy<XunitLoggerProvider>>();
        private readonly Lazy<FileStream> file;
        private readonly Lazy<StreamWriter> fileWriter;
        private readonly ConcurrentDictionary<string, XunitLogger> loggers = new ConcurrentDictionary<string, XunitLogger>();
        private readonly ITestOutputHelper testOutputHelper;

        private XunitLoggerProvider(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;

            var logFilePath = Path.Combine(AppContext.BaseDirectory, $"{GetTest(testOutputHelper).DisplayName}.log");
            var assemblyName = GetType().Assembly.GetName().Name!;
            if (logFilePath.StartsWith(assemblyName!)) logFilePath = logFilePath[assemblyName.Length..].TrimStart('.');
            file = new Lazy<FileStream>(() => File.Open(logFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read));
            fileWriter = new Lazy<StreamWriter>(() => new StreamWriter(file.Value) { AutoFlush = true });
        }

        public IExternalScopeProvider ScopeProvider { get; private set; } = NullExternalScopeProvider.Instance;

        public void Dispose()
        {
            loggers.Values.ToList().ForEach(i => i.Dispose());

            if (fileWriter.IsValueCreated)
            {
                fileWriter.Value.Flush();
                fileWriter.Value.Dispose();
            }

            if (file.IsValueCreated)
            {
                file.Value.Flush();
                file.Value.Dispose();
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            var logger = loggers.GetOrAdd(categoryName, _ => new XunitLogger(testOutputHelper, categoryName, ScopeProvider, fileWriter));
            return logger;
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => ScopeProvider = scopeProvider;

        public static XunitLoggerProvider GetOrCreate(ITestOutputHelper testOutputHelper) => Providers.GetOrAdd(testOutputHelper, _ => new Lazy<XunitLoggerProvider>(() => new XunitLoggerProvider(testOutputHelper))).Value;

        private static ITest GetTest(ITestOutputHelper output)
        {
            var type = output.GetType();
            var testMember = type.GetField("test", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var test = (ITest)testMember!.GetValue(output)!;
            return test;
        }

        /// <summary>
        ///     Scope provider that does nothing.
        /// </summary>
        private sealed class NullExternalScopeProvider : IExternalScopeProvider
        {
            private NullExternalScopeProvider()
            {
            }

            /// <summary>
            ///     Returns a cached instance of <see cref="NullExternalScopeProvider" />.
            /// </summary>
            public static IExternalScopeProvider Instance { get; } = new NullExternalScopeProvider();

            void IExternalScopeProvider.ForEachScope<TState>(Action<object, TState> callback, TState state)
            {
            }

            IDisposable IExternalScopeProvider.Push(object state) => NullScope.Instance;
        }

        /// <summary>
        ///     An empty scope without any logic
        /// </summary>
        private sealed class NullScope : IDisposable
        {
            private NullScope()
            {
            }

            public static NullScope Instance { get; } = new NullScope();

            public void Dispose()
            {
            }
        }

        private sealed class XunitLogger : ILogger, IDisposable
        {
            private readonly string categoryName;
            private readonly Lazy<StreamWriter> fileWriter;
            private readonly IExternalScopeProvider scopeProvider;

            private readonly ITestOutputHelper testOutput;
            private bool isDisposed;

            public XunitLogger(ITestOutputHelper testOutput, string categoryName, IExternalScopeProvider scopeProvider, Lazy<StreamWriter> fileWriter)
            {
                this.testOutput = testOutput;
                this.categoryName = categoryName;
                this.scopeProvider = scopeProvider;
                this.fileWriter = fileWriter;
            }

            public void Dispose() => isDisposed = true;

            public IDisposable BeginScope<TState>(TState state) => scopeProvider.Push(state) ?? NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (isDisposed) return;

                var sb = new StringBuilder();
                FileLogEntryTextBuilder.Instance.BuildEntryText(sb, categoryName, logLevel, eventId, formatter(state, exception), exception, scopeProvider, DateTimeOffset.Now);
                var output = sb.ToString();
                testOutput.WriteLine(output.Trim());
                if (Debugger.IsAttached)
                {
                    Trace.Write(output);
                }

                fileWriter.Value.Write(output);

                if (logLevel == LogLevel.Error)
                {
                    throw new XunitException(exception.ToString());
                }
            }
        }
    }
}