# xUnit ILogger
This library is a relatively simple wrapper for the `ITestOutputHelper` provided by xUnit. 
When doing integration tests, often the system logs to `ILogger` instances created via Dependency Injection. 
This it problematic for capturing the logs from the tests - which are usually handy for debugging what went wrong in the test!

This `ILoggingProvider` (and associated `ILogger`) implementation allows you to wrap the `ITestOutputHelper` that xUnit provides and 
pass it to your system by registering it as a Logging Provider. This will result in your system's normal logging being captured 
and available in the test output logs.

Typically you would register the `ILoggingProvider` in a `ConfigureServices` method by accessing the `ILoggingBuilder` 
provider therein.

**Example:**

```csharp
public class TestBase {
        protected readonly ITestOutputHelper output;
        protected readonly TestServer server;
        protected TestBase(ITestOutputHelper output)
        {
            this.output = output;
            server = new TestServer(
                new WebHostBuilder()
                    .ConfigureLogging(lb =>
                        lb.AddConsole()
                          .AddProvider(new XUnitLoggerProvider(output))
                    )
                    .UseStartup<Startup>()
            );
        }
}
```

You will likely have more complex `WebHostBuilder` setup than this example. The example is only meant to illustrate the 
registration of the `ILoggingProvder`.

The `XUnitLoggingProvider` also supports reading some configuration from an `IConfiguration` instance so if you have
loaded up confgiuration, you can pass it in the constructor.

**Example:**

_appsettings.Tests.json_
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Trace",
      "Microsoft.Hosting.Lifetime": "Debug"
    }
  },
  "Xunit": {
    "LogLevel": "Debug",
    "LogFormat": "Unformatted"
  }
}
```
Test class:
```csharp
public class TestBase {
        protected readonly ITestOutputHelper output;
        protected readonly TestServer server;
        protected TestBase(ITestOutputHelper output)
        {
            string baseAppSettingsFilename = "appsettings.json";
            IConfigurationBuilder ConfigurationBuilder = new ConfigurationBuilder()
                .AddJsonFile(
                     path: baseAppSettingsFilename,
                     optional: false,
                     reloadOnChange: false)
                .AddJsonFile(
                     path: baseAppSettingsFilename.Replace(".json", $".{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json"),
                     optional: false,
                     reloadOnChange: false)
                .AddJsonFile(
                     path: baseAppSettingsFilename.Replace(".json", $".Tests.json"),
                     optional: false,
                     reloadOnChange: false)
                .AddEnvironmentVariables()
                ;
            IConfiguration config = ConfigurationBuilder.Build();
            this.output = output;
            server = new TestServer(
                new WebHostBuilder()
                    .ConfigureLogging(lb =>
                        lb.AddConsole()
                          .AddProvider(new XUnitLoggerProvider(output))
                    )
                    .UseStartup<Startup>()
            );
        }
}
```

### Configuration
`LogLevel` is read from settings in this priority:
1. `Logging:LogLevel:Xunit:LogLevel`
2. `Logging:Xunit:LogLevel`
3. `Logging:LogLevel:Xunit`
4. `Logging:LogLevel:Default`

`LogFormat` is read from settings in this priority:
1. `Logging:Xunit:LogFormat`
2. `Logging:LogLevel:Xunit:LogFormat`

If you're using ILogger extensions to re-format the log messages, set the `LogFormat` to `Unformatted`. 

The default log output tries to emulate close to the Microsoft log format:
```
{LogLevel}: [{CategoryName}] 
            {formatter output}
{exception}
            => {scope} 
```
