using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var repositoryRoot = args.Length > 0
            ? Path.GetFullPath(args[0])
            : FindRepositoryRoot(AppContext.BaseDirectory);
        var outputDirectory = args.Length > 1
            ? Path.GetFullPath(args[1])
            : Path.Combine(repositoryRoot, "artifacts", "ui-previews");
        Directory.CreateDirectory(outputDirectory);

        var app = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };

        ApplyAppResources(app, Path.Combine(repositoryRoot, "src", "SpeechTranslatorDesktop", "App.xaml"));

        var scenarios = new[]
        {
            CreateScenario("normal", "ja", 1100, 760, isEnglish: false, stressLayout: false),
            CreateScenario("normal", "ja", 1920, 1080, isEnglish: false, stressLayout: false),
            CreateScenario("stress", "ja", 1100, 760, isEnglish: false, stressLayout: true),
            CreateScenario("stress", "ja", 1920, 1080, isEnglish: false, stressLayout: true),
            CreateScenario("normal", "en", 1100, 760, isEnglish: true, stressLayout: false),
            CreateScenario("normal", "en", 1920, 1080, isEnglish: true, stressLayout: false),
            CreateScenario("stress", "en", 1100, 760, isEnglish: true, stressLayout: true),
            CreateScenario("stress", "en", 1920, 1080, isEnglish: true, stressLayout: true)
        };

        foreach (var scenario in scenarios)
        {
            var window = LoadMainWindow(Path.Combine(repositoryRoot, "src", "SpeechTranslatorDesktop", "MainWindow.xaml"));
            window.Width = scenario.Width;
            window.Height = scenario.Height;
            window.ShowInTaskbar = false;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -10000;
            window.Top = -10000;
            window.DataContext = scenario.ViewModel;
            window.Show();
            window.UpdateLayout();
            ValidateScenario(window, scenario);

            var bitmap = new RenderTargetBitmap(scenario.Width, scenario.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var outputPath = Path.Combine(
                outputDirectory,
                $"mainwindow-{scenario.Locale}-{scenario.Kind}-{scenario.Width}x{scenario.Height}.png");
            using var stream = File.Create(outputPath);
            encoder.Save(stream);
            Console.WriteLine(outputPath);
            window.Close();
        }

        app.Shutdown();
    }

    private static void ValidateScenario(Window window, PreviewScenario scenario)
    {
        if (Math.Abs(window.ActualHeight - scenario.Height) > 1)
        {
            throw new InvalidOperationException($"Window height changed unexpectedly. expected={scenario.Height}, actual={window.ActualHeight}");
        }

        var rootScrollViewer = window.FindName("RootScrollViewer") as ScrollViewer
            ?? throw new InvalidOperationException("RootScrollViewer was not found.");
        var translationLogListBox = window.FindName("TranslationLogListBox") as ListBox
            ?? throw new InvalidOperationException("TranslationLogListBox was not found.");
        var translationInnerScrollViewer = FindDescendant<ScrollViewer>(translationLogListBox)
            ?? throw new InvalidOperationException("Translation log internal ScrollViewer was not found.");

        if (scenario.TranslationLogCount >= 500 && translationInnerScrollViewer.ScrollableHeight <= 0)
        {
            throw new InvalidOperationException($"Translation log should scroll internally for {scenario.Locale}-{scenario.Kind}-{scenario.Width}x{scenario.Height}.");
        }

        var isNormalLargeViewport = scenario.Kind == "normal" && scenario.Width == 1920;
        if (isNormalLargeViewport && rootScrollViewer.ScrollableHeight > 0)
        {
            throw new InvalidOperationException($"Outer scroll should not appear for {scenario.Locale}-{scenario.Kind}-{scenario.Width}x{scenario.Height}.");
        }

        var isStressCompactViewport = scenario.Kind == "stress" && scenario.Width == 1100;
        if (isStressCompactViewport && rootScrollViewer.ScrollableHeight <= 0)
        {
            throw new InvalidOperationException($"Outer scroll should appear for {scenario.Locale}-{scenario.Kind}-{scenario.Width}x{scenario.Height}.");
        }
    }

    private static string FindRepositoryRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "speech-translator.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("speech-translator.sln が見つかりません。");
        }

        return directory.FullName;
    }
    
    private static void ApplyAppResources(Application application, string appXamlPath)
    {
        var appXaml = File.ReadAllText(appXamlPath);
        var document = XDocument.Parse(appXaml);
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;
        var resources = document.Root?.Element(ns + "Application.Resources");
        if (resources is null)
        {
            return;
        }

        foreach (var dictionaryElement in resources.Elements())
        {
            if (XamlReader.Parse(dictionaryElement.ToString()) is ResourceDictionary dictionary)
            {
                application.Resources.MergedDictionaries.Add(dictionary);
            }
        }
    }
    
    private static Window LoadMainWindow(string mainWindowXamlPath)
    {
        var xaml = File.ReadAllText(mainWindowXamlPath);
        xaml = Regex.Replace(xaml, "\\s+x:Class=\"[^\"]+\"", string.Empty);
        xaml = Regex.Replace(xaml, "\\s+Click=\"[^\"]+\"", string.Empty);
        xaml = Regex.Replace(xaml, "xmlns:local=\"clr-namespace:SpeechTranslatorDesktop.Behaviors\"",
            "xmlns:local=\"clr-namespace:SpeechTranslatorDesktop.Behaviors;assembly=SpeechTranslatorDesktopPlus\"");
        xaml = Regex.Replace(xaml, "\\s+Icon=\"[^\"]+\"", string.Empty);
        return (Window)XamlReader.Parse(xaml);
    }
    
    private static PreviewScenario CreateScenario(
        string kind,
        string locale,
        int width,
        int height,
        bool isEnglish,
        bool stressLayout)
    {
        var noOpCommand = new NoOpCommand();
        var translationLogs = BuildTranslationLogs(isEnglish);
        var statusDetail = isEnglish
            ? "Recognition worker could not reconnect after network interruption. The recording was saved successfully."
            : "ネットワーク中断後に認識ワーカーが再接続できませんでした。録音ファイルは保存済みです。";
        var updateMessage = isEnglish
            ? "Version 1.9.1 is available. This build contains UI fixes for compact status and improved action layout."
            : "バージョン 1.9.1 を利用できます。コンパクトなステータス表示と操作レイアウト改善を含みます。";

        return new PreviewScenario(
            kind,
            locale,
            width,
            height,
            translationLogs.Count,
            new PreviewMainWindowViewModel
        {
            MainWindowTitle = isEnglish ? "Speech Translator Desktop Plus" : "Speech Translator Desktop Plus",
            HeroSubtitle = isEnglish ? "Hands-free bilingual captions and logs" : "ハンズフリー字幕と翻訳ログ",
            SettingsButtonText = isEnglish ? "Settings" : "設定",
            RecognitionModeLabel = isEnglish ? "Mode" : "利用モード",
            SourceLanguageLabel = isEnglish ? "Source language" : "元言語",
            TargetLanguageLabel = isEnglish ? "Target language" : "翻訳先言語",
            AudioInputLabel = isEnglish ? "Audio input" : "音声入力",
            SaveRecordingLabel = isEnglish ? "Save recording" : "記録を保存する",
            RecordingFileNameLabel = isEnglish ? "File name prefix (optional)" : "ファイル名 prefix（任意）",
            RecordingFileName = isEnglish ? "meeting-2026-09-09" : "会議-2026-09-09",
            RecordingFileNamePreview = isEnglish
                ? "Example: meeting-2026-09-09_yyyyMMdd_HHmmss.txt (letters/numbers/spaces/-/_)"
                : "保存例: 会議-2026-09-09_yyyyMMdd_HHmmss.txt（文字/数字/スペース/-/_）",
            StartButtonText = isEnglish ? "Start" : "開始",
            StopButtonText = isEnglish ? "Stop" : "停止",
            ClearLogsButtonText = isEnglish ? "Clear logs" : "ログクリア",
            OpenLatestRecordingFolderButtonText = isEnglish ? "Open saved folder" : "保存フォルダーを開く",
            ShowRecentTranslationsButtonText = isEnglish ? "Open live notes window" : "別ウィンドウでライブノートを開く",
            StatusLabel = isEnglish ? "Status" : "状態",
            StatusBadgeText = isEnglish ? "● Stopped" : "● 停止",
            StatusDetailMessage = stressLayout ? statusDetail : string.Empty,
            StatusDetailVisibility = stressLayout ? Visibility.Visible : Visibility.Collapsed,
            UpdateStatusMessage = stressLayout ? updateMessage : string.Empty,
            UpdateBannerVisibility = stressLayout ? Visibility.Visible : Visibility.Collapsed,
            CheckForUpdatesButtonText = isEnglish ? "Check updates" : "更新を確認",
            UpdateActionButtonText = isEnglish ? "Restart to update" : "再起動して更新",
            TranslationLogHeader = isEnglish ? "Translation log" : "翻訳ログ",
            StatusLogHeader = isEnglish ? "Status log" : "状態ログ",
            CopyAllLogsButtonText = isEnglish ? "Copy all" : "すべてコピー",
            CopyAllSourceLogsButtonText = isEnglish ? "Copy all source" : "原文をすべてコピー",
            CopyAllTranslatedLogsButtonText = isEnglish ? "Copy all translations" : "訳文をすべてコピー",
            CopyBlockButtonText = isEnglish ? "Copy" : "コピー",
            CopySourceButtonText = isEnglish ? "Copy source" : "原文コピー",
            CopyTranslationButtonText = isEnglish ? "Copy translation" : "訳文コピー",
            SourceTextHeader = isEnglish ? "Source" : "原文",
            TranslatedTextHeader = isEnglish ? "Translated" : "翻訳",
            AvailableRecognitionModes = ["Real-time"],
            AvailableLanguages = ["ja-JP", "en-US"],
            AvailableAudioInputSources = ["Default Microphone"],
            SelectedRecognitionMode = "Real-time",
            SelectedSourceLanguage = "ja-JP",
            SelectedTargetLanguage = "en-US",
            SelectedAudioInputSource = "Default Microphone",
            TranslationColumnVisibility = Visibility.Visible,
            OpenLatestRecordingFolderButtonVisibility = stressLayout ? Visibility.Visible : Visibility.Collapsed,
            IsTranslationMode = true,
            IsRecordingSaveEnabled = true,
            IsTranslationLogExpanded = true,
            IsStatusLogExpanded = true,
            TranslationLogRowHeight = new GridLength(1, GridUnitType.Star),
            StartCommand = noOpCommand,
            StopCommand = noOpCommand,
            ClearLogsCommand = noOpCommand,
            OpenLatestRecordingFolderCommand = noOpCommand,
            CheckForUpdatesCommand = noOpCommand,
            ApplyUpdateCommand = noOpCommand,
            CopyAllLogsCommand = noOpCommand,
            CopyAllSourceLogsCommand = noOpCommand,
            CopyAllTranslatedLogsCommand = noOpCommand,
            CopySelectedTranslationLogCommand = noOpCommand,
            CopySelectedSourceTextCommand = noOpCommand,
            CopySelectedTranslatedTextCommand = noOpCommand,
            CopyTranslationLogCommand = noOpCommand,
            CopySourceTextCommand = noOpCommand,
            CopyTranslatedTextCommand = noOpCommand,
            TranslationLogs = translationLogs,
            ActivityLogs =
            [
                isEnglish ? "Recorder initialized." : "レコーダーを初期化しました。",
                isEnglish ? "Saved audio file and exported transcript." : "音声ファイルを保存して文字起こしを出力しました。"
            ]
        });
    }

    private static ObservableCollection<PreviewTranslationLog> BuildTranslationLogs(bool isEnglish)
    {
        var logs = new ObservableCollection<PreviewTranslationLog>();
        for (var i = 1; i <= 500; i++)
        {
            logs.Add(new PreviewTranslationLog
            {
                DisplaySourceText = isEnglish ? $"Source sample line {i} for layout validation." : $"レイアウト検証用の原文サンプル {i} 行目。",
                TranslatedText = isEnglish ? $"Translated sample line {i} for layout validation." : $"レイアウト検証用の翻訳サンプル {i} 行目。",
                AutomationText = $"log-{i}"
            });
        }

        return logs;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T matchedChild)
            {
                return matchedChild;
            }

            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}

