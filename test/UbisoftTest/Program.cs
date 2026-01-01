d using Microsoft.Win32;
using Panomi.Detection.Detectors;

Console.WriteLine("=== Ubisoft Connect Detection Test ===\n");

// Test the actual detector
Console.WriteLine("TESTING ACTUAL DETECTOR");
Console.WriteLine("═══════════════════════════════════════\n");

var detector = new UbisoftConnectDetector();

Console.WriteLine($"Launcher Type: {detector.LauncherType}");
Console.WriteLine($"Launcher Name: {detector.LauncherName}");

var isInstalled = await detector.IsInstalledAsync();
Console.WriteLine($"Is Installed: {isInstalled}");

var installPath = await detector.GetInstallPathAsync();
Console.WriteLine($"Install Path: {installPath ?? "NULL"}\n");

Console.WriteLine("Detecting games...");
Console.WriteLine("─────────────────────────────────────────");

var result = await detector.DetectGamesAsync();

Console.WriteLine($"Detection Result:");
Console.WriteLine($"  IsInstalled: {result.IsInstalled}");
Console.WriteLine($"  InstallPath: {result.InstallPath ?? "NULL"}");
Console.WriteLine($"  ErrorMessage: {result.ErrorMessage ?? "none"}");
Console.WriteLine($"  Games Count: {result.Games.Count}\n");

if (result.Games.Count > 0)
{
    foreach (var game in result.Games)
    {
        Console.WriteLine($"✓ GAME: {game.Name}");
        Console.WriteLine($"    ExternalId: {game.ExternalId}");
        Console.WriteLine($"    InstallPath: {game.InstallPath}");
        Console.WriteLine($"    ExecutablePath: {game.ExecutablePath}");
        Console.WriteLine($"    LaunchCommand: {game.LaunchCommand}");
        Console.WriteLine();
    }
}
else
{
    Console.WriteLine("❌ No games detected!\n");
    
    // Manual registry check
    Console.WriteLine("\nMANUAL REGISTRY CHECK");
    Console.WriteLine("─────────────────────────────────────────");
    
    var registryPath = @"SOFTWARE\ubisoft\Launcher\Installs";
    using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
    using var installsKey = root.OpenSubKey(registryPath);
    
    if (installsKey != null)
    {
        var gameIds = installsKey.GetSubKeyNames();
        Console.WriteLine($"Registry shows {gameIds.Length} game(s): {string.Join(", ", gameIds)}");
    }
}

Console.WriteLine("\n=== Test Complete ===");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
