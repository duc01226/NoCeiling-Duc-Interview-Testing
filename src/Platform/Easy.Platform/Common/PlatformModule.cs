using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Easy.Platform.Common.Cqrs;
using Easy.Platform.Common.Cqrs.Events;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.Extensions;
using Easy.Platform.Common.HostingBackgroundServices;
using Easy.Platform.Common.JsonSerialization;
using Easy.Platform.Common.Utils;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pyroscope;

namespace Easy.Platform.Common;

public interface IPlatformModule
{
    public const int DefaultMaxWaitModuleInitiatedSeconds = 86400 * 5;

    public const string DefaultLogCategory = "Easy.Platform";

    /// <summary>
    /// Higher Priority value mean the module init will be executed before lower Priority value in the same level module dependencies
    /// <br />
    /// Default is 10. For the default priority should be:  InfrastructureModule (Not Dependent on DatabaseInitialization) => PersistenceModule => InfrastructureModule (Dependent on DatabaseInitialization) => Others Module (10)
    /// </summary>
    public int ExecuteInitPriority { get; }

    public IServiceCollection ServiceCollection { get; }
    public IServiceProvider ServiceProvider { get; }
    public IConfiguration Configuration { get; }

    /// <summary>
    /// A child module is a module which exist other module depend on it
    /// </summary>
    public bool IsChildModule { get; set; }

    /// <summary>
    /// A module is root when it's the entry module, when there's not any module depend on it. Usually it's PlatformAspNetCoreModule or PlatformApplicationModule
    /// </summary>
    public bool IsRootModule => CheckIsRootModule(this);

    /// <summary>
    /// Current runtime module instance Assembly
    /// </summary>
    public Assembly Assembly { get; }

    public bool RegisterServicesExecuted { get; }
    public bool Initiated { get; }

    /// <summary>
    /// Gets the action that configures additional tracing settings for the platform module.
    /// </summary>
    /// <value>
    /// The action that accepts a <see cref="TracerProviderBuilder" /> and configures it.
    /// </value>
    /// <remarks>
    /// This property can be used to add custom tracing configurations for the platform module.
    /// </remarks>
    public Action<TracerProviderBuilder> AdditionalTracingConfigure { get; }

    public static ILogger CreateDefaultLogger(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(DefaultLogCategory);
    }

    /// <summary>
    /// Waits until all modules of a specific type are initiated.
    /// </summary>
    /// <param name="serviceProvider">The service provider to fetch services.</param>
    /// <param name="moduleType">The type of the modules to wait for.</param>
    /// <param name="logger">The logger to log information. If null, a default logger will be created.</param>
    /// <param name="logSuffix">The suffix for the log information.</param>
    /// <param name="notLogging">If true not log information</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public static async Task WaitAllModulesInitiatedAsync(
        IServiceProvider serviceProvider,
        Type moduleType,
        ILogger logger = null,
        string logSuffix = null,
        bool notLogging = true)
    {
        if (serviceProvider.GetServices(moduleType).Select(p => p.As<IPlatformModule>()).All(p => p.Initiated)) return;

        var useLogger = logger ?? CreateDefaultLogger(serviceProvider);

        if (!notLogging) useLogger.LogInformation("[PlatformModule] Start WaitAllModulesInitiated of type {ModuleType} {LogSuffix} STARTED", moduleType.Name, logSuffix);

        await Util.TaskRunner.WaitUntilAsync(
            () =>
            {
                var modules = serviceProvider.GetServices(moduleType).Select(p => p.As<IPlatformModule>());

                return Task.FromResult(modules.All(p => p.Initiated));
            },
            serviceProvider.GetServices(moduleType).Count() * DefaultMaxWaitModuleInitiatedSeconds,
            waitForMsg: $"Wait for all modules of type {moduleType.Name} get initiated",
            waitIntervalSeconds: 5);

        if (!notLogging) useLogger.LogInformation("[Platform] WaitAllModulesInitiated of type {ModuleType} {LogSuffix} FINISHED", moduleType.Name, logSuffix);
    }

    /// <summary>
    /// Retrieves all dependent child modules of the current platform module.
    /// </summary>
    /// <param name="useServiceCollection">Optional. The service collection to use. If null, the service provider of the current module is used.</param>
    /// <param name="includeDeepChildModules">Optional. If true, includes all deep child modules in the returned list. Default is true.</param>
    /// <returns>A list of all dependent child modules.</returns>
    public List<IPlatformModule> AllDependencyChildModules(IServiceCollection useServiceCollection = null, bool includeDeepChildModules = true);

    public static bool CheckIsRootModule(IPlatformModule module)
    {
        return !module.IsChildModule;
    }

    public void RegisterServices(IServiceCollection serviceCollection);

    /// <summary>
    /// Initializes the platform module.
    /// </summary>
    /// <param name="currentApp">Optional. The current application builder. If null, the application builder of the current module is used.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public Task Init(IApplicationBuilder currentApp = null);

    public List<Func<IConfiguration, Type>> ModuleTypeDependencies();

    /// <summary>
    /// Override this to call every time a new platform module is registered
    /// </summary>
    public void OnNewOtherModuleRegistered(
        IServiceCollection serviceCollection,
        PlatformModule newOtherRegisterModule);

    public void RegisterRuntimeModuleDependencies<TModule>(
        IServiceCollection serviceCollection) where TModule : PlatformModule;

    public string[] TracingSources();
}

