using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Panomi.Core.Models;
using Panomi.Core.Services;
using Panomi.UI.Helpers;
using Panomi.UI.Services;
using Windows.UI;
using H.NotifyIcon;

namespace Panomi.UI;

public sealed partial class MainWindow : Window
{
    private readonly IGameService _gameService;
    private readonly ILauncherService _launcherService;
    private readonly string _filterSettingsPath;
    private readonly string _favoritesSettingsPath;
    private readonly string _settingsPath;
    
    private ObservableCollection<LibraryItem> _allItems = new();
    private ObservableCollection<LibraryItem> _filteredItems = new();
    private FilterType _currentFilter = FilterType.All;
    private bool _isInitialized = false;
    private bool _showFavoritesOnly = false;
    private bool _isFullscreen = false;
    private bool _isLoadingSettings = false;
    private HashSet<string> _selectedLaunchers = new();
    private HashSet<string> _favorites = new();
    private Dictionary<LauncherType, BitmapImage?> _launcherIcons = new();
    
    private Microsoft.UI.Windowing.AppWindow? _appWindow;
    
    // Scrollbar drag state
    private bool _isDraggingThumb = false;
    private double _dragStartY = 0;
    private double _dragStartScrollOffset = 0;
    
    // Track button currently showing "Launching..." for language updates
    private LibraryItem? _launchingButton = null;
    
    // Tray icon for minimize to tray feature
    private TaskbarIcon? _trayIcon;
    private bool _minimizeToTray = true;  // Default ON for better first impression
    private bool _isReallyClosing = false;

    public MainWindow()
    {
        this.InitializeComponent();
        
        // Set window size and customize title bar
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        _appWindow.Resize(new Windows.Graphics.SizeInt32(1400, 900));
        
        // Listen for window state changes
        _appWindow.Changed += AppWindow_Changed;
        
        // Set custom window icon for taskbar
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "logo_ico.ico");
        _appWindow.SetIcon(iconPath);
        
        // Customize title bar color to match cards
        if (_appWindow.TitleBar != null)
        {
            _appWindow.TitleBar.BackgroundColor = Windows.UI.Color.FromArgb(255, 15, 17, 20);
            _appWindow.TitleBar.InactiveBackgroundColor = Windows.UI.Color.FromArgb(255, 15, 17, 20);
            _appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(255, 15, 17, 20);
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(255, 15, 17, 20);
        }
        
        Title = "";
        
        _gameService = App.GetService<IGameService>();
        _launcherService = App.GetService<ILauncherService>();
        
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var panomiPath = System.IO.Path.Combine(appDataPath, "Panomi");
        System.IO.Directory.CreateDirectory(panomiPath);
        _filterSettingsPath = System.IO.Path.Combine(panomiPath, "filters.json");
        _favoritesSettingsPath = System.IO.Path.Combine(panomiPath, "favorites.json");
        _settingsPath = System.IO.Path.Combine(panomiPath, "settings.json");
        
        LoadFavorites();
        LoadAppSettings();
        
        CardsGrid.ItemsSource = _filteredItems;
        
        // Setup custom scrollbar after layout
        MainScrollViewer.Loaded += (s, e) => UpdateCustomScrollBar();
        MainScrollViewer.SizeChanged += (s, e) => UpdateCustomScrollBar();
        
        // ESC key to exit fullscreen
        this.Content.KeyDown += Content_KeyDown;
        
        // Detect utility apps in background (non-blocking)
        Task.Run(() =>
        {
            _discordPath = IconExtractor.GetDiscordExePath();
            _spotifyPath = IconExtractor.GetSpotifyExePath();
            
            // Load Discord icon on UI thread if found
            if (_discordPath != null)
            {
                DispatcherQueue.TryEnqueue(() => LoadDiscordIcon());
            }
        });
        
        PopulateLanguagePanel();
        ApplyLocalizedStrings();
        _ = LoadLibraryAsync();
        
        // Initialize tray icon only if setting is enabled
        if (_minimizeToTray)
        {
            InitializeTrayIcon();
        }
        
        // Handle window close for minimize to tray
        _appWindow.Closing += AppWindow_Closing;
        
