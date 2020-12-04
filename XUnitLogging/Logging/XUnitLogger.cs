using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using Microsoft.Extensions.Logging;

using Xunit.Abstractions;

namespace tsh.XUnit.Logging
{
    /// <summary>
    /// Implementation of <see cref="ILogger"/> interface to wrap a <see cref="ITestOutputHelper"/> 
    /// to capture logging output from implentations being tested which require ILogger interfaces 
    /// to write to.
    /// </summary>
    /// <remarks>
    /// Typically <see cref="XUnitLogger"/> is not instantiated directly, but an <see cref="XUnitLoggerProvider"/> 
    /// is created and used with an <see cref="ILoggingBuilder"/> inside a ConfigureServices call.
    /// </remarks>
    public class XUnitLogger : ILogger
    {
        public enum LogFormat
        {
            Minimal,
            Compressed,
            Normal,
            Unformatted
        }

        /// <summary>
        /// If no specific <see cref="LogLevel"/> is provided when creating this 
        /// logger, this is the default log level to use.
        /// </summary>
        public const LogLevel DefaultLogLevel = LogLevel.Information;

        /// <summary>
        /// If not specific <see cref="OutputFormat"/> is provided when creating this
        /// logger, this is the default log format to use.
        /// </summary>
        public const LogFormat DefaultLogFormat = LogFormat.Normal;

        /// <summary>
        /// Log messages sent to this ILogger will be output to this <see cref="ITestOutputHelper"/>
        /// </summary>
        /// <remarks>
        /// This is immutable once the logger is created.
        /// </remarks>
        public ITestOutputHelper Output { get; }

        /// <summary>
        /// The name of this logger which will be included in the output messages.
        /// </summary>
        public string CategoryName { get; } = null;

        /// <summary>
        /// Log messages at or above this <see cref="LogLevel"/> will be output by this logger
        /// </summary>
        public LogLevel LogLevel { get; } = DefaultLogLevel;

        /// <summary>
        /// Control the format of the output - mostly to support limiting the newlines 
        /// inserted or to output the raw log message without manipulation (i.e. if you are 
        /// already using a structured logging solution)
        /// </summary>
        public LogFormat OutputFormat { get; set; } = DefaultLogFormat;

        /// <summary>
        /// If scopes are enabled, the <see cref="IExternalScopeProvider"/> used to create scopes for this logger.
        /// </summary>
        /// <remarks>
        /// This is exposed only to facilitate implementations extending this class access.
        /// </remarks>
        protected IExternalScopeProvider ScopeProvider { get; } = new LoggerExternalScopeProvider();

        /// <summary>
        /// Create a new <see cref="XUnitLogger"/> instance.
        /// </summary>
        /// <param name="output"></param>
        /// <param name="name"></param>
        /// <param name="level"></param>
        /// <param name="outputFormat"></param>
        /// <param name="scopeProvider"></param>
        /// <returns></returns>
        public static ILogger CreateLogger(
            [DisallowNull] ITestOutputHelper output, 
            string name = null, 
            LogLevel? level = null,
            LogFormat? outputFormat = null,
            IExternalScopeProvider scopeProvider = null)  => new XUnitLogger(output, name, level, outputFormat, scopeProvider);

        /// <summary>
        /// Create a new <see cref="XUnitLogger{T}"/> instance.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="output"></param>
        /// <param name="name"></param>
        /// <param name="level"></param>
        /// <param name="outputFormat"></param>
        /// <param name="scopeProvider"></param>
        /// <returns></returns>
        public static ILogger<T> CreateLogger<T>(
            [DisallowNull] ITestOutputHelper output, 
            string name = null, 
            LogLevel? level = null,
            LogFormat? outputFormat = null,
            IExternalScopeProvider scopeProvider = null) => new XUnitLogger<T>(output, name, level, outputFormat, scopeProvider);

        /// <summary>
        /// Minimal constructor for the only REQUIRED parameter.
        /// </summary>
        /// <param name="output"></param>
        public XUnitLogger([DisallowNull] ITestOutputHelper output)
        {
            if (output is null) throw new ArgumentNullException(nameof(output));
            Output = output;
        }

