using Microsoft.Extensions.DependencyInjection;

namespace PITS.Plugins;

public class PluginContext : IDisposable
{
    private readonly IServiceScope? _scope;
    public IServiceProvider ServiceProvider { get; }
    public ILogger Logger { get; }
    public string PluginDirectory { get; }
    public string PluginName { get; }

    public PluginContext(IServiceProvider serviceProvider, ILogger logger, string pluginDirectory, string pluginName)
    {
        ServiceProvider = serviceProvider;
        Logger = logger;
        PluginDirectory = pluginDirectory;
        PluginName = pluginName;
    }

    internal PluginContext(IServiceScope scope, ILogger logger, string pluginDirectory, string pluginName)
    {
        _scope = scope;
        ServiceProvider = scope.ServiceProvider;
        Logger = logger;
        PluginDirectory = pluginDirectory;
        PluginName = pluginName;
    }

    public T? GetService<T>() where T : class
        => ServiceProvider.GetService<T>();

    public void Dispose()
    {
        _scope?.Dispose();
    }
}