        // Check for updates and show button if available
        CheckForUpdateButton();
    }
    
    private async void CheckForUpdateButton()
    {
        // Wait a few seconds for update check to complete
        await Task.Delay(5000);
        
        if (UpdateService.HasPendingUpdate)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateButton.Visibility = Visibility.Visible;
            });
        }
    }
    
    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateService.ApplyUpdateAndRestart();
    }
    
    private void ApplyLocalizedStrings()
    {
        // Buttons
        ScanButtonText.Text = Loc.Get("ScanButton");
        
        // Filter buttons
        FilterAll.Content = Loc.Get("FilterAll");
        FilterLaunchers.Content = Loc.Get("FilterLaunchers");
        FilterGames.Content = Loc.Get("FilterGames");
        SourceFilterButton.Content = Loc.Get("FilterButton");
        
        // Settings
        FullscreenText.Text = Loc.Get("FullscreenMode");
        StartupText.Text = Loc.Get("StartWithWindows");
        MinimizeToTrayText.Text = Loc.Get("MinimizeToTray");
        QuickLaunchInfoText.Text = Loc.Get("QuickLaunchInfo");
        LanguageText.Text = Loc.Get("LanguageSetting");
        
        // Stats labels
        StatTotalLabel.Text = Loc.Get("StatTotal");
        StatLaunchersLabel.Text = Loc.Get("StatLaunchers");
        StatGamesLabel.Text = Loc.Get("StatGames");
        
        // Library header
        LibraryHeader.Text = Loc.Get("Library");
        
        // Update Launch button text on all items
        var launchText = Loc.Get("LaunchButton");
        foreach (var item in _allItems)
        {
            item.LaunchText = launchText;
        }
        
        // Update launching button if one is active
        if (_launchingButton != null)
        {
            _launchingButton.LaunchText = Loc.Get("LaunchingButton");
        }
    }
    
    private void LoadAppSettings()
    {
        try
        {
            if (System.IO.File.Exists(_settingsPath))
            {
                var json = System.IO.File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    _isLoadingSettings = true;
                    FullscreenToggle.IsChecked = settings.Fullscreen;
                    StartupToggle.IsChecked = settings.StartWithWindows;
                    MinimizeToTrayToggle.IsChecked = settings.MinimizeToTray;
                    _minimizeToTray = settings.MinimizeToTray;
                    
                    // Set language
                    Loc.SetLanguage(settings.Language ?? "en-US");
                    
                    _isLoadingSettings = false;
                    
                    if (settings.Fullscreen)
                    {
                        SetFullscreen(true);
                    }
                }
            }
        }
        catch { }
    }
    
    private void SaveAppSettings()
    {
        try
        {
            var settings = new AppSettings 
            { 
                Fullscreen = _isFullscreen,
                StartWithWindows = StartupToggle.IsChecked == true,
                MinimizeToTray = _minimizeToTray,
                Language = Loc.CurrentLanguage
            };
            var json = JsonSerializer.Serialize(settings);
            System.IO.File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }
    
    private readonly Dictionary<string, string> _languageNames = new()
    {
        ["en-US"] = "English",
        ["es"] = "Español",
        ["pt-BR"] = "Português",
        ["fr"] = "Français",
        ["de"] = "Deutsch",
        ["it"] = "Italiano",
        ["ko"] = "한국어",
        ["ja"] = "日本語",
        ["zh-CN"] = "中文",
        ["hi"] = "हिन्दी"
    };
    
    private void PopulateLanguagePanel()
    {
        LanguagePanel.Children.Clear();
        var currentLang = Loc.CurrentLanguage;
        
        foreach (var (code, name) in _languageNames)
        {
            var isSelected = code == currentLang;
            
            var row = new Grid
            {
                Background = new SolidColorBrush(Colors.Transparent),
                Padding = new Thickness(4, 6, 4, 6)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var textBlock = new TextBlock
            {
                Text = name,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(textBlock, 0);
            row.Children.Add(textBlock);
            
            var checkBox = new CheckBox
            {
                Tag = code,
                IsChecked = isSelected,
                MinWidth = 0,
                Padding = new Thickness(0)
            };
            checkBox.Checked += LanguageCheckBox_Changed;
            checkBox.Unchecked += LanguageCheckBox_Changed;
            // Add Enter key support (XYFocusKeyboardNavigation handles arrows)
            checkBox.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    checkBox.IsChecked = !checkBox.IsChecked;
                    e.Handled = true;
                }
            };
            Grid.SetColumn(checkBox, 1);
            row.Children.Add(checkBox);
            
            // Hover effect
            row.PointerEntered += (s, e) =>
            {
                row.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 45, 45, 50));
            };
            row.PointerExited += (s, e) =>
            {
                row.Background = new SolidColorBrush(Colors.Transparent);
            };
            
            // Make entire row clickable
            row.PointerPressed += (s, e) =>
            {
                checkBox.IsChecked = !checkBox.IsChecked;
                e.Handled = true;
            };
            
            LanguagePanel.Children.Add(row);
        }
    }
    
    private void UpdateLanguageCheckboxes(string newLangCode)
    {
        // Update checkboxes in place without rebuilding (preserves animation)
        foreach (var child in LanguagePanel.Children)
        {
            if (child is Grid row)
            {
                foreach (var rowChild in row.Children)
                {
                    if (rowChild is CheckBox cb && cb.Tag is string code)
                    {
                        // Temporarily remove handler to prevent recursion
                        cb.Checked -= LanguageCheckBox_Changed;
                        cb.Unchecked -= LanguageCheckBox_Changed;
                        
                        cb.IsChecked = (code == newLangCode);
                        
                        // Re-add handler
                        cb.Checked += LanguageCheckBox_Changed;
                        cb.Unchecked += LanguageCheckBox_Changed;
                    }
                }
            }
        }
    }
    
    private void LanguageCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is string langCode && cb.IsChecked == true)
        {
            Loc.SetLanguage(langCode);
            ApplyLocalizedStrings();
            UpdateLanguageCheckboxes(langCode); // Update in place, no rebuild
            SaveAppSettings();
            // Don't close flyouts - let user click off to close
        }
        else if (sender is CheckBox cb2 && cb2.IsChecked == false)
        {
            // Prevent unchecking current language - re-check it
            cb2.Checked -= LanguageCheckBox_Changed;
            cb2.IsChecked = true;
            cb2.Checked += LanguageCheckBox_Changed;
        }
    }
    
    private void LanguageItem_Click(object sender, RoutedEventArgs e)
    {
        // No longer used - kept for compatibility
    }
    
    private void LanguageFlyout_Closed(object sender, object e)
    {
        // Close settings flyout when language flyout closes (unless clicking on settings panel)
        SettingsFlyout.Hide();
    }
    
    public void SetFullscreen(bool fullscreen)
    {
        if (_appWindow == null) return;
        
        _isFullscreen = fullscreen;
        
        if (fullscreen)
        {
            _appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
        }
        else
        {
            _appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
        }
    }
    
    private void AppWindow_Changed(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
    {
        if (args.DidPresenterChange)
        {
            var isFullscreen = _appWindow?.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen;
            
            // If user exited fullscreen (e.g., pressed Escape or restore), update toggle
            if (_isFullscreen && !isFullscreen)
            {
                _isFullscreen = false;
                _isLoadingSettings = true;
                FullscreenToggle.IsChecked = false;
                _isLoadingSettings = false;
                SaveAppSettings();
            }
        }
    }
    
    private void Content_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && _isFullscreen)
        {
            SetFullscreen(false);
            _isLoadingSettings = true;
            FullscreenToggle.IsChecked = false;
            _isLoadingSettings = false;
            SaveAppSettings();
            e.Handled = true;
        }
    }
    
    private void FullscreenRow_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        FullscreenToggle.IsChecked = !FullscreenToggle.IsChecked;
        e.Handled = true;
    }
    
    private void FullscreenToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;
        
        SetFullscreen(FullscreenToggle.IsChecked == true);
        SaveAppSettings();
    }
    
    private void SettingsCheckbox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is CheckBox checkbox)
        {
            checkbox.IsChecked = !checkbox.IsChecked;
            e.Handled = true;
        }
    }
    
    private void StartupRow_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        StartupToggle.IsChecked = !StartupToggle.IsChecked;
        e.Handled = true;
    }
    
    private void StartupToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;
        
        SetStartWithWindows(StartupToggle.IsChecked == true);
        SaveAppSettings();
    }
    
    private void SetStartWithWindows(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            
            if (key == null) return;
            
            if (enabled)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue("Panomi", $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue("Panomi", false);
            }
        }
        catch { }
    }
    
    private void MinimizeToTrayRow_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        MinimizeToTrayToggle.IsChecked = !MinimizeToTrayToggle.IsChecked;
        e.Handled = true;
    }
    
    private void QuickLaunchToggle_GotFocus(object sender, RoutedEventArgs e)
    {
        QuickLaunchTooltip.IsOpen = true;
    }
    
    private void QuickLaunchToggle_LostFocus(object sender, RoutedEventArgs e)
    {
        QuickLaunchTooltip.IsOpen = false;
    }
    
    private void MinimizeToTrayToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;
        
        _minimizeToTray = MinimizeToTrayToggle.IsChecked == true;
        
        // Create or dispose tray icon based on setting
        if (_minimizeToTray)
        {
            if (_trayIcon == null)
            {
                InitializeTrayIcon();
            }
        }
        else
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
        }
        
        SaveAppSettings();
    }
    
    private void InitializeTrayIcon()
    {
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "logo_ico.ico");
        
        _trayIcon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri(iconPath))
        };
        
        // Create context menu
        var contextMenu = new MenuFlyout();
        
        var showItem = new MenuFlyoutItem { Text = "Show" };
        showItem.Click += (s, e) => ShowFromTray();
        contextMenu.Items.Add(showItem);
        
        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (s, e) => ExitApplication();
        contextMenu.Items.Add(exitItem);
        
        _trayIcon.ContextFlyout = contextMenu;
        
        // Double-click to show
        _trayIcon.LeftClickCommand = new RelayCommand(ShowFromTray);
    }
    
    private class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
    
    public void ShowFromTray()
    {
        // Show and activate window
        _appWindow?.Show();
        this.Activate();
    }
    
    private void ExitApplication()
    {
        _isReallyClosing = true;
        _trayIcon?.Dispose();
        this.Close();
    }
    
    private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_minimizeToTray && !_isReallyClosing)
        {
            // Cancel close and hide to tray instead
            args.Cancel = true;
            _appWindow?.Hide();
        }
        else
        {
            // Clean up tray icon on real close
            _trayIcon?.Dispose();
        }
    }
    
    // Discord and Spotify paths are cached for quick access
    private string? _discordPath;
    private string? _spotifyPath;
    
    private void LoadDiscordIcon()
    {
        _discordPath = IconExtractor.GetDiscordExePath();
        if (_discordPath != null)
        {
            var iconPath = IconExtractor.GetDiscordIconPath();
            var icon = IconExtractor.ExtractIconFromExe(iconPath);
            if (icon != null)
            {
                DiscordIcon.Source = icon;
            }
        }
        // Always show button - will redirect to website if not installed
    }
    
    private void UpdateCustomScrollBar()
    {
        // Calculate thumb size and position based on content
        var scrollableHeight = MainScrollViewer.ScrollableHeight;
        var viewportHeight = MainScrollViewer.ViewportHeight;
        var verticalOffset = MainScrollViewer.VerticalOffset;
        
        if (scrollableHeight > 0 && viewportHeight > 0)
        {
            // Calculate thumb height (proportional to viewport/total content ratio)
            var totalHeight = scrollableHeight + viewportHeight;
            var trackHeight = ScrollThumb.Parent is Grid parent ? parent.ActualHeight - 2 : 200;
            var thumbHeight = Math.Max(40, (viewportHeight / totalHeight) * trackHeight);
            
            // Calculate thumb position
            var scrollRatio = verticalOffset / scrollableHeight;
            var thumbTop = scrollRatio * (trackHeight - thumbHeight);
            
            ScrollThumb.Height = thumbHeight;
            ScrollThumb.Margin = new Thickness(1, thumbTop, 1, 0);
            ScrollThumb.Visibility = Visibility.Visible;
        }
        else
        {
            ScrollThumb.Visibility = Visibility.Collapsed;
        }
    }
    
    private void MainScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (!_isDraggingThumb)
        {
            UpdateCustomScrollBar();
        }
    }
    
    private void ScrollThumb_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _isDraggingThumb = true;
        _dragStartY = e.GetCurrentPoint(ScrollTrack).Position.Y;
        _dragStartScrollOffset = MainScrollViewer.VerticalOffset;
        ScrollThumb.CapturePointer(e.Pointer);
        e.Handled = true;
    }
    
    private void ScrollThumb_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isDraggingThumb)
        {
            var currentY = e.GetCurrentPoint(ScrollTrack).Position.Y;
            var deltaY = currentY - _dragStartY;
            
            // Convert pixel delta to scroll delta
            var trackHeight = ScrollTrack.ActualHeight;
            var thumbHeight = ScrollThumb.Height;
            var scrollableTrack = trackHeight - thumbHeight;
            
            if (scrollableTrack > 0)
            {
                var scrollRatio = deltaY / scrollableTrack;
                var newOffset = _dragStartScrollOffset + (scrollRatio * MainScrollViewer.ScrollableHeight);
                newOffset = Math.Max(0, Math.Min(newOffset, MainScrollViewer.ScrollableHeight));
                MainScrollViewer.ChangeView(null, newOffset, null, true);
                UpdateCustomScrollBar();
            }
            e.Handled = true;
        }
    }
    
    private void ScrollThumb_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isDraggingThumb)
        {
            _isDraggingThumb = false;
            ScrollThumb.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
    }
    
    private void ScrollTrack_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Click on track to jump to position
        var clickY = e.GetCurrentPoint(ScrollTrack).Position.Y;
        var trackHeight = ScrollTrack.ActualHeight;
        var thumbHeight = ScrollThumb.Height;
        
        // Calculate target scroll position
        var clickRatio = clickY / trackHeight;
        var newOffset = clickRatio * MainScrollViewer.ScrollableHeight;
        newOffset = Math.Max(0, Math.Min(newOffset, MainScrollViewer.ScrollableHeight));
        MainScrollViewer.ChangeView(null, newOffset, null, false);
        e.Handled = true;
    }

    private async Task LoadLibraryAsync()
    {
        _allItems.Clear();
        _filteredItems.Clear();
        _launcherIcons.Clear();

        // Load launchers and extract their icons
        var launchers = await _launcherService.GetAllLaunchersAsync();
        foreach (var launcher in launchers.Where(l => l.Type != LauncherType.Manual && l.IsInstalled))
        {
            // Extract icon from launcher exe if not already cached
            if (!_launcherIcons.ContainsKey(launcher.Type))
            {
                var exePath = IconExtractor.GetLauncherExePath(launcher.Type, launcher.InstallPath);
                _launcherIcons[launcher.Type] = IconExtractor.ExtractIconFromExe(exePath);
            }

            var item = new LibraryItem
            {
                Id = launcher.Id,
                Name = launcher.Name,
                TypeLabel = Loc.Get("TypeLauncher"),
                LaunchText = Loc.Get("LaunchButton"),
                IsLauncher = true,
                LaunchCommand = GetLauncherCommand(launcher.Type),
                LauncherName = launcher.Name,
                LauncherIcon = _launcherIcons.GetValueOrDefault(launcher.Type)
            };
            item.IsFavorite = _favorites.Contains(item.UniqueKey);
            _allItems.Add(item);
        }

        // Load games and collect launcher names
        var games = await _gameService.GetAllGamesAsync();
        var launcherNames = new HashSet<string>();
        var launcherNameToIcon = new Dictionary<string, BitmapImage?>();
        
        // Add all installed launchers to filter (even if they have no games)
        foreach (var launcher in launchers.Where(l => l.Type != LauncherType.Manual && l.IsInstalled))
        {
            launcherNames.Add(launcher.Name);
            if (!launcherNameToIcon.ContainsKey(launcher.Name))
                launcherNameToIcon[launcher.Name] = _launcherIcons.GetValueOrDefault(launcher.Type);
        }
        
        foreach (var game in games)
        {
            var launcherName = game.Launcher?.Name ?? "Unknown";
            launcherNames.Add(launcherName);
            var launcherType = game.Launcher?.Type ?? LauncherType.Manual;
            
            // Store name-to-icon mapping for filter dropdown
            if (!launcherNameToIcon.ContainsKey(launcherName))
                launcherNameToIcon[launcherName] = _launcherIcons.GetValueOrDefault(launcherType);
            
            // Load game icon if available
            BitmapImage? gameIcon = null;
            if (!string.IsNullOrEmpty(game.IconPath) && System.IO.File.Exists(game.IconPath))
            {
                try
                {
                    gameIcon = new BitmapImage(new Uri(game.IconPath));
                }
                catch { }
            }
            
            var item = new LibraryItem
            {
                Id = game.Id,
                Name = game.Name,
                TypeLabel = Loc.Get("TypeGame"),
                LaunchText = Loc.Get("LaunchButton"),
                IsLauncher = false,
                LaunchCommand = game.LaunchCommand ?? game.ExecutablePath,
                LauncherName = launcherName,
                LauncherIcon = _launcherIcons.GetValueOrDefault(launcherType),
                GameIcon = gameIcon
            };
            item.IsFavorite = _favorites.Contains(item.UniqueKey);
            _allItems.Add(item);
        }
        
        // Populate launcher filter checkboxes
        SourceFilterPanel.Children.Clear();
        
        // Add "Reset filters" button at top
        var resetButton = new Button
        {
            Content = Loc.Get("ResetFilters"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 10, 25, 47)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(6)
        };
        resetButton.Click += ResetFilters_Click;
        SourceFilterPanel.Children.Add(resetButton);
        
        // Add separator
        var separator = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 42, 42, 47)),
            Margin = new Thickness(0, 4, 0, 4)
        };
        SourceFilterPanel.Children.Add(separator);
        
        foreach (var name in launcherNames.OrderBy(n => n))
        {
            AddFilterItem(name, launcherNameToIcon.GetValueOrDefault(name));
        }
        
        // Load saved filter settings
        LoadFilterSettings(launcherNames);
        
        _isInitialized = true;
        ApplyFilter();
        UpdateStats();
    }
    
    private void AddFilterItem(string name, BitmapImage? icon)
    {
        var grid = new Grid
        {
            MinWidth = 150,
            Background = new SolidColorBrush(Colors.Transparent),
            Padding = new Thickness(4, 6, 4, 6),
            ColumnDefinitions = {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        
        // Icon
        if (icon != null)
        {
            var iconImage = new Image
            {
                Source = icon,
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconImage, 0);
            grid.Children.Add(iconImage);
        }
        else
        {
            // Fallback to Panomi logo for launchers without icons
            var logoImage = new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///Assets/logo_png.png")),
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(logoImage, 0);
            grid.Children.Add(logoImage);
        }
        
        var textBlock = new TextBlock
        {
            Text = name,
            Foreground = new SolidColorBrush(Colors.White),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(textBlock, 1);
        
        var checkbox = new CheckBox
        {
            Tag = name,
            IsChecked = false,
            MinWidth = 0,
            Padding = new Thickness(0)
        };
        checkbox.Checked += LauncherCheckbox_Changed;
        checkbox.Unchecked += LauncherCheckbox_Changed;
        // Add Enter key support (XYFocusKeyboardNavigation handles arrows)
        checkbox.KeyDown += (s, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                checkbox.IsChecked = !checkbox.IsChecked;
                e.Handled = true;
            }
        };
        Grid.SetColumn(checkbox, 2);
        
        // Make entire row clickable
        grid.PointerPressed += (s, e) =>
        {
            checkbox.IsChecked = !checkbox.IsChecked;
            e.Handled = true;
        };
        
        grid.Children.Add(textBlock);
        grid.Children.Add(checkbox);
        SourceFilterPanel.Children.Add(grid);
    }

    private static string? GetLauncherCommand(LauncherType type)
    {
        return type switch
        {
            LauncherType.Steam => "steam://open/games",
            LauncherType.EpicGames => "com.epicgames.launcher://",
            LauncherType.EAApp => "origin://",
            LauncherType.UbisoftConnect => "uplay://",
            LauncherType.GOGGalaxy => @"C:\Program Files (x86)\GOG Galaxy\GalaxyClient.exe",
            LauncherType.BattleNet => @"C:\Program Files (x86)\Battle.net\Battle.net Launcher.exe",
            LauncherType.RockstarGames => @"C:\Program Files\Rockstar Games\Launcher\Launcher.exe",
            LauncherType.RiotGames => @"C:\Riot Games\Riot Client\RiotClientServices.exe",
            LauncherType.Roblox => "roblox://",
            LauncherType.Minecraft => "shell:AppsFolder\\Microsoft.4297127D64EC6_8wekyb3d8bbwe!Minecraft",
            _ => null
        };
    }

    private void ApplyFilter()
    {
        if (!_isInitialized) return;
        
        _filteredItems.Clear();
        
        var searchText = SearchBox?.Text?.ToLowerInvariant() ?? "";
        var noSourceFilter = _selectedLaunchers.Count == 0;
        
        foreach (var item in _allItems)
        {
            // Apply type filter
            if (_currentFilter == FilterType.Launchers && !item.IsLauncher) continue;
            if (_currentFilter == FilterType.Games && item.IsLauncher) continue;
            
            // Apply favorites filter (independent toggle)
            if (_showFavoritesOnly && !item.IsFavorite) continue;
            
            // Apply launcher source filter (skip if none selected = show all)
            if (!noSourceFilter)
            {
                if (item.LauncherName == null || !_selectedLaunchers.Contains(item.LauncherName)) continue;
            }
            
            // Apply search filter
            if (!string.IsNullOrEmpty(searchText) && !item.Name.ToLowerInvariant().Contains(searchText)) continue;
            
            _filteredItems.Add(item);
        }
        
        // Sort: Fav launchers > Fav games > Regular launchers > Regular games (alphabetically within each)
        var sorted = _filteredItems
            .OrderByDescending(i => i.IsFavorite)
            .ThenByDescending(i => i.IsLauncher)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        
        _filteredItems.Clear();
        foreach (var item in sorted)
        {
            _filteredItems.Add(item);
        }
        
        // Update Source button style (cyan if filtering)
        UpdateSourceButtonStyle();
        
        EmptyState.Visibility = _filteredItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateStats()
    {
        var launcherCount = _allItems.Count(i => i.IsLauncher);
        var gameCount = _allItems.Count(i => !i.IsLauncher);
        
        TotalCount.Text = _allItems.Count.ToString();
        LauncherCount.Text = launcherCount.ToString();
        GameCount.Text = gameCount.ToString();
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        // Reset all filter states
        ResetFilterStates();
        
        ScanButton.IsEnabled = false;

        try
        {
            await _launcherService.DetectInstalledLaunchersAsync();
            await _gameService.ScanAllLaunchersAsync();
            await LoadLibraryAsync();
            
            // Refresh utility app detection (in case apps were installed)
            _discordPath = IconExtractor.GetDiscordExePath();
            _spotifyPath = IconExtractor.GetSpotifyExePath();
            LoadDiscordIcon();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Scan error: {ex}");
        }
        finally
        {
            ScanButton.IsEnabled = true;
        }
    }

    private void DiscordButton_Click(object sender, RoutedEventArgs e)
    {
        // Check cache first, then try fresh detection
        _discordPath ??= IconExtractor.GetDiscordExePath();
        
        if (_discordPath == null)
        {
            // Not installed - open Discord website
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://discord.com",
                    UseShellExecute = true
                });
            }
            catch { }
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _discordPath,
                Arguments = "--processStart Discord.exe",
                UseShellExecute = true
            });
        }
        catch
        {
            // Failed to launch - open website instead
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://discord.com",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    private void SpotifyButton_Click(object sender, RoutedEventArgs e)
    {
        // Check cache first, then try fresh detection
        _spotifyPath ??= IconExtractor.GetSpotifyExePath();
        
        if (_spotifyPath == null)
        {
            // Not installed - open Spotify website
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://open.spotify.com",
                    UseShellExecute = true
                });
            }
            catch { }
            return;
        }

        try
        {
            if (_spotifyPath.StartsWith("shell:"))
            {
                // Store app - launch via explorer
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = _spotifyPath,
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _spotifyPath,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Failed to launch - open website instead
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://open.spotify.com",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    private void WebLink_Click(object sender, RoutedEventArgs e)
    {
        string? url = null;
        
        if (sender is Button button && button.Tag is string buttonUrl)
        {
            url = buttonUrl;
        }
        else if (sender is MenuFlyoutItem menuItem && menuItem.Tag is string menuUrl)
        {
            url = menuUrl;
        }
        
        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Silently fail
            }
        }
    }
    
    private void OpenDefaultBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Opens the user's default browser to their homepage
            Process.Start(new ProcessStartInfo
            {
                FileName = "about:blank",
                UseShellExecute = true
            });
        }
        catch
        {
            // Silently fail
        }
    }
    
    private void ResetFilterStates()
    {
        _showFavoritesOnly = false;
        UpdateFavoritesButtonStyle();
        
        _selectedLaunchers.Clear();
        foreach (var child in SourceFilterPanel.Children)
        {
            if (child is Grid grid)
            {
                foreach (var gridChild in grid.Children)
                {
                    if (gridChild is CheckBox checkbox)
                    {
                        checkbox.IsChecked = false;
                    }
                }
            }
        }
        UpdateSourceButtonStyle();
        
        SetFilter(FilterType.All);
        SaveFilterSettings();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }
    
    private void ResetFilters_Click(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        
        // Uncheck all launcher checkboxes
        _selectedLaunchers.Clear();
        foreach (var child in SourceFilterPanel.Children)
        {
            if (child is Grid grid)
            {
                foreach (var gridChild in grid.Children)
                {
                    if (gridChild is CheckBox cb)
                    {
                        cb.IsChecked = false;
                    }
                }
            }
        }
        
        ApplyFilter();
        SaveFilterSettings();
    }
    
    private void LauncherCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        
        if (sender is CheckBox checkbox && checkbox.Tag is string launcherName)
        {
            if (checkbox.IsChecked == true)
            {
                _selectedLaunchers.Add(launcherName);
            }
            else
            {
                _selectedLaunchers.Remove(launcherName);
            }
            
            ApplyFilter();
            SaveFilterSettings();
        }
    }
    
    private void SaveFilterSettings()
    {
        try
        {
            var settings = new FilterSettings
            {
                FilterType = _currentFilter.ToString(),
                SelectedLaunchers = _selectedLaunchers.ToList(),
                ShowFavoritesOnly = _showFavoritesOnly
            };
            var json = JsonSerializer.Serialize(settings);
            System.IO.File.WriteAllText(_filterSettingsPath, json);
        }
        catch { /* Ignore save errors */ }
    }
    
    private void LoadFilterSettings(HashSet<string> availableLaunchers)
    {
        try
        {
            if (!System.IO.File.Exists(_filterSettingsPath)) return;
            
            var json = System.IO.File.ReadAllText(_filterSettingsPath);
            var settings = JsonSerializer.Deserialize<FilterSettings>(json);
            if (settings == null) return;
            
            // Restore filter type
            if (Enum.TryParse<FilterType>(settings.FilterType, out var filterType))
            {
                _currentFilter = filterType;
                SetFilter(filterType);
            }
            
            // Restore favorites filter
            _showFavoritesOnly = settings.ShowFavoritesOnly;
            UpdateFavoritesButtonStyle();
            
            // Restore selected launchers (only those that still exist)
            _selectedLaunchers.Clear();
            foreach (var name in settings.SelectedLaunchers)
            {
                if (availableLaunchers.Contains(name))
                    _selectedLaunchers.Add(name);
            }
            
            // Update checkboxes to match
            foreach (var child in SourceFilterPanel.Children)
            {
                if (child is Grid grid)
                {
                    foreach (var gridChild in grid.Children)
                    {
                        if (gridChild is CheckBox checkbox && checkbox.Tag is string launcherName)
                        {
                            checkbox.IsChecked = _selectedLaunchers.Contains(launcherName);
                        }
                    }
                }
            }
        }
        catch { /* Ignore load errors, use defaults */ }
    }
    
    private void UpdateSourceButtonStyle()
    {
        var hasFilter = _selectedLaunchers.Count > 0;
        var cyanStyle = (Style)Application.Current.Resources["CyanPillButtonStyle"];
        var darkStyle = (Style)Application.Current.Resources["DarkPillButtonStyle"];
        
        SourceFilterButton.Style = hasFilter ? cyanStyle : darkStyle;
    }

    private void FilterAll_Click(object sender, RoutedEventArgs e)
    {
        SetFilter(FilterType.All);
    }

    private void FilterLaunchers_Click(object sender, RoutedEventArgs e)
    {
        SetFilter(FilterType.Launchers);
    }

    private void FilterGames_Click(object sender, RoutedEventArgs e)
    {
        SetFilter(FilterType.Games);
    }

    private void FilterFavorites_Click(object sender, RoutedEventArgs e)
    {
        _showFavoritesOnly = !_showFavoritesOnly;
        UpdateFavoritesButtonStyle();
        ApplyFilter();
        SaveFilterSettings();
    }
    
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // Flyout opens automatically, no action needed
    }
    
    private void UpdateSettingsButtonStyle()
    {
        // No longer used - settings uses flyout like filter dropdown
    }
    
    private void UpdateFavoritesButtonStyle()
    {
        var cyanStyle = (Style)Application.Current.Resources["CyanPillButtonStyle"];
        var darkStyle = (Style)Application.Current.Resources["DarkPillButtonStyle"];
        
        FilterFavorites.Style = _showFavoritesOnly ? cyanStyle : darkStyle;
        
        // Keep icon white always
        FavoritesIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
    }

    private void SetFilter(FilterType filter)
    {
        _currentFilter = filter;
        
        var cyanStyle = (Style)Application.Current.Resources["CyanPillButtonStyle"];
        var darkStyle = (Style)Application.Current.Resources["DarkPillButtonStyle"];
        
        FilterAll.Style = filter == FilterType.All ? cyanStyle : darkStyle;
        FilterLaunchers.Style = filter == FilterType.Launchers ? cyanStyle : darkStyle;
        FilterGames.Style = filter == FilterType.Games ? cyanStyle : darkStyle;
        
        ApplyFilter();
        SaveFilterSettings();
    }

    private async void LaunchItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is LibraryItem item)
        {
            // Visual feedback: change to "Launching..." with cyan style
            // Update item.LaunchText to preserve binding (so language changes work)
            var originalStyle = button.Style;
            item.LaunchText = Loc.Get("LaunchingButton");
            button.Style = (Style)Application.Current.Resources["CyanPillButtonStyle"];
            button.IsEnabled = false;
            _launchingButton = item; // Track item for language updates
            
            try
            {
                if (item.IsLauncher)
                {
                    // Launch the launcher
                    if (LaunchValidator.IsValidLaunchCommand(item.LaunchCommand))
                    {
                        // Handle shell: protocol for Store apps (e.g., Minecraft)
                        if (item.LaunchCommand?.StartsWith("shell:") == true)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = item.LaunchCommand,
                                UseShellExecute = true
                            });
                        }
                        else
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = item.LaunchCommand,
                                UseShellExecute = true
                            });
                        }
                    }
                }
                else
                {
                    // Launch the game (errors handled silently)
                    await _gameService.TryLaunchGameAsync(item.Id);
                }
                
                // Delay so user sees "Launching..." feedback while app opens
                await Task.Delay(3000);
            }
            catch { }
            finally
            {
                // Restore button - update LaunchText to preserve binding
                _launchingButton = null;
                item.LaunchText = Loc.Get("LaunchButton");
                button.Style = originalStyle;
                button.IsEnabled = true;
                
                // Restore focus to the button
                button.Focus(FocusState.Keyboard);
            }
        }
    }
    
    private void FavoriteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is LibraryItem item)
        {
            item.IsFavorite = !item.IsFavorite;
            
            if (item.IsFavorite)
                _favorites.Add(item.UniqueKey);
            else
                _favorites.Remove(item.UniqueKey);
            
            SaveFavorites();
            
            // Only re-filter if showing favorites only and we're removing a favorite
            // Otherwise just update the icon without losing focus
            if (_showFavoritesOnly && !item.IsFavorite)
            {
                ApplyFilter();
            }
            
            // Keep focus on the button
            button.Focus(FocusState.Keyboard);
        }
    }
    
    private void LoadFavorites()
    {
        try
        {
            if (!System.IO.File.Exists(_favoritesSettingsPath)) return;
            
            var json = System.IO.File.ReadAllText(_favoritesSettingsPath);
            var settings = JsonSerializer.Deserialize<FavoritesSettings>(json);
            if (settings?.FavoriteKeys != null)
            {
                _favorites = new HashSet<string>(settings.FavoriteKeys);
            }
        }
        catch { /* Ignore load errors */ }
    }
    
    private void SaveFavorites()
    {
        try
        {
            var settings = new FavoritesSettings
            {
                FavoriteKeys = _favorites.ToList()
            };
            var json = JsonSerializer.Serialize(settings);
            System.IO.File.WriteAllText(_favoritesSettingsPath, json);
        }
        catch { /* Ignore save errors */ }
    }
    
    // Hover handlers for cyan buttons
    private void CyanButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            var isCyan = button.Background is SolidColorBrush brush && 
                         brush.Color == Color.FromArgb(255, 0, 229, 255);
            if (isCyan)
            {
                button.Background = new SolidColorBrush(Color.FromArgb(255, 0, 240, 200)); // #00F0C8
            }
        }
    }
    
    private void CyanButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            var isHoverCyan = button.Background is SolidColorBrush brush && 
                              brush.Color == Color.FromArgb(255, 0, 240, 200);
            if (isHoverCyan)
            {
                button.Background = new SolidColorBrush(Color.FromArgb(255, 0, 229, 255)); // #00E5FF
            }
        }
    }
    
    private void SettingsButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        // Handled by style
    }
    
    private void SettingsButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        // Handled by style
    }
}

