using System.Globalization;
using SpeechTranslatorDesktop.Commands;
using SpeechTranslatorDesktop.Models;
using SpeechTranslatorDesktop.Services;

namespace SpeechTranslatorDesktop.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private const string DefaultRecordingFileNamePrefix = "session";
    private readonly IUiDispatcher _dispatcher;
    private readonly ISpeechCredentialsProvider _credentialsProvider;
    private readonly IAzureAiServiceSettingsStore _settingsStore;
    private readonly IRecordingFileService _recordingFileService;
    private readonly IRecordingFolderPicker _recordingFolderPicker;
    private readonly IRecordingFolderSettingsStore _recordingFolderSettingsStore;
    private readonly IAppPreferencesStore _appPreferencesStore;
    private readonly IClipboardService _clipboardService;
    private readonly ITranslationController _translationController;
    private readonly IDesktopTranslationWorkerFactory _workerFactory;
    private IDesktopTranslationWorker? _currentWorker;
    private AudioInputSourceOption? _selectedAudioInputSource;
    private RecognitionModeOption? _selectedRecognitionMode;
    private LanguageOption? _selectedSourceLanguage;
    private LanguageOption? _selectedTargetLanguage;
    private TranslationLogItem? _selectedTranslationLog;
    private UiLanguageOption? _selectedUiLanguage;
    private string _azureApiKey = string.Empty;
    private string _azureRegion = string.Empty;
    private string _recordingFileNamePrefix = string.Empty;
    private string _recordingsFolderPath = string.Empty;
    private string _settingsStatusMessage = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isRecordingSaveEnabled = true;
    private bool _isRunning;

    public MainViewModel(
        IUiDispatcher dispatcher,
        ISpeechCredentialsProvider credentialsProvider,
        IAzureAiServiceSettingsStore settingsStore,
        IRecordingFileService recordingFileService,
        IRecordingFolderPicker recordingFolderPicker,
        IRecordingFolderSettingsStore recordingFolderSettingsStore,
        IAppPreferencesStore appPreferencesStore,
        IClipboardService clipboardService,
        ITranslationController translationController,
        IDesktopTranslationWorkerFactory workerFactory)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _credentialsProvider = credentialsProvider ?? throw new ArgumentNullException(nameof(credentialsProvider));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _recordingFileService = recordingFileService ?? throw new ArgumentNullException(nameof(recordingFileService));
        _recordingFolderPicker = recordingFolderPicker ?? throw new ArgumentNullException(nameof(recordingFolderPicker));
        _recordingFolderSettingsStore = recordingFolderSettingsStore ?? throw new ArgumentNullException(nameof(recordingFolderSettingsStore));
        _appPreferencesStore = appPreferencesStore ?? throw new ArgumentNullException(nameof(appPreferencesStore));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _translationController = translationController ?? throw new ArgumentNullException(nameof(translationController));
        _workerFactory = workerFactory ?? throw new ArgumentNullException(nameof(workerFactory));

        AvailableLanguages =
        [
            new LanguageOption("en-US", "English (en-US)"),
            new LanguageOption("ja-JP", "Japanese (ja-JP)"),
            new LanguageOption("zh-CN", "Chinese Simplified (zh-CN)"),
            new LanguageOption("zh-TW", "Chinese Traditional (zh-TW)"),
            new LanguageOption("ko-KR", "Korean (ko-KR)"),
            new LanguageOption("fr-FR", "French (fr-FR)"),
            new LanguageOption("de-DE", "German (de-DE)"),
            new LanguageOption("es-ES", "Spanish (es-ES)"),
            new LanguageOption("it-IT", "Italian (it-IT)"),
            new LanguageOption("pt-BR", "Portuguese Brazil (pt-BR)"),
            new LanguageOption("id-ID", "Indonesian (id-ID)"),
            new LanguageOption("th-TH", "Thai (th-TH)"),
            new LanguageOption("vi-VN", "Vietnamese (vi-VN)")
        ];

        AvailableUiLanguages =
        [
            new UiLanguageOption(UiLanguage.Japanese, "日本語"),
            new UiLanguageOption(UiLanguage.English, "English")
        ];

        var defaultUiLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase)
            ? UiLanguage.Japanese
            : UiLanguage.English;

        AvailableAudioInputSources =
            CreateAudioInputSourceOptions(defaultUiLanguage);
        AvailableRecognitionModes =
            CreateRecognitionModeOptions(defaultUiLanguage);

        _selectedUiLanguage = AvailableUiLanguages.First(option => option.Language == defaultUiLanguage);
        _selectedAudioInputSource = AvailableAudioInputSources.First(option => option.Source == AudioInputSource.MicrophoneAndSystemAudio);
        _selectedRecognitionMode = AvailableRecognitionModes.First(option => option.Mode == RecognitionMode.Translation);
        _selectedSourceLanguage = AvailableLanguages[0];
        _selectedTargetLanguage = AvailableLanguages[1];
        _statusMessage = Text("停止", "Stopped");
        _recordingsFolderPath = _recordingFileService.RecordingsDirectory;

        StartCommand = new AsyncRelayCommand(StartAsync, () => !IsRunning, _dispatcher, HandleCommandException);
        StopCommand = new AsyncRelayCommand(StopAsync, () => IsRunning, _dispatcher, HandleCommandException);
        ChooseRecordingsFolderCommand = new AsyncRelayCommand(ChooseRecordingsFolderAsync, dispatcher: _dispatcher, onException: HandleCommandException);
        ClearLogsCommand = new AsyncRelayCommand(ClearLogsAsync, dispatcher: _dispatcher, onException: HandleCommandException);
        CopyAllLogsCommand = new AsyncRelayCommand(CopyAllLogsAsync, dispatcher: _dispatcher, onException: HandleCommandException);
        CopyAllSourceLogsCommand = new AsyncRelayCommand(CopyAllSourceLogsAsync, dispatcher: _dispatcher, onException: HandleCommandException);
        CopyAllTranslatedLogsCommand = new AsyncRelayCommand(CopyAllTranslatedLogsAsync, dispatcher: _dispatcher, onException: HandleCommandException);
        CopyTranslationLogCommand = new ParameterizedAsyncRelayCommand(CopyTranslationLogAsync, dispatcher: _dispatcher, onException: HandleCommandException);
        CopySourceTextCommand = new ParameterizedAsyncRelayCommand(CopySourceTextAsync, dispatcher: _dispatcher, onException: HandleCommandException);
        CopyTranslatedTextCommand = new ParameterizedAsyncRelayCommand(CopyTranslatedTextAsync, dispatcher: _dispatcher, onException: HandleCommandException);
        CopySelectedTranslationLogCommand = new AsyncRelayCommand(() => CopyTranslationLogAsync(SelectedTranslationLog), dispatcher: _dispatcher, onException: HandleCommandException);
        CopySelectedSourceTextCommand = new AsyncRelayCommand(() => CopySourceTextAsync(SelectedTranslationLog), dispatcher: _dispatcher, onException: HandleCommandException);
        CopySelectedTranslatedTextCommand = new AsyncRelayCommand(() => CopyTranslatedTextAsync(SelectedTranslationLog), dispatcher: _dispatcher, onException: HandleCommandException);
        OpenRecordingsFolderCommand = new AsyncRelayCommand(OpenRecordingsFolderAsync, dispatcher: _dispatcher, onException: HandleCommandException);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, dispatcher: _dispatcher, onException: HandleSettingsCommandException);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> ActivityLogs { get; } = [];

    public ObservableCollection<AudioInputSourceOption> AvailableAudioInputSources { get; }

    public ObservableCollection<LanguageOption> AvailableLanguages { get; }

    public ObservableCollection<RecognitionModeOption> AvailableRecognitionModes { get; }

    public ObservableCollection<UiLanguageOption> AvailableUiLanguages { get; }

    public string AzureApiKey
    {
        get => _azureApiKey;
        set
        {
            if (_azureApiKey == value)
            {
                return;
            }

            _azureApiKey = value;
            OnPropertyChanged();
        }
    }

    public string AzureRegion
    {
        get => _azureRegion;
        set
        {
            if (_azureRegion == value)
            {
                return;
            }

            _azureRegion = value;
            OnPropertyChanged();
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value)
            {
                return;
            }

            _isRunning = value;
            OnPropertyChanged();
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsRecordingSaveEnabled
    {
        get => _isRecordingSaveEnabled;
        set
        {
            if (_isRecordingSaveEnabled == value)
            {
                return;
            }

            _isRecordingSaveEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RecordingFileNamePreview));
        }
    }

    public string RecordingFileName
    {
        get => _recordingFileNamePrefix;
        set
        {
            if (_recordingFileNamePrefix == value)
            {
                return;
            }

            _recordingFileNamePrefix = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RecordingFileNamePreview));
        }
    }

    public string RecordingsFolderPath
    {
        get => _recordingsFolderPath;
        private set
        {
            if (_recordingsFolderPath == value)
            {
                return;
            }

            _recordingsFolderPath = value;
            OnPropertyChanged();
        }
    }

    public AudioInputSourceOption? SelectedAudioInputSource
    {
        get => _selectedAudioInputSource;
        set
        {
            if (_selectedAudioInputSource == value)
            {
                return;
            }

            _selectedAudioInputSource = value;
            OnPropertyChanged();
        }
    }

    public RecognitionModeOption? SelectedRecognitionMode
    {
        get => _selectedRecognitionMode;
        set
        {
            if (_selectedRecognitionMode == value)
            {
                return;
            }

            _selectedRecognitionMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTranslationMode));
            OnPropertyChanged(nameof(TranslationColumnVisibility));
            RaiseUiTextChanged();
        }
    }

    public UiLanguageOption? SelectedUiLanguage
    {
        get => _selectedUiLanguage;
        set
        {
            if (_selectedUiLanguage == value)
            {
                return;
            }

            _selectedUiLanguage = value;
            OnPropertyChanged();
            UpdateAudioInputSourceLabels();
            UpdateRecognitionModeLabels();
            UpdateLocalizedStatusMessage();
            RaiseUiTextChanged();
        }
    }

    public LanguageOption? SelectedSourceLanguage
    {
        get => _selectedSourceLanguage;
        set
        {
            if (_selectedSourceLanguage == value)
            {
                return;
            }

            _selectedSourceLanguage = value;
            OnPropertyChanged();
        }
    }

    public LanguageOption? SelectedTargetLanguage
    {
        get => _selectedTargetLanguage;
        set
        {
            if (_selectedTargetLanguage == value)
            {
                return;
            }

            _selectedTargetLanguage = value;
            OnPropertyChanged();
        }
    }

    public TranslationLogItem? SelectedTranslationLog
    {
        get => _selectedTranslationLog;
        set
        {
            if (_selectedTranslationLog == value)
            {
                return;
            }

            _selectedTranslationLog = value;
            OnPropertyChanged();
        }
    }

    public AsyncRelayCommand StartCommand { get; }

    public AsyncRelayCommand ChooseRecordingsFolderCommand { get; }

    public AsyncRelayCommand ClearLogsCommand { get; }

    public AsyncRelayCommand CopyAllLogsCommand { get; }

    public AsyncRelayCommand CopyAllSourceLogsCommand { get; }

    public AsyncRelayCommand CopyAllTranslatedLogsCommand { get; }

    public ParameterizedAsyncRelayCommand CopyTranslationLogCommand { get; }

    public ParameterizedAsyncRelayCommand CopySourceTextCommand { get; }

    public ParameterizedAsyncRelayCommand CopyTranslatedTextCommand { get; }

    public AsyncRelayCommand CopySelectedTranslationLogCommand { get; }

    public AsyncRelayCommand CopySelectedSourceTextCommand { get; }

    public AsyncRelayCommand CopySelectedTranslatedTextCommand { get; }

    public AsyncRelayCommand OpenRecordingsFolderCommand { get; }

    public AsyncRelayCommand SaveSettingsCommand { get; }

    public string SettingsStatusMessage
    {
        get => _settingsStatusMessage;
        private set
        {
            if (_settingsStatusMessage == value)
            {
                return;
            }

            _settingsStatusMessage = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public AsyncRelayCommand StopCommand { get; }

    public ObservableCollection<TranslationLogItem> TranslationLogs { get; } = [];

    public ObservableCollection<TranslationLogItem> RecentTranslationLogs { get; } = [];

    public string ApiKeyLabel => Text("API Key", "API Key");

    public string AudioInputLabel => Text("音声入力", "Audio input");

    public string AzureSettingsHeader => Text("保存先 / Azure AI Service 設定", "Recordings / Azure AI Service settings");

    public string ChooseButtonText => Text("選択", "Choose");

    public string ClearLogsButtonText => Text("ログクリア", "Clear logs");

    public string CopyAllLogsButtonText => Text("すべてコピー", "Copy all");

    public string CopyAllSourceLogsButtonText => Text("原文をすべてコピー", "Copy all source");

    public string CopyAllTranslatedLogsButtonText => Text("訳文をすべてコピー", "Copy all translations");

    public string CopyBlockButtonText => Text("コピー", "Copy");

    public string CopySourceButtonText => Text("原文コピー", "Copy source");

    public string CopyTranslationButtonText => Text("訳文コピー", "Copy translation");

    public string OpenButtonText => Text("開く", "Open");

    public string RecognitionModeLabel => Text("利用モード", "Mode");

    public string RecordingFileNameLabel => Text("ファイル名 prefix（任意）", "File name prefix (optional)");

    public string RecordingFileNamePreview
    {
        get
        {
            if (!IsRecordingSaveEnabled)
            {
                return Text("保存OFF: ファイルには保存しません。", "Save off: no recording file will be written.");
            }

            return Text(
                $"保存例: {GetEffectiveRecordingFileNamePrefix()}_yyyyMMdd_HHmmss.txt",
                $"Example: {GetEffectiveRecordingFileNamePrefix()}_yyyyMMdd_HHmmss.txt");
        }
    }

    public string RecordingsFolderLabel => Text("保存先", "Recordings folder");

    public string SaveButtonText => Text("保存", "Save");

    public string SaveRecordingLabel => Text("記録を保存する", "Save recording");

    public string SettingsButtonText => Text("設定", "Settings");

    public string SettingsWindowTitle => Text("設定", "Settings");

    public string ShowRecentTranslationsButtonText => Text("別ウィンドウでライブノートを開く", "Open live notes window");

    public string SourceLanguageLabel => Text("話者言語", "Speaker language");

    public string SourceTextHeader => IsTranslationMode ? Text("原文", "Source text") : Text("書き起こし", "Transcript");

    public string StartButtonText => Text("開始", "Start");

    public string StatusLabel => Text("状態", "Status");

    public string StatusLogHeader => Text("状態ログ", "Status log");

    public string StopButtonText => Text("停止", "Stop");

    public string TargetLanguageLabel => Text("翻訳先言語", "Target language");

    public string TranslationLogHeader => IsTranslationMode ? Text("翻訳ログ", "Translation log") : Text("書き起こしログ", "Transcript log");

    public string TranslationsWindowTitle => Text("ライブノート（最新3件）", "Live notes (latest 3)");

    public string TranslatedTextHeader => Text("翻訳文", "Translation");

    public string UiLanguageLabel => Text("UI言語", "UI language");

    public bool IsTranslationMode => SelectedRecognitionMode?.Mode != RecognitionMode.TranscriptionOnly;

    public Visibility TranslationColumnVisibility => IsTranslationMode ? Visibility.Visible : Visibility.Collapsed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await InitializeRecordingFolderAsync(cancellationToken);
        await InitializeAppPreferencesAsync(cancellationToken);

        try
        {
            var savedSettings = await _settingsStore.LoadAsync(cancellationToken);
            if (savedSettings is null)
            {
                SettingsStatusMessage = Text(
                    "Azure AI Service のリージョンと API キーを入力して保存してください。未保存の場合は SPEECH_REGION / SPEECH_KEY をフォールバックとして使用します。",
                    "Enter and save the Azure AI Service region and API key. If not saved, SPEECH_REGION / SPEECH_KEY environment variables are used as fallback.");
                return;
            }

            AzureRegion = savedSettings.Region;
            AzureApiKey = savedSettings.ApiKey;
            SettingsStatusMessage = Text("保存済みの Azure AI Service 設定を読み込みました。", "Loaded saved Azure AI Service settings.");
        }
        catch (Exception ex)
        {
            SettingsStatusMessage = Text($"設定の読み込みに失敗しました: {ex.Message}", $"Failed to load settings: {ex.Message}");
            AddActivityLog(SettingsStatusMessage);
        }
    }

    private async Task InitializeRecordingFolderAsync(CancellationToken cancellationToken)
    {
        try
        {
            var savedRecordingsFolder = await _recordingFolderSettingsStore.LoadAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(savedRecordingsFolder))
            {
                _recordingFileService.SetRecordingsDirectory(savedRecordingsFolder);
            }

            RecordingsFolderPath = _recordingFileService.RecordingsDirectory;
        }
        catch (Exception ex)
        {
            AddActivityLog(Text($"保存先設定の読み込みに失敗しました: {ex.Message}", $"Failed to load recordings folder settings: {ex.Message}"));
        }
    }

    private async Task InitializeAppPreferencesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var preferences = await _appPreferencesStore.LoadAsync(cancellationToken);
            if (preferences is null)
            {
                return;
            }

            if (Enum.TryParse<UiLanguage>(preferences.UiLanguage, out var uiLanguage))
            {
                SelectedUiLanguage = AvailableUiLanguages.FirstOrDefault(option => option.Language == uiLanguage) ?? SelectedUiLanguage;
            }

            SelectedSourceLanguage = AvailableLanguages.FirstOrDefault(option => option.Code == preferences.SourceLanguage) ?? SelectedSourceLanguage;
            SelectedTargetLanguage = AvailableLanguages.FirstOrDefault(option => option.Code == preferences.TargetLanguage) ?? SelectedTargetLanguage;

            if (Enum.TryParse<AudioInputSource>(preferences.AudioInputSource, out var audioInputSource))
            {
                SelectedAudioInputSource = AvailableAudioInputSources.FirstOrDefault(option => option.Source == audioInputSource) ?? SelectedAudioInputSource;
            }

            if (Enum.TryParse<RecognitionMode>(preferences.RecognitionMode, out var recognitionMode))
            {
                SelectedRecognitionMode = AvailableRecognitionModes.FirstOrDefault(option => option.Mode == recognitionMode) ?? SelectedRecognitionMode;
            }

            IsRecordingSaveEnabled = preferences.IsRecordingSaveEnabled ?? true;
            RecordingFileName = preferences.RecordingFileNamePrefix ?? string.Empty;
        }
        catch (Exception ex)
        {
            AddActivityLog(Text($"前回の選択設定の読み込みに失敗しました: {ex.Message}", $"Failed to load previous selections: {ex.Message}"));
        }
    }

    private async Task StartAsync()
    {
        if (SelectedSourceLanguage is null || SelectedAudioInputSource is null || SelectedRecognitionMode is null)
        {
            StatusMessage = Text("言語、音声入力、利用モードを選択してください。", "Select language, audio input, and mode.");
            return;
        }

        if (IsTranslationMode && SelectedTargetLanguage is null)
        {
            StatusMessage = Text("翻訳先言語を選択してください。", "Select a target language.");
            return;
        }

        string? recordingFileName;
        try
        {
            recordingFileName = CreateRecordingFileNameForStart();
        }
        catch (ArgumentException ex)
        {
            StatusMessage = Text($"ファイル名 prefix が不正です: {ex.Message}", $"Invalid file name prefix: {ex.Message}");
            AddActivityLog(StatusMessage);
            return;
        }

        var credentialsResult = _credentialsProvider.GetCredentials(AzureRegion, AzureApiKey);
        if (!credentialsResult.IsValid || credentialsResult.Credentials is null)
        {
            StatusMessage = Text(
                $"Azure AI Service の認証情報が未設定です。設定画面で保存するか、環境変数を設定してください: SPEECH_REGION, SPEECH_KEY",
                "Azure AI Service credentials are not configured. Save them in the settings area or set environment variables: SPEECH_REGION, SPEECH_KEY");
            AddActivityLog(credentialsResult.ErrorMessage);
            return;
        }

        var targetLanguage = SelectedTargetLanguage?.Code ?? SelectedSourceLanguage.Code;
        var recognitionMode = SelectedRecognitionMode.Mode;
        var worker = _workerFactory.Create(targetLanguage, recordingFileName, SelectedUiLanguage?.Language ?? UiLanguage.Japanese, recognitionMode);
        SubscribeWorker(worker);

        try
        {
            await SaveAppPreferencesAsync();
            await _translationController.StartAsync(
                credentialsResult.Credentials,
                SelectedSourceLanguage.Code,
                targetLanguage,
                SelectedAudioInputSource.Source,
                recognitionMode,
                worker);

            _currentWorker = worker;
            IsRunning = true;
            StatusMessage = Text("開始", "Started");
            AddActivityLog(IsTranslationMode ? Text("翻訳を開始しました。", "Translation started.") : Text("書き起こしを開始しました。", "Transcription started."));
            if (recordingFileName is not null)
            {
                AddActivityLog(Text($"記録ファイル: {recordingFileName}.txt", $"Recording file: {recordingFileName}.txt"));
            }
        }
        catch (Exception ex)
        {
            UnsubscribeWorker(worker);
            StatusMessage = ex.Message;
            AddActivityLog(ex.Message);
        }
    }

    private async Task SaveSettingsAsync()
    {
        var normalizedRegion = AzureRegion.Trim();
        var normalizedApiKey = AzureApiKey.Trim();

        if (string.IsNullOrWhiteSpace(normalizedRegion) || string.IsNullOrWhiteSpace(normalizedApiKey))
        {
            SettingsStatusMessage = Text("Azure AI Service のリージョンと API キーを入力してから保存してください。", "Enter the Azure AI Service region and API key before saving.");
            return;
        }

        await _settingsStore.SaveAsync(new AzureAiServiceSettings(normalizedRegion, normalizedApiKey));

        AzureRegion = normalizedRegion;
        AzureApiKey = normalizedApiKey;
        SettingsStatusMessage = Text("Azure AI Service 設定を保存しました。", "Saved Azure AI Service settings.");
        AddActivityLog(SettingsStatusMessage);
    }

    private Task OpenRecordingsFolderAsync()
    {
        try
        {
            var recordingsDirectory = _recordingFileService.OpenRecordingsFolder();
            RecordingsFolderPath = recordingsDirectory;
            StatusMessage = Text("保存先を開きました。", "Opened recordings folder.");
            AddActivityLog(Text($"保存先を開きました: {recordingsDirectory}", $"Opened recordings folder: {recordingsDirectory}"));
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            AddActivityLog(ex.Message);
        }

        return Task.CompletedTask;
    }

    private Task ClearLogsAsync()
    {
        TranslationLogs.Clear();
        RecentTranslationLogs.Clear();
        ActivityLogs.Clear();
        StatusMessage = Text("ログをクリアしました。", "Logs cleared.");
        AddActivityLog(Text("ログをクリアしました。記録ファイルは開始ごとに新規作成されます。", "Logs cleared. Recording files are created fresh on each start."));
        return Task.CompletedTask;
    }

    private async Task CopyAllLogsAsync()
    {
        if (!await CopyManyLogsAsync(TranslationLogs, FormatLogItem, Text("コピーするログがありません。", "No logs to copy.")))
        {
            return;
        }

        StatusMessage = Text("コピーしました。", "Copied.");
        AddActivityLog(Text("すべてのログをコピーしました。", "Copied all logs."));
    }

    private async Task CopyAllSourceLogsAsync()
    {
        if (!await CopyManyLogsAsync(TranslationLogs, item => item.SourceText, Text("コピーする原文がありません。", "No source text to copy.")))
        {
            return;
        }

        StatusMessage = Text("コピーしました。", "Copied.");
        AddActivityLog(Text("すべての原文をコピーしました。", "Copied all source text."));
    }

    private async Task CopyAllTranslatedLogsAsync()
    {
        if (!await CopyManyLogsAsync(
            TranslationLogs.Where(item => !string.IsNullOrWhiteSpace(item.TranslatedText)),
            item => item.TranslatedText,
            Text("コピーする訳文がありません。", "No translations to copy.")))
        {
            return;
        }

        StatusMessage = Text("コピーしました。", "Copied.");
        AddActivityLog(Text("すべての訳文をコピーしました。", "Copied all translations."));
    }

    private async Task CopyTranslationLogAsync(object? parameter)
    {
        if (parameter is not TranslationLogItem item)
        {
            StatusMessage = Text("コピーするログを選択してください。", "Select a log to copy.");
            AddActivityLog(StatusMessage);
            return;
        }

        if (!await TrySetClipboardTextAsync(FormatLogItem(item)))
        {
            return;
        }

        StatusMessage = Text("コピーしました。", "Copied.");
        AddActivityLog(Text("ログブロックをコピーしました。", "Copied log block."));
    }

    private async Task CopySourceTextAsync(object? parameter)
    {
        if (parameter is not TranslationLogItem item || string.IsNullOrWhiteSpace(item.SourceText))
        {
            StatusMessage = Text("コピーする原文を選択してください。", "Select source text to copy.");
            AddActivityLog(StatusMessage);
            return;
        }

        if (!await TrySetClipboardTextAsync(item.SourceText))
        {
            return;
        }

        StatusMessage = Text("コピーしました。", "Copied.");
        AddActivityLog(Text("原文をコピーしました。", "Copied source text."));
    }

    private async Task CopyTranslatedTextAsync(object? parameter)
    {
        if (parameter is not TranslationLogItem item || string.IsNullOrWhiteSpace(item.TranslatedText))
        {
            StatusMessage = Text("コピーする訳文を選択してください。", "Select translation text to copy.");
            AddActivityLog(StatusMessage);
            return;
        }

        if (!await TrySetClipboardTextAsync(item.TranslatedText))
        {
            return;
        }

        StatusMessage = Text("コピーしました。", "Copied.");
        AddActivityLog(Text("訳文をコピーしました。", "Copied translation text."));
    }

    private async Task<bool> CopyManyLogsAsync(IEnumerable<TranslationLogItem> items, Func<TranslationLogItem, string> selector, string emptyMessage)
    {
        var blocks = items
            .Select(selector)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
        if (blocks.Length == 0)
        {
            StatusMessage = emptyMessage;
            AddActivityLog(StatusMessage);
            return false;
        }

        var text = string.Join($"{Environment.NewLine}{Environment.NewLine}", blocks);
        return await TrySetClipboardTextAsync(text);
    }

    private async Task<bool> TrySetClipboardTextAsync(string text)
    {
        try
        {
            await _clipboardService.SetTextAsync(text);
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = Text($"コピーに失敗しました: {ex.Message}", $"Copy failed: {ex.Message}");
            AddActivityLog(StatusMessage);
            return false;
        }
    }

    private async Task ChooseRecordingsFolderAsync()
    {
        var selectedFolder = _recordingFolderPicker.PickFolder(RecordingsFolderPath);
        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            return;
        }

        _recordingFileService.SetRecordingsDirectory(selectedFolder);
        await _recordingFolderSettingsStore.SaveAsync(_recordingFileService.RecordingsDirectory);
        RecordingsFolderPath = _recordingFileService.RecordingsDirectory;
        StatusMessage = Text("保存先を変更しました。", "Changed recordings folder.");
        AddActivityLog(Text($"保存先を変更しました: {RecordingsFolderPath}", $"Changed recordings folder: {RecordingsFolderPath}"));
    }

    private async Task StopAsync()
    {
        try
        {
            await _translationController.StopAsync();
            StatusMessage = Text("停止", "Stopped");
            AddActivityLog(IsTranslationMode ? Text("翻訳を停止しました。", "Translation stopped.") : Text("書き起こしを停止しました。", "Transcription stopped."));
        }
        catch (Exception ex)
        {
            StatusMessage = Text($"停止に失敗しました: {ex.Message}", $"Failed to stop: {ex.Message}");
            AddActivityLog(StatusMessage);
        }
        finally
        {
            IsRunning = _translationController.IsRunning;

            if (!IsRunning)
            {
                DetachCurrentWorker();
            }
        }
    }

    private void SubscribeWorker(IDesktopTranslationWorker worker)
    {
        worker.StatusChanged += OnWorkerStatusChanged;
        worker.MessageLogged += OnWorkerMessageLogged;
        worker.TranslationLogged += OnWorkerTranslationLogged;
    }

    private void UnsubscribeWorker(IDesktopTranslationWorker worker)
    {
        worker.StatusChanged -= OnWorkerStatusChanged;
        worker.MessageLogged -= OnWorkerMessageLogged;
        worker.TranslationLogged -= OnWorkerTranslationLogged;
    }

    private void OnWorkerStatusChanged(object? sender, WorkerStatusChangedEventArgs e)
    {
        _dispatcher.Invoke(() =>
        {
            StatusMessage = e.Message;
            if (e.Status != DesktopTranslationStatus.Recognizing)
            {
                AddActivityLog(e.Message);
            }

            if (e.Status is DesktopTranslationStatus.Canceled or DesktopTranslationStatus.SessionStopped)
            {
                IsRunning = false;

                if (sender is IDesktopTranslationWorker worker)
                {
                    UnsubscribeWorker(worker);

                    if (ReferenceEquals(_currentWorker, worker))
                    {
                        _currentWorker = null;
                    }
                }
            }
        });
    }

    private void OnWorkerMessageLogged(object? sender, string e)
    {
        _dispatcher.Invoke(() => AddActivityLog(e));
    }

    private void OnWorkerTranslationLogged(object? sender, TranslationLogItem e)
    {
            _dispatcher.Invoke(() =>
            {
                TranslationLogs.Insert(0, e);
                RecentTranslationLogs.Insert(0, e);
                while (RecentTranslationLogs.Count > 3)
                {
                    RecentTranslationLogs.RemoveAt(RecentTranslationLogs.Count - 1);
            }
        });
    }

    private void AddActivityLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ActivityLogs.Insert(0, message);
    }

    private static ObservableCollection<AudioInputSourceOption> CreateAudioInputSourceOptions(UiLanguage language)
    {
        return
        [
            new AudioInputSourceOption(AudioInputSource.Microphone, language == UiLanguage.Japanese ? "マイク" : "Microphone"),
            new AudioInputSourceOption(AudioInputSource.SystemAudio, language == UiLanguage.Japanese ? "PC音声（既定の再生デバイス）" : "PC audio (default playback device)"),
            new AudioInputSourceOption(AudioInputSource.MicrophoneAndSystemAudio, language == UiLanguage.Japanese ? "マイク + PC音声" : "Microphone + PC audio")
        ];
    }

    private static ObservableCollection<RecognitionModeOption> CreateRecognitionModeOptions(UiLanguage language)
    {
        return
        [
            new RecognitionModeOption(RecognitionMode.Translation, language == UiLanguage.Japanese ? "翻訳 + 書き起こし" : "Translate + transcript"),
            new RecognitionModeOption(RecognitionMode.TranscriptionOnly, language == UiLanguage.Japanese ? "書き起こしのみ" : "Transcript only")
        ];
    }

    private string Text(string japanese, string english)
    {
        return SelectedUiLanguage?.Language == UiLanguage.English ? english : japanese;
    }

    private void UpdateAudioInputSourceLabels()
    {
        var selectedSource = SelectedAudioInputSource?.Source ?? AudioInputSource.Microphone;
        var options = CreateAudioInputSourceOptions(SelectedUiLanguage?.Language ?? UiLanguage.Japanese);

        AvailableAudioInputSources.Clear();
        foreach (var option in options)
        {
            AvailableAudioInputSources.Add(option);
        }

        SelectedAudioInputSource = AvailableAudioInputSources.FirstOrDefault(option => option.Source == selectedSource) ?? AvailableAudioInputSources[0];
    }

    private void UpdateRecognitionModeLabels()
    {
        var selectedMode = SelectedRecognitionMode?.Mode ?? RecognitionMode.Translation;
        var options = CreateRecognitionModeOptions(SelectedUiLanguage?.Language ?? UiLanguage.Japanese);

        AvailableRecognitionModes.Clear();
        foreach (var option in options)
        {
            AvailableRecognitionModes.Add(option);
        }

        SelectedRecognitionMode = AvailableRecognitionModes.FirstOrDefault(option => option.Mode == selectedMode) ?? AvailableRecognitionModes[0];
    }

    private Task SaveAppPreferencesAsync()
    {
        return _appPreferencesStore.SaveAsync(new AppPreferences(
            SelectedUiLanguage?.Language.ToString(),
            SelectedSourceLanguage?.Code,
            SelectedTargetLanguage?.Code,
            SelectedAudioInputSource?.Source.ToString(),
            SelectedRecognitionMode?.Mode.ToString(),
            IsRecordingSaveEnabled,
            RecordingFileName));
    }

    private string? CreateRecordingFileNameForStart()
    {
        if (!IsRecordingSaveEnabled)
        {
            return null;
        }

        var normalizedPrefix = _recordingFileService.NormalizeFileName(GetEffectiveRecordingFileNamePrefix());
        if (normalizedPrefix is null)
        {
            throw new ArgumentException(Text("ファイル名 prefix を指定してください。", "Specify a file name prefix."));
        }

        return CreateTimestampedRecordingFileName(normalizedPrefix);
    }

    private string GetEffectiveRecordingFileNamePrefix()
    {
        return string.IsNullOrWhiteSpace(RecordingFileName)
            ? DefaultRecordingFileNamePrefix
            : RecordingFileName.Trim();
    }

    private void UpdateLocalizedStatusMessage()
    {
        if (_statusMessage is "停止" or "Stopped")
        {
            StatusMessage = Text("停止", "Stopped");
            return;
        }

        if (_statusMessage is "開始" or "Started")
        {
            StatusMessage = Text("開始", "Started");
            return;
        }

        if (_statusMessage is "ログをクリアしました。" or "Logs cleared.")
        {
            StatusMessage = Text("ログをクリアしました。", "Logs cleared.");
            return;
        }
    }

    private void RaiseUiTextChanged()
    {
        OnPropertyChanged(nameof(ApiKeyLabel));
        OnPropertyChanged(nameof(AudioInputLabel));
        OnPropertyChanged(nameof(AzureSettingsHeader));
        OnPropertyChanged(nameof(ChooseButtonText));
        OnPropertyChanged(nameof(ClearLogsButtonText));
        OnPropertyChanged(nameof(CopyAllLogsButtonText));
        OnPropertyChanged(nameof(CopyAllSourceLogsButtonText));
        OnPropertyChanged(nameof(CopyAllTranslatedLogsButtonText));
        OnPropertyChanged(nameof(CopyBlockButtonText));
        OnPropertyChanged(nameof(CopySourceButtonText));
        OnPropertyChanged(nameof(CopyTranslationButtonText));
        OnPropertyChanged(nameof(OpenButtonText));
        OnPropertyChanged(nameof(RecognitionModeLabel));
        OnPropertyChanged(nameof(RecordingFileNameLabel));
        OnPropertyChanged(nameof(RecordingFileNamePreview));
        OnPropertyChanged(nameof(RecordingsFolderLabel));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(SaveRecordingLabel));
        OnPropertyChanged(nameof(SettingsButtonText));
        OnPropertyChanged(nameof(SettingsWindowTitle));
        OnPropertyChanged(nameof(ShowRecentTranslationsButtonText));
        OnPropertyChanged(nameof(SourceLanguageLabel));
        OnPropertyChanged(nameof(SourceTextHeader));
        OnPropertyChanged(nameof(StartButtonText));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(StatusLogHeader));
        OnPropertyChanged(nameof(StopButtonText));
        OnPropertyChanged(nameof(TargetLanguageLabel));
        OnPropertyChanged(nameof(TranslationLogHeader));
        OnPropertyChanged(nameof(TranslationsWindowTitle));
        OnPropertyChanged(nameof(TranslatedTextHeader));
        OnPropertyChanged(nameof(UiLanguageLabel));
        OnPropertyChanged(nameof(IsTranslationMode));
        OnPropertyChanged(nameof(TranslationColumnVisibility));
    }

    private void DetachCurrentWorker()
    {
        if (_currentWorker is null)
        {
            return;
        }

        UnsubscribeWorker(_currentWorker);
        _currentWorker = null;
    }

    private static string CreateTimestampedRecordingFileName(string baseFileName)
    {
        return $"{baseFileName}_{DateTime.Now:yyyyMMdd_HHmmss}";
    }

    private static string FormatLogItem(TranslationLogItem item)
    {
        return string.IsNullOrWhiteSpace(item.TranslatedText)
            ? item.SourceText
            : $"Source:{Environment.NewLine}{item.SourceText}{Environment.NewLine}{Environment.NewLine}Translation:{Environment.NewLine}{item.TranslatedText}";
    }

    private void HandleCommandException(Exception ex)
    {
        _dispatcher.Invoke(() =>
        {
            StatusMessage = ex.Message;
            AddActivityLog(ex.Message);
            IsRunning = _translationController.IsRunning;

            if (!IsRunning)
            {
                DetachCurrentWorker();
            }
        });
    }

    private void HandleSettingsCommandException(Exception ex)
    {
        _dispatcher.Invoke(() =>
        {
            SettingsStatusMessage = Text($"設定の保存に失敗しました: {ex.Message}", $"Failed to save settings: {ex.Message}");
            AddActivityLog(SettingsStatusMessage);
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
