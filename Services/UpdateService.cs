using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PITS.Data;
using PITS.Models;
using PITS.Plugins;

namespace PITS.Services;

public class UpdateService
{
    private readonly HttpClient _http;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PluginService _pluginService;
    private readonly ILogger<UpdateService> _logger;

    private const string PitsOwner = "zee2ex2";
    private const string PitsRepo = "PITS";

    public UpdateService(HttpClient http, IServiceScopeFactory scopeFactory, PluginService pluginService, ILogger<UpdateService> logger)
    {
        _http = http;
        _scopeFactory = scopeFactory;
        _pluginService = pluginService;
        _logger = logger;
    }

    public async Task<UpdateCheckResult?> CheckPitsUpdateAsync()
    {
        try
        {
            var current = Assembly.GetEntryAssembly()?.GetName()?.Version;
            if (current == null) return null;

            var release = await FetchLatestReleaseAsync(PitsOwner, PitsRepo);
            if (release == null) return null;

            var tagVersion = ParseVersion(release.TagName);
            if (tagVersion == null) return null;

            if (tagVersion <= current) return null;

            return new UpdateCheckResult
            {
                Name = "PITS",
                CurrentVersion = FormatVersion(current),
                LatestVersion = FormatVersion(tagVersion)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check PITS update");
            return null;
        }
    }

    public async Task<List<UpdateCheckResult>> CheckPluginUpdatesAsync(IEnumerable<Plugin> plugins)
    {
        var results = new List<UpdateCheckResult>();

        foreach (var plugin in plugins)
        {
            if (string.IsNullOrEmpty(plugin.RepoUrl)) continue;

            try
            {
                var (owner, repo) = ParseRepoUrl(plugin.RepoUrl);
                if (owner == null || repo == null) continue;

                var release = await FetchLatestReleaseAsync(owner, repo);
                if (release == null) continue;

                var current = ParseVersion(plugin.Version);
                var latest = ParseVersion(release.TagName);
                if (current == null || latest == null) continue;

                if (latest <= current) continue;

                var asset = FindMatchingAsset(release, plugin.Name, plugin.AssemblyPath);

                results.Add(new UpdateCheckResult
                {
                    Name = plugin.Name,
                    PluginId = plugin.Id.ToString(),
                    CurrentVersion = FormatVersion(current),
                    LatestVersion = FormatVersion(latest),
                    DownloadUrl = asset
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check update for plugin {Name}", plugin.Name);
            }
        }

        return results;
    }

    public async Task<bool> UpdatePluginAsync(int pluginId, string downloadUrl)
    {
        try
        {
            var response = await _http.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();

            var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            if (string.IsNullOrEmpty(fileName))
                fileName = "plugin.dll";

            await using var stream = await response.Content.ReadAsStreamAsync();
            await _pluginService.UpdatePluginRecordAsync(pluginId, stream, fileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update plugin {PluginId}", pluginId);
            return false;
        }
    }

    private async Task<GitHubRelease?> FetchLatestReleaseAsync(string owner, string repo)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("PITS/1.0");

        var response = await _http.SendAsync(request);

        if ((int)response.StatusCode == 403 || (int)response.StatusCode == 429)
        {
            _logger.LogWarning("GitHub API rate limited when checking {Owner}/{Repo}", owner, repo);
            return null;
        }

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (json.ValueKind == JsonValueKind.Undefined) return null;

        var tagName = json.GetProperty("tag_name").GetString();
        if (string.IsNullOrEmpty(tagName)) return null;

        var assets = new List<GitHubAsset>();
        if (json.TryGetProperty("assets", out var assetsEl))
        {
            foreach (var asset in assetsEl.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                var url2 = asset.GetProperty("browser_download_url").GetString();
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(url2))
                    assets.Add(new GitHubAsset(name, url2));
            }
        }

        return new GitHubRelease(tagName, assets);
    }

    private static string? FindMatchingAsset(GitHubRelease release, string pluginName, string currentAssemblyPath)
    {
        var currentExt = Path.GetExtension(currentAssemblyPath)?.ToLowerInvariant();

        var candidates = release.Assets
            .Where(a => a.Name.StartsWith(pluginName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
            candidates = release.Assets
                .Where(a => a.Name.Contains(pluginName, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var exactExt = candidates.FirstOrDefault(a =>
            Path.GetExtension(a.Name).Equals(currentExt, StringComparison.OrdinalIgnoreCase));

        if (exactExt != null) return exactExt.Url;

        return candidates.FirstOrDefault()?.Url;
    }

    private static Version? ParseVersion(string? version)
    {
        if (string.IsNullOrEmpty(version)) return null;
        version = version.TrimStart('v', 'V');
        if (Version.TryParse(version, out var v)) return v;
        return null;
    }

    private static string FormatVersion(Version v) => $"{v.Major}.{v.Minor}.{v.Build}";

    private static (string? owner, string? repo) ParseRepoUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            if (uri.Host != "github.com") return (null, null);
            var parts = uri.AbsolutePath.Trim('/').Split('/');
            if (parts.Length < 2) return (null, null);
            return (parts[0], parts[1]);
        }
        catch
        {
            return (null, null);
        }
    }

    private record GitHubRelease(string TagName, List<GitHubAsset> Assets);
    private record GitHubAsset(string Name, string Url);
}