public class FilterSettings
{
    public string FilterType { get; set; } = "All";
    public List<string> SelectedLaunchers { get; set; } = new();
    public bool ShowFavoritesOnly { get; set; } = false;
}

public class FavoritesSettings
{
    public List<string> FavoriteKeys { get; set; } = new();
}

public class LibraryItem : System.ComponentModel.INotifyPropertyChanged
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    
    private string _launchText = "Launch";
    public string LaunchText
    {
        get => _launchText;
        set
        {
            if (_launchText != value)
            {
                _launchText = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(LaunchText)));
            }
        }
    }
    
    public bool IsLauncher { get; set; }

    public string? LaunchCommand { get; set; }
    public string? LauncherName { get; set; }
    public Microsoft.UI.Xaml.Media.ImageSource? LauncherIcon { get; set; }
    public Microsoft.UI.Xaml.Media.ImageSource? GameIcon { get; set; }
    public string? TitleGlyph { get; set; } // Optional glyph icon to show before title
    public bool HasTitleGlyph => !string.IsNullOrEmpty(TitleGlyph);
    
    // Icon to show next to name: LauncherIcon for launchers, GameIcon for games
    public Microsoft.UI.Xaml.Media.ImageSource? ItemIcon => IsLauncher ? LauncherIcon : GameIcon;
    public bool HasItemIcon => ItemIcon != null;
    
    // For launcher icon visibility - only show if launcher AND has icon
    public bool HasLauncherIcon => IsLauncher && LauncherIcon != null;
    
    // Show Panomi logo fallback for items without icons
    public bool ShowFallbackIcon => IsLauncher ? LauncherIcon == null : GameIcon == null;
    
    private bool _isFavorite;
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsFavorite)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(FavoriteIcon)));
            }
        }
    }
    
    public string FavoriteIcon => IsFavorite ? "\uE735" : "\uE734";
    
    // LauncherGame items use "LG" prefix for unique key
    public string UniqueKey => $"{(IsLauncher ? "L" : "G")}_{Name}";
    
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

public enum FilterType
{
    All,
    Launchers,
    Games
}

public class AppSettings
{
    public bool Fullscreen { get; set; }
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; }
    public string Language { get; set; } = "en-US";
}
