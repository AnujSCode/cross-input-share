using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace CrossInputShare.Tests.TestUtilities
{
    /// <summary>
    /// Base class for all tests providing common functionality.
    /// </summary>
    public abstract class TestBase : IAsyncLifetime
    {
        protected readonly ITestOutputHelper TestOutputHelper;
        protected IConfiguration Configuration { get; private set; }
        protected IServiceProvider ServiceProvider { get; private set; }
        protected ILoggerFactory LoggerFactory { get; private set; }
        
        /// <summary>
        /// Gets a unique test identifier for this test run.
        /// </summary>
        protected string TestId { get; } = Guid.NewGuid().ToString("N")[..8];
        
        /// <summary>
        /// Gets the test output directory for this test.
        /// </summary>
        protected string TestOutputDirectory { get; private set; }

        protected TestBase(ITestOutputHelper testOutputHelper)
        {
            TestOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
        }

        /// <summary>
        /// Initializes the test before each test method.
        /// </summary>
        public virtual async Task InitializeAsync()
        {
            // Create test-specific output directory
            TestOutputDirectory = Path.Combine(
                Path.GetTempPath(),
                "CrossInputShareTests",
                TestId);
            
            Directory.CreateDirectory(TestOutputDirectory);
            
            // Load configuration
            Configuration = BuildConfiguration();
            
            // Configure services
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
            
            // Configure logging
            LoggerFactory = ServiceProvider.GetService<ILoggerFactory>();
            
            TestOutputHelper.WriteLine($"Test initialized: {GetType().Name}");
            TestOutputHelper.WriteLine($"Test ID: {TestId}");
            TestOutputHelper.WriteLine($"Output directory: {TestOutputDirectory}");
            
            await OnInitializeAsync();
        }

        /// <summary>
        /// Cleans up after each test method.
        /// </summary>
        public virtual async Task DisposeAsync()
        {
            await OnDisposeAsync();
            
            // Clean up test output directory if empty
            try
            {
                if (Directory.Exists(TestOutputDirectory) && 
                    Directory.GetFiles(TestOutputDirectory).Length == 0)
                {
                    Directory.Delete(TestOutputDirectory);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
            
            // Dispose service provider
            if (ServiceProvider is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            
            TestOutputHelper.WriteLine($"Test disposed: {GetType().Name}");
        }

        /// <summary>
        /// Builds the configuration for tests.
        /// </summary>
        protected virtual IConfiguration BuildConfiguration()
        {
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.test.json", optional: true)
                .AddEnvironmentVariables()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>("TestSettings:TestId", TestId),
                    new KeyValuePair<string, string>("TestSettings:OutputDirectory", TestOutputDirectory)
                });

            return configurationBuilder.Build();
        }

        /// <summary>
        /// Configures services for dependency injection.
        /// </summary>
        protected virtual void ConfigureServices(IServiceCollection services)
        {
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConfiguration(Configuration.GetSection("Logging"));
                builder.AddConsole();
                builder.AddDebug();
                builder.AddProvider(new TestOutputLoggerProvider(TestOutputHelper));
            });

            // Add configuration
            services.AddSingleton(Configuration);
        }

        /// <summary>
        /// Override to perform additional initialization.
        /// </summary>
        protected virtual Task OnInitializeAsync() => Task.CompletedTask;

        /// <summary>
        /// Override to perform additional cleanup.
        /// </summary>
        protected virtual Task OnDisposeAsync() => Task.CompletedTask;

        /// <summary>
        /// Creates a test file in the test output directory.
        /// </summary>
        protected string CreateTestFile(string fileName, string content = null)
        {
            var filePath = Path.Combine(TestOutputDirectory, fileName);
            
            if (content != null)
            {
                File.WriteAllText(filePath, content);
            }
            else
            {
                File.Create(filePath).Dispose();
            }
            
            return filePath;
        }

        /// <summary>
        /// Creates a test directory in the test output directory.
        /// </summary>
        protected string CreateTestDirectory(string directoryName)
        {
            var directoryPath = Path.Combine(TestOutputDirectory, directoryName);
            Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }

        /// <summary>
        /// Gets a logger for the current test.
        /// </summary>
        protected ILogger<T> GetLogger<T>() where T : class
        {
            return LoggerFactory?.CreateLogger<T>() ?? 
                   ServiceProvider.GetService<ILogger<T>>();
        }

        /// <summary>
        /// Asserts that an action throws an exception of the specified type.
        /// </summary>
        protected void AssertThrows<TException>(Action action, string because = "")
            where TException : Exception
        {
            action.Should().Throw<TException>(because);
        }

        /// <summary>
        /// Asserts that an async action throws an exception of the specified type.
        /// </summary>
        protected async Task AssertThrowsAsync<TException>(Func<Task> action, string because = "")
            where TException : Exception
        {
            await action.Should().ThrowAsync<TException>(because);
        }

        /// <summary>
        /// Measures the execution time of an action.
        /// </summary>
        protected TimeSpan MeasureExecutionTime(Action action)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        /// <summary>
        /// Measures the execution time of an async action.
        /// </summary>
        protected async Task<TimeSpan> MeasureExecutionTimeAsync(Func<Task> action)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await action();
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }
    }

    /// <summary>
    /// Logger provider that writes to XUnit test output.
    /// </summary>
    public class TestOutputLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public TestOutputLoggerProvider(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TestOutputLogger(categoryName, _testOutputHelper);
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Logger that writes to XUnit test output.
    /// </summary>
    public class TestOutputLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly ITestOutputHelper _testOutputHelper;

        public TestOutputLogger(string categoryName, ITestOutputHelper testOutputHelper)
        {
            _categoryName = categoryName;
            _testOutputHelper = testOutputHelper;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            try
            {
                var message = formatter(state, exception);
                var logMessage = $"{DateTime.Now:HH:mm:ss.fff} [{logLevel}] {_categoryName}: {message}";
                
                if (exception != null)
                {
                    logMessage += $"\nException: {exception}";
                }
                
                _testOutputHelper.WriteLine(logMessage);
            }
            catch
            {
                // Ignore logging errors in tests
            }
        }

        private class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();
            public void Dispose() { }
        }
    }
}