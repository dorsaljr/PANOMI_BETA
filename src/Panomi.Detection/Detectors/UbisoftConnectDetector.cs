using Panomi.Core.Models;

namespace Panomi.Detection.Detectors;

/// <summary>
/// Detector for Ubisoft Connect (formerly Uplay) games
/// </summary>
public class UbisoftConnectDetector : BaseLauncherDetector
{
    public override LauncherType LauncherType => LauncherType.UbisoftConnect;
    public override string LauncherName => "Ubisoft";

    private const string UbisoftRegistryKey = @"SOFTWARE\Ubisoft\Launcher";
    private const string UbisoftRegistryKey32 = @"SOFTWARE\WOW6432Node\Ubisoft\Launcher";

    public override Task<bool> IsInstalledAsync()
    {
        var installPath = GetUbisoftConnectPath();
        return Task.FromResult(!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath));
    }

    public override Task<string?> GetInstallPathAsync()
    {
        return Task.FromResult(GetUbisoftConnectPath());
    }

    public override Task<DetectionResult> DetectGamesAsync()
    {
        var result = new DetectionResult();
        
        var ubisoftPath = GetUbisoftConnectPath();
        if (string.IsNullOrEmpty(ubisoftPath))
        {
            result.ErrorMessage = "Ubisoft Connect installation not found";
            return Task.FromResult(result);
        }

        result.IsInstalled = true;
        result.InstallPath = ubisoftPath;

        // Primary: Read games from registry (for games with InstallDir)
        var registryGames = GetGamesFromRegistry();
        foreach (var game in registryGames)
        {
            if (!result.Games.Any(g => g.Name == game.Name))
                result.Games.Add(game);
        }

        // Secondary: Scan native games folder from settings.yaml
        var nativeGames = GetNativeGamesFromFolder();
        foreach (var game in nativeGames)
        {
            // Skip if already added from registry
            if (!result.Games.Any(g => g.Name.Equals(game.Name, StringComparison.OrdinalIgnoreCase) || 
                                       g.InstallPath?.Equals(game.InstallPath, StringComparison.OrdinalIgnoreCase) == true))
                result.Games.Add(game);
        }

        return Task.FromResult(result);
    }

    private List<DetectedGame> GetGamesFromRegistry()
    {
        var games = new List<DetectedGame>();
        
        // Games are registered under HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\{GameId}
        // Use lowercase 'ubisoft' and 32-bit registry view (matching Playnite's proven approach)
        var registryConfigs = new[]
        {
            (Path: @"SOFTWARE\ubisoft\Launcher\Installs", Use32Bit: true),  // 32-bit view (where Ubisoft stores data)
            (Path: @"SOFTWARE\ubisoft\Launcher\Installs", Use32Bit: false), // 64-bit view fallback
        };

        // Check HKLM paths with different registry views
        foreach (var config in registryConfigs)
        {
            try
            {
                var gameIds = GetRegistrySubKeyNames(config.Path, use32BitView: config.Use32Bit);
                foreach (var gameId in gameIds)
                {
                    var game = ParseGameFromRegistry(config.Path, gameId, config.Use32Bit);
                    if (game != null && !games.Any(g => g.ExternalId == game.ExternalId))
                    {
                        games.Add(game);
                    }
                }
            }
            catch
            {
                // Continue to next registry config
            }
        }
        
        // Check HKCU paths
        var hkcuPath = @"SOFTWARE\ubisoft\Launcher\Installs";
        try
        {
            var gameIds = GetUserRegistrySubKeyNames(hkcuPath);
            foreach (var gameId in gameIds)
            {
                var game = ParseGameFromUserRegistry(hkcuPath, gameId);
                if (game != null && !games.Any(g => g.ExternalId == game.ExternalId))
                    games.Add(game);
            }
        }
        catch
        {
            // Continue if registry access fails
        }

        return games;
    }

    private List<DetectedGame> GetNativeGamesFromFolder()
    {
        var games = new List<DetectedGame>();
        
        // Get the games folder from settings.yaml
        var gamesFolder = GetGamesInstallFolder();
        if (string.IsNullOrEmpty(gamesFolder) || !Directory.Exists(gamesFolder))
            return games;

        try
        {
            // Scan each subdirectory as a potential game
            var gameDirs = Directory.GetDirectories(gamesFolder);
            foreach (var gameDir in gameDirs)
            {
                var game = ParseGameFromFolder(gameDir);
                if (game != null)
                    games.Add(game);
            }
        }
        catch
        {
            // Ignore folder scanning errors
        }

        return games;
    }

    private string? GetGamesInstallFolder()
    {
        // Read from settings.yaml in LocalAppData
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Ubisoft Game Launcher",
                "settings.yaml");

            if (!File.Exists(settingsPath))
                return null;

            var content = File.ReadAllText(settingsPath);
            
            // Parse game_installation_path from YAML (simple parsing)
            var match = System.Text.RegularExpressions.Regex.Match(
                content, 
                @"game_installation_path:\s*(.+?)(?:\r?\n|$)");
            
            if (match.Success)
            {
                var path = match.Groups[1].Value.Trim();
                // Convert forward slashes to backslashes
                path = path.Replace("/", "\\");
                if (Directory.Exists(path))
                    return path;
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        // Fallback to default location
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Ubisoft", "Ubisoft Game Launcher", "games");
        
        if (Directory.Exists(defaultPath))
            return defaultPath;

        return null;
    }

    private DetectedGame? ParseGameFromFolder(string gameDir)
    {
        try
        {
            var folderName = Path.GetFileName(gameDir);
            if (string.IsNullOrEmpty(folderName))
                return null;

            // Skip if it's not a valid game folder
            if (!IsValidGameInstall(gameDir))
                return null;

            // Find executable
            var exePath = FindMainExecutable(gameDir);
            if (string.IsNullOrEmpty(exePath))
                return null;

            // Get game name from folder name
            var gameName = folderName;

            // Skip DLC and extras
            if (IsSkippable(gameName, ""))
                return null;

            // Try to find game ID from known games or folder name mapping
            var gameId = GetGameIdFromFolderName(folderName);

            // Launch command uses uplay:// protocol if we have an ID, otherwise use exe
            var launchCommand = !string.IsNullOrEmpty(gameId) 
                ? $"uplay://launch/{gameId}/0" 
                : exePath;

            return new DetectedGame
            {
                Name = CleanGameTitle(gameName),
                ExternalId = gameId ?? folderName,
                InstallPath = gameDir,
                ExecutablePath = exePath,
                LaunchCommand = launchCommand
            };
        }
        catch
        {
            return null;
        }
    }

    private string? GetGameIdFromFolderName(string folderName)
    {
        // Map known folder names to game IDs (from Ubisoft Connect registry)
        var folderToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Brawlhalla", "16382" },
            { "Roller Champions", "11899" },
            { "XDefiant", "925" },
            { "Tom Clancy's Rainbow Six Siege", "635" },
            { "Rainbow Six Siege", "635" },
            { "Far Cry 5", "720" },
            { "Far Cry 6", "4923" },
            { "Far Cry 4", "410" },
            { "Far Cry Primal", "568" },
            { "Assassin's Creed Valhalla", "1842" },
            { "Assassin's Creed Mirage", "5855" },
            { "Assassin's Creed Odyssey", "3539" },
            { "Watch Dogs Legion", "5266" },
            { "The Division 2", "2738" },
            { "The Crew 2", "2739" },
            { "Ghost Recon Breakpoint", "4312" },
            { "Immortals Fenyx Rising", "1843" },
            { "Riders Republic", "5436" },
            { "Anno 1800", "3787" },
            { "Hyper Scape", "5265" },
        };

        if (folderToId.TryGetValue(folderName, out var id))
            return id;

        return null;
    }

    private DetectedGame? ParseGameFromRegistry(string basePath, string gameId, bool use32BitView = false)
    {
        try
        {
            var fullPath = $@"{basePath}\{gameId}";
            
            // Read install directory
            var installDir = ReadRegistryValue(fullPath, "InstallDir", use32BitView);
            if (string.IsNullOrEmpty(installDir))
                return null;
            
            // Normalize path (Ubisoft uses forward slashes in registry)
            installDir = installDir.Replace("/", "\\").TrimEnd('\\');
            
            if (!Directory.Exists(installDir))
                return null;

            // Validate it's a real game install
            if (!IsValidGameInstall(installDir))
                return null;

            // Get game name from folder or config
            var gameName = GetGameName(installDir, gameId);
            if (string.IsNullOrEmpty(gameName))
                return null;

            // Skip DLC and extras
            if (IsSkippable(gameName, gameId))
                return null;

            // Find executable
            var exePath = FindMainExecutable(installDir);

            // Launch command uses uplay:// protocol
            var launchCommand = $"uplay://launch/{gameId}/0";

            return new DetectedGame
            {
                Name = CleanGameTitle(gameName),
                ExternalId = gameId,
                InstallPath = installDir,
                ExecutablePath = exePath,
                LaunchCommand = launchCommand
            };
        }
        catch
        {
            return null;
        }
    }

    private DetectedGame? ParseGameFromUserRegistry(string basePath, string gameId)
    {
        try
        {
            var fullPath = $@"{basePath}\{gameId}";
            
            var installDir = ReadUserRegistryValue(fullPath, "InstallDir");
            if (string.IsNullOrEmpty(installDir))
                return null;
            
            // Normalize path (Ubisoft uses forward slashes in registry)
            installDir = installDir.Replace("/", "\\").TrimEnd('\\');
            
            if (!Directory.Exists(installDir))
                return null;

            if (!IsValidGameInstall(installDir))
                return null;

            var gameName = GetGameName(installDir, gameId);
            if (string.IsNullOrEmpty(gameName))
                return null;

            if (IsSkippable(gameName, gameId))
                return null;

            var exePath = FindMainExecutable(installDir);
            var launchCommand = $"uplay://launch/{gameId}/0";

            return new DetectedGame
            {
                Name = CleanGameTitle(gameName),
                ExternalId = gameId,
                InstallPath = installDir,
                ExecutablePath = exePath,
                LaunchCommand = launchCommand
            };
        }
        catch
        {
            return null;
        }
    }

    private string? GetGameName(string installDir, string gameId)
    {
        // First try to get name from folder
        var folderName = Path.GetFileName(installDir);
        if (!string.IsNullOrEmpty(folderName) && !IsGenericFolderName(folderName))
            return folderName;

        // Map known game IDs to names (common Ubisoft titles)
        var knownGames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "635", "Tom Clancy's Rainbow Six Siege" },
            { "720", "Far Cry 5" },
            { "1842", "Assassin's Creed Valhalla" },
            { "5855", "Assassin's Creed Mirage" },
            { "4923", "Far Cry 6" },
            { "3539", "Assassin's Creed Odyssey" },
            { "5266", "Watch Dogs: Legion" },
            { "410", "Far Cry 4" },
            { "568", "Far Cry Primal" },
            { "2738", "The Division 2" },
            { "2739", "The Crew 2" },
            { "4312", "Ghost Recon Breakpoint" },
            { "1843", "Immortals Fenyx Rising" },
            { "5436", "Riders Republic" },
            { "5265", "Hyper Scape" },
            { "3787", "Anno 1800" },
        };

        if (knownGames.TryGetValue(gameId, out var knownName))
            return knownName;

        // Fallback to folder name even if generic
        return folderName;
    }

    private bool IsGenericFolderName(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower == "game" || lower == "games" || lower.All(char.IsDigit);
    }

    private string? FindMainExecutable(string installDir)
    {
        try
        {
            // Check root directory first
            var rootExes = Directory.GetFiles(installDir, "*.exe", SearchOption.TopDirectoryOnly);
            
            if (rootExes.Length > 0)
            {
                var bestMatch = rootExes
                    .Where(f => !IsUtilityExe(Path.GetFileName(f)))
                    .OrderByDescending(f => new FileInfo(f).Length)
                    .FirstOrDefault();

                if (bestMatch != null)
                    return bestMatch;
            }

            // Check common subdirectories
            var subDirs = new[] { "bin", "Bin", "x64", "Win64" };
            foreach (var subDir in subDirs)
            {
                var subPath = Path.Combine(installDir, subDir);
                if (Directory.Exists(subPath))
                {
                    var subExes = Directory.GetFiles(subPath, "*.exe", SearchOption.TopDirectoryOnly);
                    var mainExe = subExes.FirstOrDefault(f => !IsUtilityExe(Path.GetFileName(f)));
                    if (mainExe != null)
                        return mainExe;
                }
            }

            return rootExes.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private bool IsUtilityExe(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        var utilityPatterns = new[]
        {
            "unins", "crash", "report", "update", "patch",
            "redist", "vcredist", "dxsetup", "dotnet",
            "installer", "setup", "upc", "uplay"
        };
        return utilityPatterns.Any(p => lower.Contains(p));
    }

    private bool IsSkippable(string name, string gameId)
    {
        var lower = name.ToLowerInvariant();
        var skipPatterns = new[]
        {
            "dlc", "season pass", "expansion", "soundtrack",
            "art book", "artbook", "bonus", "pack"
        };
        return skipPatterns.Any(p => lower.Contains(p));
    }

    private string? GetUbisoftConnectPath()
    {
        // Try registry first (HKLM 64-bit)
        var path = ReadRegistryValue(UbisoftRegistryKey, "InstallDir");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Try 32-bit registry view
        path = ReadRegistryValue(UbisoftRegistryKey32, "InstallDir");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Try HKCU
        path = ReadUserRegistryValue(UbisoftRegistryKey, "InstallDir");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        path = ReadUserRegistryValue(UbisoftRegistryKey32, "InstallDir");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Common default paths
        var defaultPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Ubisoft", "Ubisoft Game Launcher"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ubisoft", "Ubisoft Game Launcher"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Ubisoft Game Launcher"),
        };

        foreach (var defaultPath in defaultPaths)
        {
            if (Directory.Exists(defaultPath))
                return defaultPath;
        }

        return null;
    }

    private string CleanGameTitle(string title)
    {
        return title
            .Replace("™", "")
            .Replace("®", "")
            .Replace("  ", " ")
            .Trim();
    }
}
