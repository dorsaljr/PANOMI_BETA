using Microsoft.Win32;
using Panomi.Core.Interfaces;
using Panomi.Core.Models;
using System.Runtime.Versioning;

namespace Panomi.Detection;

/// <summary>
/// Base class for launcher detectors with common registry/file utilities
/// </summary>
[SupportedOSPlatform("windows")]
public abstract class BaseLauncherDetector : ILauncherDetector
{
    public abstract LauncherType LauncherType { get; }
    public abstract string LauncherName { get; }

    public abstract Task<bool> IsInstalledAsync();
    public abstract Task<string?> GetInstallPathAsync();
    public abstract Task<DetectionResult> DetectGamesAsync();

    /// <summary>
    /// Read a registry value from HKEY_LOCAL_MACHINE
    /// </summary>
    protected string? ReadRegistryValue(string keyPath, string valueName, bool use32BitView = false)
    {
        try
        {
            var view = use32BitView ? RegistryView.Registry32 : RegistryView.Registry64;
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var key = baseKey.OpenSubKey(keyPath);
            return key?.GetValue(valueName)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Read a registry value from HKEY_CURRENT_USER
    /// </summary>
    protected string? ReadUserRegistryValue(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(valueName)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check if a registry key exists
    /// </summary>
    protected bool RegistryKeyExists(string keyPath, bool useHKCU = false, bool use32BitView = false)
    {
        try
        {
            if (useHKCU)
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath);
                return key != null;
            }
            else
            {
                var view = use32BitView ? RegistryView.Registry32 : RegistryView.Registry64;
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(keyPath);
                return key != null;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get all subkey names from a registry key
    /// </summary>
    protected string[] GetRegistrySubKeyNames(string keyPath, bool useHKCU = false, bool use32BitView = false)
    {
        try
        {
            RegistryKey? key;
            if (useHKCU)
            {
                key = Registry.CurrentUser.OpenSubKey(keyPath);
            }
            else
            {
                var view = use32BitView ? RegistryView.Registry32 : RegistryView.Registry64;
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                key = baseKey.OpenSubKey(keyPath);
            }

            if (key != null)
            {
                using (key)
                {
                    return key.GetSubKeyNames();
                }
            }
        }
        catch
        {
        }
        return Array.Empty<string>();
    }

    /// <summary>
    /// Get all subkey names from HKCU registry key
    /// </summary>
    protected string[] GetUserRegistrySubKeyNames(string keyPath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            if (key != null)
            {
                return key.GetSubKeyNames();
            }
        }
        catch
        {
        }
        return Array.Empty<string>();
    }

    /// <summary>
    /// Safely read all text from a file
    /// </summary>
    protected async Task<string?> ReadFileAsync(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return await File.ReadAllTextAsync(path);
            }
        }
        catch
        {
        }
        return null;
    }

    /// <summary>
    /// Find files matching a pattern in a directory
    /// </summary>
    protected IEnumerable<string> FindFiles(string directory, string pattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                return Directory.GetFiles(directory, pattern, searchOption);
            }
        }
        catch
        {
        }
        return Enumerable.Empty<string>();
    }
    
    /// <summary>
    /// Validates that a game install path exists and contains actual game files.
    /// This filters out manifests for uninstalled games, tools, and runtimes.
    /// </summary>
    protected bool IsValidGameInstall(string? installPath)
    {
        if (string.IsNullOrEmpty(installPath))
            return false;
        
        if (!Directory.Exists(installPath))
            return false;
        
        try
        {
            // Check if the folder has at least one executable
            // Use EnumerateFiles for performance - stops at first match
            var hasExe = Directory.EnumerateFiles(installPath, "*.exe", SearchOption.AllDirectories).Any();
            return hasExe;
        }
        catch
        {
            return false;
        }
    }
}
