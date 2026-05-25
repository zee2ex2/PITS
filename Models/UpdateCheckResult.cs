namespace PITS.Models;

public class UpdateCheckResult
{
    public string Name { get; init; } = "";
    public string? PluginId { get; init; }
    public string CurrentVersion { get; init; } = "";
    public string? LatestVersion { get; init; }
    public string? DownloadUrl { get; init; }
    public bool HasUpdate => LatestVersion != null;
}