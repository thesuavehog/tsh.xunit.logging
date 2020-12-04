using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Xunit.Abstractions;

namespace tsh.Xunit.Logging
{
    /// <summary>
    /// Implementation of <see cref="ILoggerProvider"/> which can be added to any 
    /// <see cref="ILoggingBuilder"/> to capture log output and direct it to the 
    /// provided <see cref="ITestOutputHelper"/>.
    /// </summary>
    public class XUnitLoggerProvider : ILoggerProvider
    {

        /// <summary>
        /// LogLevel to set for all <see cref="ILogger"/> instances created by this provider.
        /// </summary>
        protected LogLevel LogLevel { get; } = XUnitLogger.DefaultLogLevel;

        /// <summary>
        /// Format to set for all <see cref="ILogger"/> instances created by this provider.
        /// </summary>
        protected XUnitLogger.LogFormat OutputFormat { get; } = XUnitLogger.DefaultLogFormat;

        /// <summary>
        /// The logging sink where all log messages sent to <see cref="ILogger"/> instances created 
        /// by this provider will be sent. 
        /// </summary>
        /// <remarks>
        /// The destination (<see cref="ITestOutputHelper"/>) cannot be changed after this provider is 
        /// instantiated. To create <see cref="ILogger"/>s for a different <see cref="ITestOutputHelper"/> 
        /// destination, create a new <see cref="XUnitLoggerProvider"/>
        /// </remarks>
        protected ITestOutputHelper Output { get; }

        private readonly IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

        /// <summary>
        /// Keep track of ILogger instances created by this provider and re-use them when there are multiple 
        /// calls for the same named ILogger to avoid creating duplicate ILogger instances.
        /// </summary>
        private readonly ConcurrentDictionary<string, ILogger> _loggers = new ConcurrentDictionary<string, ILogger>();

        /// <summary>
        /// Create a new <see cref="XUnitLoggerProvider"/> which will build <see cref="ILogger"/> instances directing 
        /// output to <paramref name="output"/> for log messages at or above <paramref name="level"/>
        /// </summary>
        /// <param name="output">The destination for log messages written to <see cref="ILogger"/> instances created by this provider</param>
        /// <param name="level">The minimum log level of logging messages to output by <see cref="ILogger"/> instances created by this provider</param>
        /// <param name="outputFormat"></param>
        /// <remarks>
        /// If <paramref name="level"/> is not provided, or null, the <see cref="XUnitLogger.DefaultLogLevel"/> will be used.
        /// </remarks>
        public XUnitLoggerProvider(
            [DisallowNull] ITestOutputHelper output, 
            LogLevel? level = XUnitLogger.DefaultLogLevel,
            XUnitLogger.LogFormat? outputFormat = XUnitLogger.DefaultLogFormat)
        {
            if (output is null) throw new ArgumentNullException(nameof(output));
            Output = output;
            LogLevel = level ?? XUnitLogger.DefaultLogLevel;
            OutputFormat = outputFormat ?? XUnitLogger.DefaultLogFormat;
        }

        /// <summary>
        /// Create a new <see cref="XUnitLoggerProvider"/> which will build <see cref="ILogger"/> instances directing 
        /// output to <paramref name="output"/> for log messages.<br/>
        /// The <see cref="LogLevel"/> and <see cref="OutputFormat"/> will be read from the provided <paramref name="configuration"/>.
        /// </summary>
        /// <param name="output">The destination for log messages written to <see cref="ILogger"/> instances created by this provider</param>
        /// <param name="configuration">IConfiguration to read the log level and log format from </param>
        /// <remarks>
        /// LogLevel Search Order:  "LogLevel:Xunit:LogLevel", "Xunit:LogLevel", "Logging:Xunit", "Logging:Default"<br/>
        /// LogFormat Search Order: "Xunit:LogFormat", "LogLevel:Xunit:LogFormat"
        /// </remarks>
        public XUnitLoggerProvider(
            [DisallowNull] ITestOutputHelper output, 
            [DisallowNull] IConfiguration configuration) : this(output)
        {
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));
            string level = 
                           configuration["Logging:LogLevel:Xunit:LogLevel"] 
                        ?? configuration["Logging:Xunit:LogLevel"] 
                        ?? configuration["Logging:LogLevel:Xunit"] 
                        ?? configuration["Logging:LogLevel:Default"];
            LogLevel = Enum.TryParse<LogLevel>(level, out LogLevel ll) ? ll : LogLevel;
            string format = 
                            configuration["Logging:Xunit:LogFormat"]
                         ?? configuration["Logging:LogLevel:Xunit:LogFormat"];
            OutputFormat = Enum.TryParse(format, out XUnitLogger.LogFormat lf) ? lf : OutputFormat;
        }

        /// <summary>
        /// Obtain an <see cref="ILogger"/> using the provided name.
        /// </summary>
        /// <param name="categoryName"></param>
        /// <returns></returns>
        public ILogger CreateLogger(string categoryName) => 
            _loggers.GetOrAdd(categoryName, name => XUnitLogger.CreateLogger(Output, name, LogLevel, OutputFormat, _scopeProvider));

        /// <summary>
        /// Clear all the ILoggers previously created by this provider.
        /// <seealso cref="IDisposable.Dispose"/>
        /// </summary>
        public void Dispose()
        {
            _loggers.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
