namespace Panomi.UI.Helpers;

/// <summary>
/// Simple localization helper - just dictionaries of strings.
/// </summary>
public static class Loc
{
    private static string _currentLanguage = "en-US";
    
    public static string CurrentLanguage => _currentLanguage;
    
    private static readonly Dictionary<string, Dictionary<string, string>> _strings = new()
    {
        ["en-US"] = new()
        {
            ["ScanButton"] = "Scan Launchers & Games",
            ["LaunchButton"] = "Launch",
            ["LaunchingButton"] = "Launching...",
            ["Library"] = "Library",
            ["FilterAll"] = "All",
            ["FilterLaunchers"] = "Launchers",
            ["FilterGames"] = "Games",
            ["FilterButton"] = "Filter",
            ["ResetFilters"] = "Reset filters",
            ["SearchPlaceholder"] = "Search games...",
            ["TypeLauncher"] = "Launcher",
            ["TypeGame"] = "Game",
            ["FullscreenMode"] = "Fullscreen",
            ["StartWithWindows"] = "Start with Windows",
            ["MinimizeToTray"] = "Quick Launch",
            ["QuickLaunchInfo"] = "Keeps the app running so it opens instantly",
            ["LanguageSetting"] = "Language",
            ["StatTotal"] = "Total",
            ["StatLaunchers"] = "Launchers",
            ["StatGames"] = "Games",
            ["UpdateAvailable"] = "Update Available"
        },
        ["es"] = new()
        {
            ["ScanButton"] = "Buscar Launchers y Juegos",
            ["LaunchButton"] = "Iniciar",
            ["LaunchingButton"] = "Iniciando...",
            ["Library"] = "Biblioteca",
            ["FilterAll"] = "Todos",
            ["FilterLaunchers"] = "Launchers",
            ["FilterGames"] = "Juegos",
            ["FilterButton"] = "Filtrar",
            ["ResetFilters"] = "Reiniciar filtros",
            ["SearchPlaceholder"] = "Buscar...",
            ["TypeLauncher"] = "Launcher",
            ["TypeGame"] = "Juego",
            ["FullscreenMode"] = "Pantalla completa",
            ["StartWithWindows"] = "Iniciar con Windows",
            ["MinimizeToTray"] = "Inicio Rápido",
            ["QuickLaunchInfo"] = "Mantiene la app activa para abrir al instante",
            ["LanguageSetting"] = "Idioma",
            ["StatTotal"] = "Total",
            ["StatLaunchers"] = "Launchers",
            ["StatGames"] = "Juegos"
        },
        ["pt-BR"] = new()
        {
            ["ScanButton"] = "Buscar Launchers e Jogos",
            ["LaunchButton"] = "Iniciar",
            ["LaunchingButton"] = "Iniciando...",
            ["Library"] = "Biblioteca",
            ["FilterAll"] = "Todos",
            ["FilterLaunchers"] = "Launchers",
            ["FilterGames"] = "Jogos",
            ["FilterButton"] = "Filtrar",
            ["ResetFilters"] = "Resetar filtros",
            ["SearchPlaceholder"] = "Buscar...",
            ["TypeLauncher"] = "Launcher",
            ["TypeGame"] = "Jogo",
            ["FullscreenMode"] = "Tela cheia",
            ["StartWithWindows"] = "Iniciar com Windows",
            ["MinimizeToTray"] = "Início Rápido",
            ["QuickLaunchInfo"] = "Mantém o app ativo para abrir instantaneamente",
            ["LanguageSetting"] = "Idioma",
            ["StatTotal"] = "Total",
            ["StatLaunchers"] = "Launchers",
            ["StatGames"] = "Jogos"
        },
        ["fr"] = new()
        {
            ["ScanButton"] = "Rechercher Launchers et Jeux",
            ["LaunchButton"] = "Lancer",
            ["LaunchingButton"] = "Lancement...",
            ["Library"] = "Bibliothèque",
            ["FilterAll"] = "Tous",
            ["FilterLaunchers"] = "Launchers",
            ["FilterGames"] = "Jeux",
            ["FilterButton"] = "Filtrer",
            ["ResetFilters"] = "Réinitialiser filtres",
            ["SearchPlaceholder"] = "Rechercher...",
            ["TypeLauncher"] = "Launcher",
            ["TypeGame"] = "Jeu",
            ["FullscreenMode"] = "Plein écran",
            ["StartWithWindows"] = "Démarrer avec Windows",
            ["MinimizeToTray"] = "Lancement Rapide",
            ["QuickLaunchInfo"] = "Garde l'app active pour une ouverture instantanée",
            ["LanguageSetting"] = "Langue",
            ["StatTotal"] = "Total",
            ["StatLaunchers"] = "Launchers",
            ["StatGames"] = "Jeux"
        },
        ["de"] = new()
        {
            ["ScanButton"] = "Launcher und Spiele Suchen",
            ["LaunchButton"] = "Starten",
            ["LaunchingButton"] = "Starten...",
            ["Library"] = "Bibliothek",
            ["FilterAll"] = "Alle",
            ["FilterLaunchers"] = "Launcher",
            ["FilterGames"] = "Spiele",
            ["FilterButton"] = "Filter",
            ["ResetFilters"] = "Filter zurücksetzen",
            ["SearchPlaceholder"] = "Suchen...",
            ["TypeLauncher"] = "Launcher",
            ["TypeGame"] = "Spiel",
            ["FullscreenMode"] = "Vollbild",
            ["StartWithWindows"] = "Mit Windows starten",
            ["MinimizeToTray"] = "Schnellstart",
            ["QuickLaunchInfo"] = "Hält die App aktiv für sofortiges Öffnen",
            ["LanguageSetting"] = "Sprache",
            ["StatTotal"] = "Gesamt",
            ["StatLaunchers"] = "Launcher",
            ["StatGames"] = "Spiele"
        },
        ["it"] = new()
        {
            ["ScanButton"] = "Cerca Launcher e Giochi",
            ["LaunchButton"] = "Avvia",
            ["LaunchingButton"] = "Avvio...",
            ["Library"] = "Libreria",
            ["FilterAll"] = "Tutti",
            ["FilterLaunchers"] = "Launcher",
            ["FilterGames"] = "Giochi",
            ["FilterButton"] = "Filtra",
            ["ResetFilters"] = "Reimposta filtri",
            ["SearchPlaceholder"] = "Cerca...",
            ["TypeLauncher"] = "Launcher",
            ["TypeGame"] = "Gioco",
            ["FullscreenMode"] = "Schermo intero",
            ["StartWithWindows"] = "Avvia con Windows",
            ["MinimizeToTray"] = "Avvio Rapido",
            ["QuickLaunchInfo"] = "Mantiene l'app attiva per aprire istantaneamente",
            ["LanguageSetting"] = "Lingua",
            ["StatTotal"] = "Totale",
            ["StatLaunchers"] = "Launcher",
            ["StatGames"] = "Giochi"
        },
        ["ko"] = new()
        {
            ["ScanButton"] = "런처 및 게임 검색",
            ["LaunchButton"] = "실행",
            ["LaunchingButton"] = "실행 중...",
            ["Library"] = "라이브러리",
            ["FilterAll"] = "전체",
            ["FilterLaunchers"] = "런처",
            ["FilterGames"] = "게임",
            ["FilterButton"] = "필터",
            ["ResetFilters"] = "필터 초기화",
            ["SearchPlaceholder"] = "검색...",
            ["TypeLauncher"] = "런처",
            ["TypeGame"] = "게임",
            ["FullscreenMode"] = "전체 화면",
            ["StartWithWindows"] = "Windows 시작 시 실행",
            ["MinimizeToTray"] = "빠른 실행",
            ["QuickLaunchInfo"] = "앱을 활성 상태로 유지하여 즉시 열기",
            ["LanguageSetting"] = "언어",
            ["StatTotal"] = "전체",
            ["StatLaunchers"] = "런처",
            ["StatGames"] = "게임"
        },
        ["ja"] = new()
        {
            ["ScanButton"] = "ランチャーとゲームをスキャン",
            ["LaunchButton"] = "起動",
            ["LaunchingButton"] = "起動中...",
            ["Library"] = "ライブラリ",
            ["FilterAll"] = "すべて",
            ["FilterLaunchers"] = "ランチャー",
            ["FilterGames"] = "ゲーム",
            ["FilterButton"] = "フィルター",
            ["ResetFilters"] = "フィルターをリセット",
            ["SearchPlaceholder"] = "検索...",
            ["TypeLauncher"] = "ランチャー",
            ["TypeGame"] = "ゲーム",
            ["FullscreenMode"] = "フルスクリーン",
            ["StartWithWindows"] = "Windows起動時に開始",
            ["MinimizeToTray"] = "クイック起動",
            ["QuickLaunchInfo"] = "アプリを常駐させて即座に起動",
            ["LanguageSetting"] = "言語",
            ["StatTotal"] = "合計",
            ["StatLaunchers"] = "ランチャー",
            ["StatGames"] = "ゲーム"
        },
        ["zh-CN"] = new()
        {
            ["ScanButton"] = "扫描启动器和游戏",
            ["LaunchButton"] = "启动",
            ["LaunchingButton"] = "启动中...",
            ["Library"] = "游戏库",
            ["FilterAll"] = "全部",
            ["FilterLaunchers"] = "启动器",
            ["FilterGames"] = "游戏",
            ["FilterButton"] = "筛选",
            ["ResetFilters"] = "重置筛选",
            ["SearchPlaceholder"] = "搜索...",
            ["TypeLauncher"] = "启动器",
            ["TypeGame"] = "游戏",
            ["FullscreenMode"] = "全屏",
            ["StartWithWindows"] = "随Windows启动",
            ["MinimizeToTray"] = "快速启动",
            ["QuickLaunchInfo"] = "保持应用运行以便即时打开",
            ["LanguageSetting"] = "语言",
            ["StatTotal"] = "总计",
            ["StatLaunchers"] = "启动器",
            ["StatGames"] = "游戏"
        },
        ["hi"] = new()
        {
            ["ScanButton"] = "लॉन्चर और गेम खोजें",
            ["LaunchButton"] = "शुरू करें",
            ["LaunchingButton"] = "लॉन्च हो रहा है...",
            ["Library"] = "लाइब्रेरी",
            ["FilterAll"] = "सभी",
            ["FilterLaunchers"] = "लॉन्चर",
            ["FilterGames"] = "गेम्स",
            ["FilterButton"] = "फ़िल्टर",
            ["ResetFilters"] = "फ़िल्टर रीसेट",
            ["SearchPlaceholder"] = "खोजें...",
            ["TypeLauncher"] = "लॉन्चर",
            ["TypeGame"] = "गेम",
            ["FullscreenMode"] = "फ़ुल स्क्रीन",
            ["StartWithWindows"] = "Windows के साथ शुरू करें",
            ["MinimizeToTray"] = "त्वरित लॉन्च",
            ["QuickLaunchInfo"] = "ऐप को सक्रिय रखता है ताकि तुरंत खुले",
            ["LanguageSetting"] = "भाषा",
            ["StatTotal"] = "कुल",
            ["StatLaunchers"] = "लॉन्चर",
            ["StatGames"] = "गेम्स"
        }
    };
    
    public static void SetLanguage(string languageCode)
    {
        _currentLanguage = _strings.ContainsKey(languageCode) ? languageCode : "en-US";
    }
    
    public static string Get(string key)
    {
        if (_strings.TryGetValue(_currentLanguage, out var lang) && lang.TryGetValue(key, out var value))
            return value;
        if (_strings["en-US"].TryGetValue(key, out var fallback))
            return fallback;
        return key;
    }
}