/// <summary>
/// Represents a platform module that provides a set of functionalities and services.
/// </summary>
/// <remarks>
/// This class is an abstract base class for all platform modules. It provides a common set of properties and methods
/// that are used to manage the lifecycle of a module, such as initialization, registration of services, and disposal.
/// </remarks>
/// <example>
/// Here is an example of how to use this class:
/// <code>
/// services.RegisterModule{XXXApiModule}(); // Register module into service collection
/// // Get module service in collection and call module.Init();
/// // Init module to start running init for all other modules and this module itself
/// </code>
/// </example>
public abstract class PlatformModule : IPlatformModule, IDisposable
{
    public const int DefaultExecuteInitPriority = 10;
    public const int ExecuteInitPriorityNextLevelDistance = 10;

    protected static readonly ConcurrentDictionary<string, Assembly> ExecutedRegisterByAssemblies = new();

    protected readonly SemaphoreSlim InitLockAsync = new(1, 1);
    protected readonly SemaphoreSlim RegisterLockAsync = new(1, 1);
    private bool disposed;

    public PlatformModule(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        ServiceProvider = serviceProvider;
        Configuration = configuration;
        LoggerFactory = serviceProvider?.GetService<ILoggerFactory>();
        Logger = serviceProvider?.GetService<ILoggerFactory>()?.Pipe(CreateLogger);
    }

    protected ILogger Logger { get; init; }

    protected ILoggerFactory LoggerFactory { get; init; }

    protected virtual bool AutoScanAssemblyRegisterCqrs => false;

