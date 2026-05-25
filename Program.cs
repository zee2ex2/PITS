using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.EntityFrameworkCore;
using PITS.Components;
using PITS.Data;
using PITS.Data.Seed;
using PITS.Plugins;
using PITS.Services;

var builder = WebApplication.CreateBuilder(args);

var dataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "PITS", "data");
Directory.CreateDirectory(dataDir);
var configPath = Path.Combine(dataDir, "config.json");

var dbPath = Path.Combine(dataDir, "pits.db");
if (File.Exists(configPath))
{
    try
    {
        var config = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(configPath));
        if (config != null && config.TryGetValue("dbPath", out var customPath) && !string.IsNullOrEmpty(customPath) && File.Exists(customPath))
            dbPath = customPath;
    }
    catch { }
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddSingleton(new PitsDbConfig { DbPath = dbPath, ConfigPath = configPath });

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<CircuitOptions>(options =>
{
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
    options.DisconnectedCircuitMaxRetained = 100;
    options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(1);
});

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<PluginService>();
builder.Services.AddSingleton<InventoryChangeNotifier>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddScoped<SessionNotificationStore>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddHttpClient<UpdateService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PITS/0.1.0");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.WebHost.UseKestrel(options =>
{
    options.ListenAnyIP(5050);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    try
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE plugins ADD COLUMN repo_url TEXT NULL");
    }
    catch
    {
        // Column already exists, ignore
    }

    if (!await db.SchemaInfo.AnyAsync())
    {
        db.SchemaInfo.Add(new PITS.Models.SchemaInfo { Version = "v1", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    var seedPath = Path.Combine(AppContext.BaseDirectory, "seed_data.json");
    if (!File.Exists(seedPath))
        seedPath = Path.Combine(builder.Environment.ContentRootPath, "..", "ShoWER", "seed_data.json");
    await SeedData.InitializeAsync(db, seedPath);
}

var pluginService = app.Services.GetRequiredService<PluginService>();
await pluginService.LoadAllPluginsAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapGet("/api/browse", (string? path) =>
{
    var dir = string.IsNullOrEmpty(path)
        ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        : path;
    if (!Directory.Exists(dir))
        return Results.NotFound();

    try
    {
        var directories = Directory.GetDirectories(dir)
            .Select(d => new { Name = Path.GetFileName(d), Path = d, IsDirectory = true })
            .OrderBy(e => e.Name);

        var files = Directory.GetFiles(dir, "*.db")
            .Select(f => new { Name = Path.GetFileName(f), Path = f, IsDirectory = false })
            .OrderBy(e => e.Name);

        return Results.Ok(new
        {
            CurrentPath = dir,
            Parent = Directory.GetParent(dir)?.FullName,
            Entries = directories.Concat(files)
        });
    }
    catch
    {
        return Results.Ok(new
        {
            CurrentPath = dir,
            Parent = Directory.GetParent(dir)?.FullName,
            Entries = Enumerable.Empty<object>()
        });
    }
});

app.MapPost("/api/db-path", (string path, PitsDbConfig config) =>
{
    if (string.IsNullOrEmpty(path) || !File.Exists(path))
        return Results.BadRequest("File does not exist");

    try
    {
        var cfg = new Dictionary<string, string> { { "dbPath", path } };
        File.WriteAllText(config.ConfigPath, JsonSerializer.Serialize(cfg));
        return Results.Ok(new { Message = "Database path saved. Restart the application to use the new database." });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

var url = $"http://localhost:5050";
Console.WriteLine($"PITS running at {url}");

var trayService = new TrayService(
    app.Services.GetRequiredService<ILogger<TrayService>>(), url);
trayService.Start();

if (app.Environment.IsDevelopment())
    TrayService.OpenBrowser(url);

app.Run();

record PitsDbConfig
{
    public string DbPath { get; init; } = "";
    public string ConfigPath { get; init; } = "";
}