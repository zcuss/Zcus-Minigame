using System.Text.Json;
using AutoMinigameWinForms.Models;

namespace AutoMinigameWinForms.Core;

public static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string DefaultPath => Path.Combine(GetConfigDirectory(), "config.json");

    private static string GetConfigDirectory()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(roaming))
        {
            var managedDir = Path.Combine(roaming, "Zcus", "Zcus Minigame");
            Directory.CreateDirectory(managedDir);
            return managedDir;
        }

        try
        {
            // During `dotnet run`, AppContext.BaseDirectory points to `bin/...`.
            // Prefer project directory when available so config stays stable.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 8 && dir is not null; i++)
            {
                var csproj = Path.Combine(dir.FullName, "AutoMinigameWinForms.csproj");
                if (File.Exists(csproj))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }
        }
        catch
        {
            // Fallback below.
        }

        return AppContext.BaseDirectory;
    }

    public static AppConfig Load(string path)
    {
        try
        {
            MigrateLegacyConfigIfNeeded(path);
            if (!File.Exists(path))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(path);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            return cfg ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public static void Save(string path, AppConfig cfg)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var json = JsonSerializer.Serialize(cfg, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static void MigrateLegacyConfigIfNeeded(string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        var legacyPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "config.json"),
            Path.Combine(AppContext.BaseDirectory, "app2_config.json"),
        };

        var legacyPath = legacyPaths.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(legacyPath))
        {
            return;
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.Copy(legacyPath, path, overwrite: false);
    }
}
