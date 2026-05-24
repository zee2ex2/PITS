namespace PITS.Plugins;

public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    Task OnLoadAsync(PluginContext context);
    Task OnEnableAsync();
    Task OnDisableAsync();
}