        /// <summary>
        /// Create a new <see cref="XUnitLogger"/> instance with the given paramters.<br/>
        /// This constructor is intended for subclasse to ease setting the read-only properties.
        /// </summary>
        /// <param name="output"></param>
        /// <param name="categoryName"></param>
        /// <param name="level"></param>
        /// <param name="outputFormat"></param>
        /// <param name="scopeProvider"></param>
        protected XUnitLogger(
            [DisallowNull] ITestOutputHelper output, 
            string categoryName = null, 
            LogLevel? level = DefaultLogLevel,
            LogFormat? outputFormat = DefaultLogFormat,
            IExternalScopeProvider scopeProvider = null) : this(output)
        {
            ScopeProvider = scopeProvider ?? new LoggerExternalScopeProvider();
            CategoryName = categoryName;
            LogLevel = level ?? DefaultLogLevel;
            OutputFormat = outputFormat ?? DefaultLogFormat;
        }

        /// <summary>
        /// Checks if the given logLevel is enabled.
        /// </summary>
        /// <param name="logLevel"></param>
        /// <returns></returns>
        /// <remarks>see: <see cref="ILogger.IsEnabled(LogLevel)"/></remarks>
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel;

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <param name="state"></param>
        /// <returns></returns>
        /// <remarks>See: <see cref="ILogger.BeginScope{TState}(TState)"/></remarks>
        public IDisposable BeginScope<TState>(TState state) => ScopeProvider.Push(state);

        /// <summary>
        /// To output log messages un modified from the source log call, set <see cref="OutputFormat"/> to <see cref="LogFormat.Unformatted"/>.
        /// This is useful, for example, when you are outputting JSON logs from a library that pre-formats the output.<br/>
        /// 
        /// <example>Default Log Output Format:<br/>
        /// <code>
        /// {LogLevel}: [{CategoryName}]<br/> 
        ///             {formatter}<br/>
        /// {exception}<br/>
        ///             => {scope} 
        /// </code>
        /// </example>
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <param name="logLevel"></param>
        /// <param name="eventId"></param>
        /// <param name="state"></param>
        /// <param name="exception"></param>
        /// <param name="formatter"></param>
        /// <remarks>
        /// See: <see cref="ILogger.Log{TState}(LogLevel, EventId, TState, Exception, Func{TState, Exception, string})"/>
        /// </remarks>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;


            string msg = formatter(state, exception);
            if (OutputFormat == LogFormat.Unformatted)
            {
                Output.WriteLine(msg);
                return;
            }
            
            StringBuilder sb = new StringBuilder();
            sb.Append(GetLogLevelString(logLevel)).Append(": ");
            if (!string.IsNullOrWhiteSpace(CategoryName))
                sb.Append('[').Append(CategoryName).Append("] ");

            if (OutputFormat == LogFormat.Normal)
            {
                sb.Append('\n');
            }

            if (!msg.Contains('\n')) sb.Append("      ");
            sb.Append(msg);

            if (exception != null)
            {
                if (msg?.Length > 0) sb.Append('\n');
                sb.Append(exception);
            }

            // Append scopes
            ScopeProvider?.ForEachScope((scope, builder) =>
            {
                if (OutputFormat == LogFormat.Normal)
                    builder.Append('\n').Append("      ");
                builder.Append(" => ");
                builder.Append(scope);
            }, sb);

            Output.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Convert a <see cref="LogLevel"/> into a 4 char string for logging output.
        /// </summary>
        /// <param name="logLevel"></param>
        /// <returns></returns>
        protected static string GetLogLevelString(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "trce",
                LogLevel.Debug => "dbug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "fail",
                LogLevel.Critical => "crit",
                LogLevel.None => "none",
                _ => "????"
            };
        }
    }

    /// <summary>
    /// Generic version of <see cref="XUnitLogger"/> which uses the <see cref="Type.FullName"/> of T 
    /// as the logger category (if none is otherwise provided in the constructor).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class XUnitLogger<T> : XUnitLogger, ILogger<T>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="output"></param>
        /// <param name="name">Normally, not used. Will overrise the <see cref="Type.FullName"/> of T if provided.</param>
        /// <param name="level"></param>
        /// <param name="outputFormat"></param>
        /// <param name="scopeProvider"></param>
        public XUnitLogger(
            [DisallowNull] ITestOutputHelper output, 
            string name = null, 
            LogLevel? level = null,
            LogFormat? outputFormat = null,
            IExternalScopeProvider scopeProvider = null)
            : base(output, name ?? typeof(T).FullName, level, outputFormat, scopeProvider)
        {
        }
    }
}
