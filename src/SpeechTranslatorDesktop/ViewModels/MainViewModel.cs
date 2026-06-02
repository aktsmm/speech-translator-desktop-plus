using System.Globalization;
using SpeechTranslatorDesktop.Commands;
using SpeechTranslatorDesktop.Models;
using SpeechTranslatorDesktop.Services;

namespace SpeechTranslatorDesktop.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IUiDispatcher _dispatcher;
    private readonly ISpeechCredentialsProvider _credentialsProvider;
    private readonly IAzureAiServiceSettingsStore _settingsStore;
    private readonly IRecordingFileService _recordingFileService;
    private readonly IRecordingFolderPicker _recordingFolderPicker;
    private readonly IRecordingFolderSettingsStore _recordingFolderSettingsStore;
    private readonly ITranslationController _translationController;
    private readonly IDesktopTranslationWorkerFactory _workerFactory;
    private IDesktopTranslationWorker? _currentWorker;
    private AudioInputSourceOption? _selectedAudioInputSource;
    private LanguageOption? _selectedSourceLanguage;
    private LanguageOption? _selectedTargetLanguage;
    private UiLanguageOption? _selectedUiLanguage;
    private string _azureApiKey = string.Empty;
    private string _azureRegion = string.Empty;
    private string _recordingFileName = string.Empty;
    private string _recordingsFolderPath = string.Empty;
    private string _settingsStatusMessage = string.Empty;
    private string _statusMessage = "停止";
    private bool _isRunning;
    private bool _startNewRecordingFileOnNextStart;

    public MainViewModel(
        IUiDispatcher dispatcher,
        ISpeechCredentialsProvider credentialsProvider,
        IAzureAiServiceSettingsStore settingsStore,
        IRecordingFileService recordingFileService,
        IRecordingFolderPicker recordingFolderPicker,
        IRecordingFolderSettingsStore recordingFolderSettingsStore,
        ITranslationController translationController,
        IDesktopTranslationWorkerFactory workerFactory)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _credentialsProvider = credentialsProvider ?? throw new ArgumentNullException(nameof(credentialsProvider));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _recordingFileService = recordingFileService ?? throw new ArgumentNullException(nameof(recordingFileService));
        _recordingFolderPicker = recordingFolderPicker ?? throw new ArgumentNullException(nameof(recordingFolderPicker));
        _recordingFolderSettingsStore = recordingFolderSettingsStore ?? throw new ArgumentNullException(nameof(recordingFolderSettingsStore));
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

        _selectedUiLanguage = AvailableUiLanguages.First(option => option.Language == defaultUiLanguage);
        _selectedAudioInputSource = AvailableAudioInputSources.First(option => option.Source == AudioInputSource.MicrophoneAndSystemAudio);
        _selectedSourceLanguage = AvailableLanguages[0];
        _selectedTargetLanguage = AvailableLanguages[1];
        _recordingsFolderPath = _recordingFileService.RecordingsDirectory;

        StartCommand = new AsyncRelayCommand(StartAsync, () => !IsRunning, _dispatcher, HandleCommandException);
        StopCommand = new AsyncRelayCommand(StopAsync, () => IsRunning, _dispatcher, HandleCommandException);
        ChooseRecordingsFolderCommand = new AsyncRelayCommand(ChooseRecordingsFolderAsync, dispatcher: _dispatcher, onException: HandleCommandException);
        ClearLogsCommand = new AsyncRelayCommand(ClearLogsAsync, dispatcher: _dispatcher, onException: HandleCommandException);
        OpenRecordingsFolderCommand = new AsyncRelayCommand(OpenRecordingsFolderAsync, dispatcher: _dispatcher, onException: HandleCommandException);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, dispatcher: _dispatcher, onException: HandleSettingsCommandException);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> ActivityLogs { get; } = [];

    public ObservableCollection<AudioInputSourceOption> AvailableAudioInputSources { get; }

    public ObservableCollection<LanguageOption> AvailableLanguages { get; }

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

    public string RecordingFileName
    {
        get => _recordingFileName;
        set
        {
            if (_recordingFileName == value)
            {
                return;
            }

            _recordingFileName = value;
            OnPropertyChanged();
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

    public AsyncRelayCommand StartCommand { get; }

    public AsyncRelayCommand ChooseRecordingsFolderCommand { get; }

    public AsyncRelayCommand ClearLogsCommand { get; }

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

    public string OpenButtonText => Text("開く", "Open");

    public string RecordingFileNameLabel => Text("記録ファイル名（任意）", "Recording file name (optional)");

    public string RecordingsFolderLabel => Text("保存先", "Recordings folder");

    public string SaveButtonText => Text("保存", "Save");

    public string ShowRecentTranslationsButtonText => Text("直近3件", "Latest 3");

    public string SourceLanguageLabel => Text("話者言語", "Speaker language");

    public string SourceTextHeader => Text("原文", "Source text");

    public string StartButtonText => Text("開始", "Start");

    public string StatusLogHeader => Text("状態ログ", "Status log");

    public string StopButtonText => Text("停止", "Stop");

    public string TargetLanguageLabel => Text("翻訳先言語", "Target language");

    public string TranslationLogHeader => Text("翻訳ログ", "Translation log");

    public string TranslatedTextHeader => Text("翻訳文", "Translation");

    public string UiLanguageLabel => Text("UI言語", "UI language");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await InitializeRecordingFolderAsync(cancellationToken);

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

    private async Task StartAsync()
    {
        if (SelectedSourceLanguage is null || SelectedTargetLanguage is null || SelectedAudioInputSource is null)
        {
            StatusMessage = Text("言語と音声入力を選択してください。", "Select languages and audio input.");
            return;
        }

        string? recordingFileName;
        try
        {
            recordingFileName = _recordingFileService.NormalizeFileName(RecordingFileName);
            RecordingFileName = recordingFileName ?? string.Empty;
            if (_startNewRecordingFileOnNextStart && recordingFileName is not null)
            {
                recordingFileName = CreateNewRecordingFileName(recordingFileName);
            }
        }
        catch (ArgumentException ex)
        {
            StatusMessage = Text($"記録ファイル名が不正です: {ex.Message}", $"Invalid recording file name: {ex.Message}");
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

        var worker = _workerFactory.Create(SelectedTargetLanguage.Code, recordingFileName, SelectedUiLanguage?.Language ?? UiLanguage.Japanese);
        SubscribeWorker(worker);

        try
        {
            await _translationController.StartAsync(
                credentialsResult.Credentials,
                SelectedSourceLanguage.Code,
                SelectedTargetLanguage.Code,
                SelectedAudioInputSource.Source,
                worker.RecognizerWorker);

            _currentWorker = worker;
            _startNewRecordingFileOnNextStart = false;
            IsRunning = true;
            StatusMessage = Text("開始", "Started");
            AddActivityLog(Text("翻訳を開始しました。", "Translation started."));
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
        _startNewRecordingFileOnNextStart = true;
        StatusMessage = Text("ログをクリアしました。", "Logs cleared.");
        AddActivityLog(Text("ログをクリアしました。次回開始時は新しい記録ファイルを作成します。", "Logs cleared. The next start creates a new recording file."));
        return Task.CompletedTask;
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
            AddActivityLog(Text("翻訳を停止しました。", "Translation stopped."));
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

    private void RaiseUiTextChanged()
    {
        OnPropertyChanged(nameof(ApiKeyLabel));
        OnPropertyChanged(nameof(AudioInputLabel));
        OnPropertyChanged(nameof(AzureSettingsHeader));
        OnPropertyChanged(nameof(ChooseButtonText));
        OnPropertyChanged(nameof(ClearLogsButtonText));
        OnPropertyChanged(nameof(OpenButtonText));
        OnPropertyChanged(nameof(RecordingFileNameLabel));
        OnPropertyChanged(nameof(RecordingsFolderLabel));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(ShowRecentTranslationsButtonText));
        OnPropertyChanged(nameof(SourceLanguageLabel));
        OnPropertyChanged(nameof(SourceTextHeader));
        OnPropertyChanged(nameof(StartButtonText));
        OnPropertyChanged(nameof(StatusLogHeader));
        OnPropertyChanged(nameof(StopButtonText));
        OnPropertyChanged(nameof(TargetLanguageLabel));
        OnPropertyChanged(nameof(TranslationLogHeader));
        OnPropertyChanged(nameof(TranslatedTextHeader));
        OnPropertyChanged(nameof(UiLanguageLabel));
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

    private static string CreateNewRecordingFileName(string baseFileName)
    {
        return $"{baseFileName}_{DateTime.Now:yyyyMMdd_HHmmss}";
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
