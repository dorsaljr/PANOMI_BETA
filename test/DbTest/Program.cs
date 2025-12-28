using Microsoft.EntityFrameworkCore;
using Panomi.Core.Models;
using Panomi.Data;
using Panomi.Detection;
using Panomi.Detection.Detectors;

Console.WriteLine("=== GAME NAME COMPARISON ===");
Console.WriteLine("".PadRight(50, '='));

try
{
    using var context = new PanomiDbContext();
    
    var games = await context.Games
        .Include(g => g.Launcher)
        .OrderBy(g => g.Name)
        .ToListAsync();
    
    Console.WriteLine($"\nTotal games in database: {games.Count}\n");
    
    // Group by normalized name to find duplicates
    var grouped = games
        .GroupBy(g => g.Name.ToLowerInvariant().Trim())
        .OrderBy(g => g.Key);
    
    foreach (var group in grouped)
    {
        var count = group.Count();
        var marker = count > 1 ? " [DUPLICATE]" : "";
        Console.WriteLine($"\"{group.Key}\"{marker}");
        foreach (var g in group)
        {
            Console.WriteLine($"    -> [{g.Launcher?.Name ?? "Unknown"}] Original: \"{g.Name}\"");
        }
    }
    
    Console.WriteLine("\n=== DUPLICATE SUMMARY ===");
    var duplicates = grouped.Where(g => g.Count() > 1).ToList();
    Console.WriteLine($"Found {duplicates.Count} games on multiple launchers:");
    foreach (var dup in duplicates)
    {
        var launchers = string.Join(", ", dup.Select(g => g.Launcher?.Name ?? "Unknown"));
        Console.WriteLine($"  - {dup.First().Name}: {launchers}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex}");
}