sealed record PreviewScenario(string Kind, string Locale, int Width, int Height, int TranslationLogCount, PreviewMainWindowViewModel ViewModel);

sealed class PreviewMainWindowViewModel
{
    public string MainWindowTitle { get; init; } = string.Empty;
    public string HeroSubtitle { get; init; } = string.Empty;
    public string SettingsButtonText { get; init; } = string.Empty;
    public string RecognitionModeLabel { get; init; } = string.Empty;
    public string SourceLanguageLabel { get; init; } = string.Empty;
    public string TargetLanguageLabel { get; init; } = string.Empty;
    public string AudioInputLabel { get; init; } = string.Empty;
    public string SaveRecordingLabel { get; init; } = string.Empty;
    public string RecordingFileNameLabel { get; init; } = string.Empty;
    public string RecordingFileName { get; init; } = string.Empty;
    public string RecordingFileNamePreview { get; init; } = string.Empty;
    public string StartButtonText { get; init; } = string.Empty;
    public string StopButtonText { get; init; } = string.Empty;
    public string ClearLogsButtonText { get; init; } = string.Empty;
    public string OpenLatestRecordingFolderButtonText { get; init; } = string.Empty;
    public string ShowRecentTranslationsButtonText { get; init; } = string.Empty;
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusBadgeText { get; init; } = string.Empty;
    public string StatusDetailMessage { get; init; } = string.Empty;
    public Visibility StatusDetailVisibility { get; init; }
    public string UpdateStatusMessage { get; init; } = string.Empty;
    public Visibility UpdateBannerVisibility { get; init; }
    public string CheckForUpdatesButtonText { get; init; } = string.Empty;
    public string UpdateActionButtonText { get; init; } = string.Empty;
    public string TranslationLogHeader { get; init; } = string.Empty;
    public string StatusLogHeader { get; init; } = string.Empty;
    public string CopyAllLogsButtonText { get; init; } = string.Empty;
    public string CopyAllSourceLogsButtonText { get; init; } = string.Empty;
    public string CopyAllTranslatedLogsButtonText { get; init; } = string.Empty;
    public string CopyBlockButtonText { get; init; } = string.Empty;
    public string CopySourceButtonText { get; init; } = string.Empty;
    public string CopyTranslationButtonText { get; init; } = string.Empty;
    public string SourceTextHeader { get; init; } = string.Empty;
    public string TranslatedTextHeader { get; init; } = string.Empty;
    public IReadOnlyList<string> AvailableRecognitionModes { get; init; } = [];
    public IReadOnlyList<string> AvailableLanguages { get; init; } = [];
    public IReadOnlyList<string> AvailableAudioInputSources { get; init; } = [];
    public string SelectedRecognitionMode { get; init; } = string.Empty;
    public string SelectedSourceLanguage { get; init; } = string.Empty;
    public string SelectedTargetLanguage { get; init; } = string.Empty;
    public string SelectedAudioInputSource { get; init; } = string.Empty;
    public Visibility TranslationColumnVisibility { get; init; }
    public Visibility OpenLatestRecordingFolderButtonVisibility { get; init; }
    public bool IsTranslationMode { get; init; }
    public bool IsRecordingSaveEnabled { get; init; }
    public bool IsTranslationLogExpanded { get; init; }
    public bool IsStatusLogExpanded { get; init; }
    public GridLength TranslationLogRowHeight { get; init; }
    public ICommand StartCommand { get; init; } = new NoOpCommand();
    public ICommand StopCommand { get; init; } = new NoOpCommand();
    public ICommand ClearLogsCommand { get; init; } = new NoOpCommand();
    public ICommand OpenLatestRecordingFolderCommand { get; init; } = new NoOpCommand();
    public ICommand CheckForUpdatesCommand { get; init; } = new NoOpCommand();
    public ICommand ApplyUpdateCommand { get; init; } = new NoOpCommand();
    public ICommand CopyAllLogsCommand { get; init; } = new NoOpCommand();
    public ICommand CopyAllSourceLogsCommand { get; init; } = new NoOpCommand();
    public ICommand CopyAllTranslatedLogsCommand { get; init; } = new NoOpCommand();
    public ICommand CopySelectedTranslationLogCommand { get; init; } = new NoOpCommand();
    public ICommand CopySelectedSourceTextCommand { get; init; } = new NoOpCommand();
    public ICommand CopySelectedTranslatedTextCommand { get; init; } = new NoOpCommand();
    public ICommand CopyTranslationLogCommand { get; init; } = new NoOpCommand();
    public ICommand CopySourceTextCommand { get; init; } = new NoOpCommand();
    public ICommand CopyTranslatedTextCommand { get; init; } = new NoOpCommand();
    public ObservableCollection<PreviewTranslationLog> TranslationLogs { get; init; } = [];
    public PreviewTranslationLog? SelectedTranslationLog { get; init; }
    public ObservableCollection<string> ActivityLogs { get; init; } = [];
}

sealed class PreviewTranslationLog
{
    public string DisplaySourceText { get; init; } = string.Empty;
    public string TranslatedText { get; init; } = string.Empty;
    public string AutomationText { get; init; } = string.Empty;
}

sealed class NoOpCommand : ICommand
{
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter)
    {
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
}
