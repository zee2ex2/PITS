using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using PITS.Data;
using PITS.Models;

namespace PITS.Plugins;

public class PluginService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PluginService> _logger;
    private readonly string _pluginsRoot;
    private readonly ConcurrentDictionary<int, IPlugin> _loadedPlugins = new();

    public IReadOnlyDictionary<int, IPlugin> LoadedPlugins =>
        _loadedPlugins.AsEnumerable().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    public PluginService(IServiceScopeFactory scopeFactory, ILogger<PluginService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pluginsRoot = Path.Combine(AppContext.BaseDirectory, "plugins");
        Directory.CreateDirectory(_pluginsRoot);
    }

    public async Task LoadAllPluginsAsync()
    {
        var loaded = new List<(Plugin record, IPlugin instance)>();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pluginRecords = await db.Plugins.Where(p => p.IsEnabled).ToListAsync();

        foreach (var record in pluginRecords)
        {
            try
            {
                var pluginDir = Path.Combine(_pluginsRoot, SanitizeName(record.Name));
                var pluginScope = _scopeFactory.CreateScope();
                var context = new PluginContext(
                    pluginScope,
                    _logger,
                    pluginDir,
                    record.Name);
                var plugin = await LoadPluginAsync(record, context);
                if (plugin != null)
                {
                    await plugin.OnLoadAsync(context);
                    await plugin.OnEnableAsync();
                    loaded.Add((record, plugin));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin {Name}", record.Name);
            }
        }

        foreach (var (record, instance) in loaded)
            _loadedPlugins[record.Id] = instance;

        _logger.LogInformation("Loaded {Count} plugins", loaded.Count);
    }

    private async Task<IPlugin?> LoadPluginAsync(Plugin record, PluginContext context)
    {
        if (record.AssemblyPath.EndsWith(".csx", StringComparison.OrdinalIgnoreCase))
            return await LoadScriptPluginAsync(record, context);

        return LoadAssemblyPlugin(record, context);
    }

    private async Task<IPlugin?> LoadScriptPluginAsync(Plugin record, PluginContext context)
    {
        var scriptPath = record.AssemblyPath;
        if (!Path.IsPathRooted(scriptPath))
            scriptPath = Path.Combine(_pluginsRoot, SanitizeName(record.Name), record.AssemblyPath);

        if (!File.Exists(scriptPath))
        {
            _logger.LogWarning("Script not found: {Path}", scriptPath);
            return null;
        }

        var code = await File.ReadAllTextAsync(scriptPath);
        var scriptOptions = ScriptOptions.Default
            .WithImports("System", "System.Threading.Tasks", "PITS.Plugins")
            .WithReferences(typeof(PluginContext).Assembly,
                typeof(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions).Assembly);

        var script = CSharpScript.Create(code, scriptOptions, typeof(PluginGlobals));
        script.Compile();

        return new ScriptPluginWrapper(record, context, script);
    }

    private IPlugin? LoadAssemblyPlugin(Plugin record, PluginContext context)
    {
        var assemblyPath = record.AssemblyPath;
        if (!Path.IsPathRooted(assemblyPath))
            assemblyPath = Path.Combine(_pluginsRoot, SanitizeName(record.Name), record.AssemblyPath);

        if (!File.Exists(assemblyPath))
        {
            _logger.LogWarning("Assembly not found: {Path}", assemblyPath);
            return null;
        }

        var assembly = Assembly.LoadFrom(assemblyPath);
        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);

        if (pluginType == null)
        {
            _logger.LogWarning("No IPlugin implementation in {Path}", assemblyPath);
            return null;
        }

        return (IPlugin)Activator.CreateInstance(pluginType)!;
    }

    public async Task InstallPluginAsync(string name, string version, string assemblyFileName, Stream assemblyStream)
    {
        var pluginDir = Path.Combine(_pluginsRoot, SanitizeName(name));
        Directory.CreateDirectory(pluginDir);

        var filePath = Path.Combine(pluginDir, assemblyFileName);
        await using (var fs = File.Create(filePath))
            await assemblyStream.CopyToAsync(fs);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var plugin = new Plugin
        {
            Name = name,
            Version = version,
            AssemblyPath = assemblyFileName,
            IsEnabled = true,
            InstalledAt = DateTime.UtcNow
        };

        db.Plugins.Add(plugin);
        await db.SaveChangesAsync();
    }

    public async Task InstallFromAssemblyAsync(Stream assemblyStream, string fileName)
    {
        if (fileName.EndsWith(".csx", StringComparison.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            await InstallPluginAsync(name, "1.0.0", fileName, assemblyStream);
            return;
        }

        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await InstallFromZipAsync(assemblyStream, fileName);
            return;
        }

        if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Plugin file must be a .zip, .dll, or .csx file");

        var tempDir = Path.Combine(_pluginsRoot, "_temp");
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, SanitizeName(fileName));
        await using (var fs = File.Create(tempPath))
            await assemblyStream.CopyToAsync(fs);

        try
        {
            var assembly = Assembly.LoadFrom(tempPath);
            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);

            if (pluginType == null)
                throw new InvalidOperationException("No IPlugin implementation found in the assembly");

            var instance = (IPlugin)Activator.CreateInstance(pluginType)!;
            var name = instance.Name;
            var version = instance.Version;

            var pluginDir = Path.Combine(_pluginsRoot, SanitizeName(name));
            Directory.CreateDirectory(pluginDir);
            var finalPath = Path.Combine(pluginDir, fileName);
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(tempPath, finalPath);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plugins.Add(new Plugin
            {
                Name = name,
                Version = version,
                AssemblyPath = fileName,
                IsEnabled = true,
                InstalledAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        }
    }

    private async Task InstallFromZipAsync(Stream zipStream, string zipFileName)
    {
        var extractDir = Path.Combine(_pluginsRoot, "_extract");
        Directory.CreateDirectory(extractDir);
        var zipPath = Path.Combine(extractDir, SanitizeName(zipFileName));

        try
        {
            await using (var fs = File.Create(zipPath))
                await zipStream.CopyToAsync(fs);

            ZipFile.ExtractToDirectory(zipPath, extractDir, true);

            var dllFiles = Directory.GetFiles(extractDir, "*.dll");
            string? detectedName = null;
            string? detectedVersion = null;
            string? assemblyFileName = null;

            foreach (var dll in dllFiles)
            {
                try
                {
                    var asm = Assembly.LoadFrom(dll);
                    var pluginType = asm.GetTypes()
                        .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);

                    if (pluginType != null)
                    {
                        var instance = (IPlugin)Activator.CreateInstance(pluginType)!;
                        detectedName = instance.Name;
                        detectedVersion = instance.Version;
                        assemblyFileName = Path.GetFileName(dll);
                        break;
                    }
                }
                catch { }
            }

            if (detectedName == null)
                throw new InvalidOperationException("No IPlugin implementation found in the zip");

            var pluginDir = Path.Combine(_pluginsRoot, SanitizeName(detectedName));
            Directory.CreateDirectory(pluginDir);

            foreach (var filePath in Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(filePath);
                if (name == zipFileName) continue;
                var dest = Path.Combine(pluginDir, name);
                File.Copy(filePath, dest, true);
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plugins.Add(new Plugin
            {
                Name = detectedName,
                Version = detectedVersion ?? "1.0.0",
                AssemblyPath = assemblyFileName ?? $"{detectedName}.dll",
                IsEnabled = true,
                InstalledAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        finally
        {
            try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true); } catch { }
        }
    }

    public async Task InstallFromPathAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Plugin file not found", filePath);

        var fileName = Path.GetFileName(filePath);
        await using var fs = File.OpenRead(filePath);
        await InstallFromAssemblyAsync(fs, fileName);
    }

    public async Task UninstallPluginAsync(int pluginId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plugin = await db.Plugins.FindAsync(pluginId);
        if (plugin == null) return;

        if (_loadedPlugins.TryRemove(pluginId, out var instance))
            await instance.OnDisableAsync();

        var pluginDir = Path.Combine(_pluginsRoot, SanitizeName(plugin.Name));
        if (Directory.Exists(pluginDir))
            Directory.Delete(pluginDir, true);

        db.Plugins.Remove(plugin);
        await db.SaveChangesAsync();
    }

    public async Task<bool> TogglePluginAsync(int pluginId, bool enabled)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var plugin = await db.Plugins.FindAsync(pluginId);
        if (plugin == null) return false;

        plugin.IsEnabled = enabled;
        await db.SaveChangesAsync();

        if (enabled)
        {
            var pluginDir = Path.Combine(_pluginsRoot, SanitizeName(plugin.Name));
            var pluginScope = _scopeFactory.CreateScope();
            var context = new PluginContext(
                pluginScope, _logger,
                pluginDir, plugin.Name);
            var instance = await LoadPluginAsync(plugin, context);
            if (instance != null)
            {
                await instance.OnLoadAsync(context);
                await instance.OnEnableAsync();
                _loadedPlugins[pluginId] = instance;
            }
        }
        else
        {
            if (_loadedPlugins.TryRemove(pluginId, out var instance))
                await instance.OnDisableAsync();
        }

        return true;
    }

    private static string SanitizeName(string name)
        => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));

    private class ScriptPluginWrapper : IPlugin
    {
        private readonly Plugin _record;
        private readonly PluginContext _context;
        private readonly Script<object> _script;

        public string Name => _record.Name;
        public string Version => _record.Version;

        public ScriptPluginWrapper(Plugin record, PluginContext context, Script<object> script)
        {
            _record = record;
            _context = context;
            _script = script;
        }

        public async Task OnLoadAsync(PluginContext context) { }

        public async Task OnEnableAsync()
        {
            var globals = new PluginGlobals { Context = _context };
            await _script.RunAsync(globals);
        }

        public Task OnDisableAsync() => Task.CompletedTask;
    }
}
