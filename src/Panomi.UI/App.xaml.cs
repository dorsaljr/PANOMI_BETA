using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Panomi.Core.Implementations;
using Panomi.Core.Services;
using Panomi.Data;
using Panomi.Data.Services;
using Panomi.Detection;
using Panomi.UI.Services;
using System.Threading;

namespace Panomi.UI;

public partial class App : Application
{
    private Window? _window;
    private static IServiceProvider? _serviceProvider;
    private static Mutex? _mutex;
    private static EventWaitHandle? _showWindowEvent;

    public static IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("Services not initialized");

    public App()
    {
        // Single instance check - must be FIRST
        _mutex = new Mutex(true, "PanomiSingleInstanceMutex", out bool isNewInstance);
        
        if (!isNewInstance)
        {
            // Signal existing instance to show its window
            try
            {
                var existingEvent = EventWaitHandle.OpenExisting("PanomiShowWindowEvent");
                existingEvent.Set();
            }
            catch { }
            
            Environment.Exit(0);
            return;
        }
        
        // Create event for other instances to signal us
        _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "PanomiShowWindowEvent");
        
        // Listen for show window signals in background
        Task.Run(() =>
        {
            while (true)
            {
                _showWindowEvent.WaitOne();
                // Show window on UI thread
                MainWindow?.DispatcherQueue?.TryEnqueue(() =>
                {
                    MainWindow?.ShowFromTray();
                });
            }
        });
        
        // Initialize Velopack first (required for updates to work)
        UpdateService.Initialize();
        
        this.InitializeComponent();
        
        // Global exception handlers for stability
        this.UnhandledException += App_UnhandledException;
        System.AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        
        ConfigureServices();
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Prevent crash, log error
        e.Handled = true;
        System.Diagnostics.Debug.WriteLine($"Unhandled UI exception: {e.Exception}");
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Unhandled domain exception: {e.ExceptionObject}");
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Prevent crash from unobserved task exceptions
        e.SetObserved();
        System.Diagnostics.Debug.WriteLine($"Unobserved task exception: {e.Exception}");
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        // Get the database path
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var panomiPath = Path.Combine(appDataPath, "Panomi");
        Directory.CreateDirectory(panomiPath);
        var dbPath = Path.Combine(panomiPath, "panomi.db");

        // Database - Factory with explicit SQLite configuration
        services.AddDbContextFactory<PanomiDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Core services
        services.AddSingleton<IIconService, IconService>();
        services.AddSingleton<LauncherDetectorFactory>();

        // Data services
        services.AddTransient<IGameService, GameService>();
        services.AddTransient<ILauncherService, LauncherService>();
        services.AddTransient<ISettingsService, SettingsService>();

        _serviceProvider = services.BuildServiceProvider();

        // Initialize database using the factory
        try
        {
            var factory = _serviceProvider.GetRequiredService<IDbContextFactory<PanomiDbContext>>();
            using var context = factory.CreateDbContext();
            context.Database.EnsureCreated();
            System.Diagnostics.Debug.WriteLine($"Database initialized at: {dbPath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database initialization error: {ex}");
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var mainWindow = new MainWindow();
        _window = mainWindow;
        MainWindow = mainWindow;
        _window.Activate();
        
        // Check for updates silently in background
        _ = UpdateService.CheckForUpdatesAsync();
    }

    public static T GetService<T>() where T : class
    {
        return Services.GetRequiredService<T>();
    }

    public static MainWindow? MainWindow { get; private set; }
}