    protected IApplicationBuilder CurrentAppBuilder { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public bool IsRootModule => IPlatformModule.CheckIsRootModule(this);

    public virtual int ExecuteInitPriority => DefaultExecuteInitPriority;

    public IServiceCollection ServiceCollection { get; private set; }
    public IServiceProvider ServiceProvider { get; }
    public IConfiguration Configuration { get; }

    /// <summary>
    /// True if the module is in a dependency list of other module, not a root module
    /// </summary>
    public bool IsChildModule { get; set; }

    /// <summary>
    /// Current runtime module instance Assembly
    /// </summary>
    public Assembly Assembly => GetType().Assembly;

    public bool RegisterServicesExecuted { get; protected set; }

    public bool Initiated { get; protected set; }

    /// <summary>
    /// Override this to call every time a new other module is registered
    /// </summary>
    public virtual void OnNewOtherModuleRegistered(
        IServiceCollection serviceCollection,
        PlatformModule newOtherRegisterModule)
    {
    }

    public void RegisterRuntimeModuleDependencies<TModule>(
        IServiceCollection serviceCollection) where TModule : PlatformModule
    {
        serviceCollection.RegisterModule<TModule>(true);
    }

    /// <summary>
    /// Registers the services provided by this module into the provided service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection to which the services will be registered.</param>
    /// <remarks>
    /// This method performs several operations:
    /// - It registers all module dependencies.
    /// - It registers default logs.
    /// - It registers CQRS.
    /// - It registers helpers.
    /// - It registers distributed tracing.
    /// - It performs internal registration.
    /// - It registers the platform root service provider.
    /// - It sets the current JSON serializer options if they are not null.
    /// After all these operations, it sets the RegisterServicesExecuted property to true.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the services have already been registered.</exception>
    public void RegisterServices(IServiceCollection serviceCollection)
    {
        try
        {
            RegisterLockAsync.Wait();

            if (RegisterServicesExecuted)
                return;

            ServiceCollection = serviceCollection;
            RegisterAllModuleDependencies(serviceCollection);
            RegisterDefaultLogs(serviceCollection);
            RegisterCqrs(serviceCollection);
            RegisterHelpers(serviceCollection);
            RegisterDistributedTracing(serviceCollection);
            InternalRegister(serviceCollection);
            serviceCollection.Register<IPlatformRootServiceProvider>(sp => new PlatformRootServiceProvider(sp, ServiceCollection), ServiceLifeTime.Singleton);

            RegisterServicesExecuted = true;

            if (JsonSerializerCurrentOptions() != null)
                PlatformJsonSerializer.SetCurrentOptions(JsonSerializerCurrentOptions());
        }
        finally
        {
            RegisterLockAsync.Release();
        }
    }

    public virtual async Task Init(IApplicationBuilder currentApp = null)
    {
        try
        {
            if (currentApp != null) CurrentAppBuilder = currentApp;

            await InitLockAsync.WaitAsync();

            if (Initiated)
                return;

            Logger.LogInformation("[PlatformModule] {Module} Init STARTED", GetType().Name);

            await InitAllModuleDependencies();
            await InitPerformanceProfiling();

            using (var scope = ServiceProvider.CreateScope()) await InternalInit(scope);

            Initiated = true;

            Logger.LogInformation("[PlatformModule] {Module} Init FINISHED", GetType().Name);
        }
        finally
        {
            InitLockAsync.Release();
        }
    }

    /// <summary>
    /// Get all dependency modules, also init the value of <see cref="IsChildModule" />, which also affect <see cref="IsRootModule" />
    /// </summary>
    public List<IPlatformModule> AllDependencyChildModules(IServiceCollection useServiceCollection = null, bool includeDeepChildModules = true)
    {
        return ModuleTypeDependencies()
            .Select(
                moduleTypeProvider =>
                {
                    var moduleType = moduleTypeProvider(Configuration);
                    var serviceProvider = useServiceCollection?.BuildServiceProvider() ?? ServiceProvider;

                    var dependModule = serviceProvider.GetService(moduleType)
                        .As<IPlatformModule>()
                        .Ensure(
                            dependModule => dependModule != null,
                            $"Module {GetType().Name} depend on {moduleType.Name} but Module {moduleType.Name} does not implement IPlatformModule");

                    dependModule.IsChildModule = true;

                    return includeDeepChildModules
                        ? dependModule.AllDependencyChildModules(useServiceCollection).ConcatSingle(dependModule)
                        : [dependModule];
                })
            .Flatten()
            .ToList();
    }

    public virtual string[] TracingSources() { return []; }
    public virtual Action<TracerProviderBuilder> AdditionalTracingConfigure => null;

    /// <summary>
    /// Define list of any modules that this module depend on. The type must be assigned to <see cref="PlatformModule" />.
    /// Example from a XXXServiceAspNetCoreModule could depend on XXXPlatformApplicationModule and
    /// XXXPlatformPersistenceModule.
    /// Example code : return new { config => typeof(XXXPlatformApplicationModule), config =>
    /// typeof(XXXPlatformPersistenceModule) };
    /// </summary>
    public virtual List<Func<IConfiguration, Type>> ModuleTypeDependencies()
    {
        return [];
    }

    /// <summary>
    /// Initializes the performance profiling settings for the platform module.
    /// </summary>
    protected async Task InitPerformanceProfiling()
    {
        if (!IsRootModule) return;

        var config = ConfigPerformanceProfiling();

        if (config.Enabled == true)
        {
            Logger.LogInformation("[PlatformModule] InitPerformanceProfiling. Config:{Config}", config.ToFormattedJson());

            Profiler.Instance.SetCPUTrackingEnabled(config.Enabled == true && (config.CPUTrackingEnabled ?? true));
            Profiler.Instance.SetAllocationTrackingEnabled(config.Enabled == true && (config.AllocationTrackingEnabled ?? true));
            Profiler.Instance.SetContentionTrackingEnabled(config.Enabled == true && (config.ContentionTrackingEnabled ?? false));
            Profiler.Instance.SetExceptionTrackingEnabled(config.Enabled == true && (config.ExceptionTrackingEnabled ?? false));
        }
    }

    /// <summary>
    /// Return the current Assembly of the module and it's parent not abstract module.
    /// Used to register scanning by assembly support scan the parent module too by default
    /// </summary>
    public virtual List<Assembly> GetServicesRegisterScanAssemblies()
    {
        var result = new List<Assembly>();

        // Process add ancestor platform parent module assemblies
        var currentCheckBaseTypeTargetType = GetType();
        while (currentCheckBaseTypeTargetType.BaseType is { IsAbstract: false } &&
               currentCheckBaseTypeTargetType.BaseType.IsAssignableTo(typeof(PlatformModule)))
        {
            result.Add(currentCheckBaseTypeTargetType.BaseType.Assembly);

            currentCheckBaseTypeTargetType = currentCheckBaseTypeTargetType.BaseType;
        }

        result.Add(Assembly);

        return result;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                InitLockAsync?.Dispose();
                RegisterLockAsync?.Dispose();
            }

            // Release unmanaged resources

            disposed = true;
        }
    }

    // Finalizer (destructor)
    ~PlatformModule()
    {
        Dispose(false);
    }

    /// <summary>
    /// Registers the distributed tracing services to the provided service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection to which the distributed tracing services are added.</param>
    /// <remarks>
    /// This method should only be called if the current module is the root module and distributed tracing is enabled.
    /// </remarks>
    protected void RegisterDistributedTracing(IServiceCollection serviceCollection)
    {
        if (IsRootModule)
        {
            var distributedTracingConfig = ConfigDistributedTracing();

            serviceCollection.Register(
                typeof(DistributedTracingConfig),
                _ => distributedTracingConfig,
                ServiceLifeTime.Singleton,
                true,
                DependencyInjectionExtension.CheckRegisteredStrategy.ByService);

            if (distributedTracingConfig.Enabled)
            {
                // Setup OpenTelemetry
                var allDependencyModules = AllDependencyChildModules(serviceCollection);

                var allDependencyModulesTracingSources = allDependencyModules.SelectMany(p => p.TracingSources());

                serviceCollection.AddOpenTelemetry()
                    .WithTracing(
                        builder => builder
                            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(distributedTracingConfig.AppName ?? GetType().Assembly.GetName().Name!))
                            .AddSource(TracingSources().Concat(CommonTracingSources()).Concat(allDependencyModulesTracingSources).Distinct().ToArray())
                            .WithIf(AdditionalTracingConfigure != null, AdditionalTracingConfigure)
                            .WithIf(distributedTracingConfig.AdditionalTraceConfig != null, distributedTracingConfig.AdditionalTraceConfig)
                            .WithIf(distributedTracingConfig.AddOtlpExporterConfig != null, p => p.AddOtlpExporter(distributedTracingConfig.AddOtlpExporterConfig))
                            .WithIf(
                                allDependencyModules.Any(),
                                p => allDependencyModules
                                    .Where(dependencyModule => dependencyModule.AdditionalTracingConfigure != null)
                                    .Select(dependencyModule => dependencyModule.AdditionalTracingConfigure)
                                    .ForEach(dependencyModuleAdditionalTracingConfigure => dependencyModuleAdditionalTracingConfigure(p))));
            }
        }
    }

    public static List<string> CommonTracingSources()
    {
        return [IPlatformCqrsEventHandler.ActivitySource.Name, PlatformIntervalHostingBackgroundService.ActivitySource.Name];
    }

    public ILogger CreateLogger(ILoggerFactory loggerFactory)
    {
        return loggerFactory.CreateLogger(GetType());
    }

    protected static void ExecuteRegisterByAssemblyOnlyOnce(Action<Assembly> action, List<Assembly> assemblies, string actionName)
    {
        assemblies.ForEach(
            assembly =>
            {
                var executedRegisterByAssemblyKey = $"Action:{ExecutedRegisterByAssemblies.ContainsKey(actionName)};Assembly:{assembly.FullName}";

                if (!ExecutedRegisterByAssemblies.ContainsKey(executedRegisterByAssemblyKey))
                {
                    action(assembly);

                    ExecutedRegisterByAssemblies.TryAdd(executedRegisterByAssemblyKey, assembly);
                }
            });
    }

    /// <summary>
    /// Registers services in the provided service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection to add the services to.</param>
    /// <remarks>
    /// This method is called internally by the platform module.
    /// Derived classes should override this method to register their specific services.
    /// </remarks>
    protected virtual void InternalRegister(IServiceCollection serviceCollection)
    {
    }

    protected virtual Task InternalInit(IServiceScope serviceScope)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Override this to setup custom value for <see cref="PlatformJsonSerializer.CurrentOptions" />
    /// </summary>
    /// <returns></returns>
    protected virtual JsonSerializerOptions JsonSerializerCurrentOptions()
    {
        return null;
    }

    /// <summary>
    /// Initializes all module dependencies asynchronously.
    /// </summary>
    /// <remarks>
    /// This method groups all dependent modules by their execution priority, orders them in descending order of priority,
    /// and then initializes each group of modules in parallel. This ensures that higher-priority modules are initialized before lower-priority ones.
    /// </remarks>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    /// <example>
    ///     <code>
    /// await InitAllModuleDependencies();
    /// </code>
    /// </example>
    protected async Task InitAllModuleDependencies()
    {
        await AllDependencyChildModules()
            .GroupBy(p => p.ExecuteInitPriority)
            .OrderByDescending(p => p.Key)
            .ForEachAsync(p => p.ParallelAsync(module => module.Init(CurrentAppBuilder)));
    }

    protected virtual void RegisterHelpers(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllFromType<IPlatformHelper>(GetServicesRegisterScanAssemblies());
    }

    /// <summary>
    /// Configures the distributed tracing settings for the platform module.
    /// </summary>
    /// <returns>
    /// A <see cref="DistributedTracingConfig" /> object that contains the configuration settings for distributed tracing.
    /// </returns>
    /// <remarks>
    /// This method can be overridden in derived classes to provide custom configuration for distributed tracing.
    /// </remarks>
    protected virtual DistributedTracingConfig ConfigDistributedTracing()
    {
        return new DistributedTracingConfig();
    }

    /// <summary>
    /// Configures the performance profiling settings for the platform module. THIS SHOULD ONLY BE CONFIGURED ON ROOT MODULE (USUALLY API OR APPLICATION MODULE, MODULE IS INIT IN PROGRAM)
    /// </summary>
    /// <returns>
    /// A <see cref="PerformanceProfilingConfig" /> object that contains the configuration settings for performance profiling.
    /// </returns>
    /// <remarks>
    /// This method can be overridden in derived classes to provide custom configuration for performance profiling.
    /// </remarks>
    protected virtual PerformanceProfilingConfig ConfigPerformanceProfiling()
    {
        return new PerformanceProfilingConfig();
    }

    protected static void RegisterDefaultLogs(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterIfServiceNotExist(typeof(ILoggerFactory), typeof(LoggerFactory), ServiceLifeTime.Singleton);
        serviceCollection.RegisterIfServiceNotExist(typeof(ILogger<>), typeof(Logger<>));
        serviceCollection.RegisterIfServiceNotExist(typeof(ILogger), IPlatformModule.CreateDefaultLogger);
    }

    protected void RegisterCqrs(IServiceCollection serviceCollection)
    {
        if (AutoScanAssemblyRegisterCqrs)
        {
            ExecuteRegisterByAssemblyOnlyOnce(
                assembly =>
                {
                    serviceCollection.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

                    serviceCollection.Register<IPlatformCqrs, PlatformCqrs>(ServiceLifeTime.Scoped, supportLazyInject: true);
                    serviceCollection.RegisterAllSelfImplementationFromType(typeof(IPipelineBehavior<,>), assembly);
                },
                GetServicesRegisterScanAssemblies(),
                nameof(RegisterCqrs));
        }
    }

    protected void RegisterAllModuleDependencies(IServiceCollection serviceCollection)
    {
        ModuleTypeDependencies()
            .Select(moduleTypeProvider => moduleTypeProvider(Configuration))
            .ForEach(moduleType => serviceCollection.RegisterModule(moduleType, true));
    }

    /// <summary>
    /// Represents the configuration settings for distributed tracing.
    /// </summary>
    public class DistributedTracingConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether distributed tracing is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the action to configure additional trace settings.
        /// </summary>
        public Action<TracerProviderBuilder> AdditionalTraceConfig { get; set; }

        /// <summary>
        /// Gets or sets the action to configure the OpenTelemetry Protocol (OTLP) exporter options.
        /// </summary>
        public Action<OtlpExporterOptions> AddOtlpExporterConfig { get; set; }

        /// <summary>
        /// Gets or sets the name of the application.
        /// </summary>
        public string AppName { get; set; }

        public string? DistributedTracingStackTrace()
        {
            return DistributedTracingStackTraceEnabled() ? PlatformEnvironment.StackTrace() : null;
        }

        public bool DistributedTracingStackTraceEnabled()
        {
            return Enabled || PlatformEnvironment.IsDevelopment;
        }
    }

    public class PerformanceProfilingConfig
    {
        public bool? Enabled { get; set; } = false;

        /// <summary>
        /// Enables or disables CPU/wall profiling dynamically.
        /// This function works in conjunction with the PYROSCOPE_PROFILING_CPU_ENABLED and
        /// PYROSCOPE_PROFILING_WALLTIME_ENABLED environment variables. If CPU/wall profiling is not
        /// configured, this function will have no effect.
        /// </summary>
        public bool? CPUTrackingEnabled { get; set; } = true;

        /// <summary>
        /// Enables or disables allocation profiling dynamically.
        /// This function works in conjunction with the PYROSCOPE_PROFILING_ALLOCATION_ENABLED environment variable.
        /// If allocation profiling is not configured, this function will have no effect.
        /// </summary>
        public bool? AllocationTrackingEnabled { get; set; } = true;

        /// <summary>
        /// Enables or disables allocation profiling dynamically.
        /// This function works in conjunction with the PYROSCOPE_PROFILING_LOCK_ENABLED environment variable.
        /// If allocation profiling is not configured, this function will have no effect.
        /// </summary>
        public bool? ContentionTrackingEnabled { get; set; } = false;

        /// <summary>
        /// Enables or disables allocation profiling dynamically.
        /// This function works in conjunction with the PYROSCOPE_PROFILING_EXCEPTION_ENABLED environment variable.
        /// If allocation profiling is not configured, this function will have no effect.
        /// </summary>
        public bool? ExceptionTrackingEnabled { get; set; } = false;
    }
}
