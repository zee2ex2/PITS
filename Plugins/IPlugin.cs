namespace PITS.Plugins;

public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    string? RepoUrl => null;
    Task OnLoadAsync(PluginContext context);
    Task OnEnableAsync();
    Task OnDisableAsync();
}
