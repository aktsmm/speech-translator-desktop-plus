using SpeechTranslatorDesktop.Services;
using SpeechTranslatorDesktop.ViewModels;

namespace SpeechTranslatorDesktop;

public partial class MainWindow : Window
{
    private RecentTranslationsWindow? _recentTranslationsWindow;

    public MainWindow()
    {
        InitializeComponent();
        var recordingFileService = new RecordingFileService(AppContext.BaseDirectory);
        var settingsDatabasePath = SpeechSettingsPathProvider.GetDatabasePath();
        var viewModel = new MainViewModel(
            new WpfUiDispatcher(),
            new EnvironmentSpeechCredentialsProvider(),
            new SqliteAzureAiServiceSettingsStore(
                settingsDatabasePath,
                new DpapiSecretProtector()),
            recordingFileService,
            new WpfRecordingFolderPicker(),
            new SqliteRecordingFolderSettingsStore(settingsDatabasePath),
            new DesktopTranslationController(),
            new DesktopTranslationWorkerFactory(recordingFileService));
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }

    private void ShowRecentTranslationsWindow(object sender, RoutedEventArgs e)
    {
        if (_recentTranslationsWindow is null)
        {
            _recentTranslationsWindow = new RecentTranslationsWindow
            {
                Owner = this,
                DataContext = DataContext
            };
            _recentTranslationsWindow.Closed += (_, _) => _recentTranslationsWindow = null;
        }

        if (!_recentTranslationsWindow.IsVisible)
        {
            _recentTranslationsWindow.Show();
        }

        _recentTranslationsWindow.Activate();
    }
}
