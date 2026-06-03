using System.Windows.Input;
using System.Windows;
using SpeechTranslatorDesktop.Commands;
using SpeechTranslatorDesktop.Models;
using SpeechTranslatorDesktop.Services;
using SpeechTranslatorDesktop.ViewModels;
using SpeechTranslatorShared;

namespace SpeechTranslator.Desktop.Tests;

public class MainViewModelTests
{
    [Fact]
    public void InitialState_StartEnabled_StopDisabled()
    {
        var viewModel = CreateViewModel();

        viewModel.StartCommand.CanExecute(null).Should().BeTrue();
        viewModel.StopCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void InitialState_DefaultAudioInputIsMicrophoneAndSystemAudio()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedAudioInputSource!.Source.Should().Be(AudioInputSource.MicrophoneAndSystemAudio);
    }

    [Fact]
    public async Task Start_WhenCredentialsMissing_ShowsErrorAndDoesNotStart()
    {
        var translationController = new FakeTranslationController();
        var viewModel = CreateViewModel(
            credentialsProvider: new FakeSpeechCredentialsProvider(SpeechCredentialsResult.Failure("Missing SPEECH_REGION and SPEECH_KEY.")),
            translationController: translationController);

        await ExecuteAsync(viewModel.StartCommand);

        translationController.StartCallCount.Should().Be(0);
        viewModel.StatusMessage.Should().Contain("SPEECH_REGION").And.Contain("SPEECH_KEY");
        viewModel.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WhenSavedSettingsExist_LoadsThemIntoViewModel()
    {
        var viewModel = CreateViewModel(
            settingsStore: new FakeAzureAiServiceSettingsStore
            {
                LoadedSettings = new AzureAiServiceSettings("japaneast", "saved-key")
            });

        await viewModel.InitializeAsync();

        viewModel.AzureRegion.Should().Be("japaneast");
        viewModel.AzureApiKey.Should().Be("saved-key");
        viewModel.SettingsStatusMessage.Should().Contain("読み込み");
    }

    [Fact]
    public async Task InitializeAsync_WhenSavedSettingsDoNotExist_ShowsGuidance()
    {
        var viewModel = CreateViewModel();

        await viewModel.InitializeAsync();

        viewModel.AzureRegion.Should().BeEmpty();
        viewModel.AzureApiKey.Should().BeEmpty();
        viewModel.SettingsStatusMessage.Should().Contain("保存");
        viewModel.SettingsStatusMessage.Should().Contain("SPEECH_REGION");
    }

    [Fact]
    public async Task InitializeAsync_OnFirstLaunchWithMissingSettingsDatabase_ShowsGuidanceInsteadOfFailure()
    {
        var testDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "test-artifacts",
            nameof(MainViewModelTests),
            Guid.NewGuid().ToString("N"));

        try
        {
            var databasePath = Path.Combine(testDirectory, "nested", "speech-translator-desktop.db");
            var viewModel = CreateViewModel(
                settingsStore: new SqliteAzureAiServiceSettingsStore(databasePath, new FakeSecretProtector()));

            await viewModel.InitializeAsync();

            viewModel.SettingsStatusMessage.Should().Contain("保存");
            viewModel.SettingsStatusMessage.Should().Contain("SPEECH_REGION");
            viewModel.SettingsStatusMessage.Should().NotContain("失敗");
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveSettings_WhenValuesAreValid_PersistsThem()
    {
        var settingsStore = new FakeAzureAiServiceSettingsStore();
        var viewModel = CreateViewModel(settingsStore: settingsStore);
        viewModel.AzureRegion = "japaneast";
        viewModel.AzureApiKey = "saved-key";

        await ExecuteAsync(viewModel.SaveSettingsCommand);

        settingsStore.SaveCallCount.Should().Be(1);
        settingsStore.SavedSettings.Should().BeEquivalentTo(new AzureAiServiceSettings("japaneast", "saved-key"));
        viewModel.SettingsStatusMessage.Should().Contain("保存");
    }

    [Fact]
    public async Task SaveSettings_WhenValuesAreMissing_ShowsValidationError()
    {
        var settingsStore = new FakeAzureAiServiceSettingsStore();
        var viewModel = CreateViewModel(settingsStore: settingsStore);
        viewModel.AzureRegion = "japaneast";
        viewModel.AzureApiKey = "";

        await ExecuteAsync(viewModel.SaveSettingsCommand);

        settingsStore.SaveCallCount.Should().Be(0);
        viewModel.SettingsStatusMessage.Should().Contain("API キー");
    }

    [Fact]
    public async Task OpenRecordingsFolder_OpensFolderAndLogsPath()
    {
        var recordingFileService = new FakeRecordingFileService();
        var viewModel = CreateViewModel(recordingFileService: recordingFileService);

        await ExecuteAsync(viewModel.OpenRecordingsFolderCommand);

        recordingFileService.OpenRecordingsFolderCallCount.Should().Be(1);
        viewModel.StatusMessage.Should().Be("保存先を開きました。");
        viewModel.ActivityLogs.Should().Contain(log => log.Contains(recordingFileService.RecordingsFolderPath, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChooseRecordingsFolder_WhenFolderSelected_SavesAndUpdatesPath()
    {
        var recordingFileService = new FakeRecordingFileService();
        var recordingFolderSettingsStore = new FakeRecordingFolderSettingsStore();
        var viewModel = CreateViewModel(
            recordingFileService: recordingFileService,
            recordingFolderPicker: new FakeRecordingFolderPicker(@"C:\selected-recordings"),
            recordingFolderSettingsStore: recordingFolderSettingsStore);

        await ExecuteAsync(viewModel.ChooseRecordingsFolderCommand);

        recordingFileService.RecordingsDirectory.Should().Be(@"C:\selected-recordings");
        recordingFolderSettingsStore.SavedDirectoryPath.Should().Be(@"C:\selected-recordings");
        viewModel.RecordingsFolderPath.Should().Be(@"C:\selected-recordings");
        viewModel.StatusMessage.Should().Be("保存先を変更しました。");
    }

    [Fact]
    public async Task InitializeAsync_WhenSavedRecordingsFolderExists_LoadsIt()
    {
        var recordingFileService = new FakeRecordingFileService();
        var viewModel = CreateViewModel(
            recordingFileService: recordingFileService,
            recordingFolderSettingsStore: new FakeRecordingFolderSettingsStore { LoadedDirectoryPath = @"C:\saved-recordings" });

        await viewModel.InitializeAsync();

        recordingFileService.RecordingsDirectory.Should().Be(@"C:\saved-recordings");
        viewModel.RecordingsFolderPath.Should().Be(@"C:\saved-recordings");
    }

    [Fact]
    public async Task OpenRecordingsFolder_WhenOpenFails_LogsError()
    {
        var recordingFileService = new FakeRecordingFileService
        {
            OpenRecordingsFolderException = new InvalidOperationException("open failed")
        };
        var viewModel = CreateViewModel(recordingFileService: recordingFileService);

        await ExecuteAsync(viewModel.OpenRecordingsFolderCommand);

        viewModel.StatusMessage.Should().Be("open failed");
        viewModel.ActivityLogs.Should().Contain("open failed");
    }

    [Fact]
    public async Task ClearLogs_ClearsTranslationAndActivityLogs()
    {
        var worker = new FakeDesktopTranslationWorker();
        var viewModel = CreateViewModel(workerFactory: new FakeDesktopTranslationWorkerFactory(worker));
        await ExecuteAsync(viewModel.StartCommand);
        worker.RaiseTranslationLogged(new TranslationLogItem("hello", "こんにちは"));
        worker.RaiseMessageLogged("message");

        await ExecuteAsync(viewModel.ClearLogsCommand);

        viewModel.TranslationLogs.Should().BeEmpty();
        viewModel.RecentTranslationLogs.Should().BeEmpty();
        viewModel.ActivityLogs.Should().ContainSingle(log => log.Contains("ログをクリア", StringComparison.Ordinal));
        viewModel.StatusMessage.Should().Be("ログをクリアしました。");
    }

    [Fact]
    public async Task Start_WhenPrefixIsEmpty_UsesDefaultTimestampedRecordingFile()
    {
        var translationController = new FakeTranslationController();
        var workerFactory = new FakeDesktopTranslationWorkerFactory(new FakeDesktopTranslationWorker());
        var viewModel = CreateViewModel(
            translationController: translationController,
            workerFactory: workerFactory);

        await ExecuteAsync(viewModel.StartCommand);

        workerFactory.LastRecordingFileName.Should().StartWith("session_");
        workerFactory.LastRecordingFileName.Should().NotBe("session");
        viewModel.RecordingFileName.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_WhenPrefixIsProvided_UsesPrefixTimestampedRecordingFile()
    {
        var workerFactory = new FakeDesktopTranslationWorkerFactory(new FakeDesktopTranslationWorker());
        var viewModel = CreateViewModel(workerFactory: workerFactory);
        viewModel.RecordingFileName = "build2026";

        await ExecuteAsync(viewModel.StartCommand);

        workerFactory.LastRecordingFileName.Should().StartWith("build2026_");
        workerFactory.LastRecordingFileName.Should().NotBe("build2026");
        viewModel.RecordingFileNamePreview.Should().Contain("build2026_yyyyMMdd_HHmmss.txt");
    }

    [Fact]
    public async Task Start_WhenSaveRecordingIsOff_PassesNoRecordingFile()
    {
        var workerFactory = new FakeDesktopTranslationWorkerFactory(new FakeDesktopTranslationWorker());
        var viewModel = CreateViewModel(workerFactory: workerFactory);
        viewModel.IsRecordingSaveEnabled = false;
        viewModel.RecordingFileName = "build2026";

        await ExecuteAsync(viewModel.StartCommand);

        workerFactory.LastRecordingFileName.Should().BeNull();
        viewModel.RecordingFileNamePreview.Should().Contain("保存OFF");
    }

    [Fact]
    public async Task Start_WhenCredentialsPresent_StartsTranslation()
    {
        var translationController = new FakeTranslationController();
        var viewModel = CreateViewModel(translationController: translationController);

        await ExecuteAsync(viewModel.StartCommand);

        translationController.StartCallCount.Should().Be(1);
        viewModel.StatusMessage.Should().Be(viewModel.SelectedUiLanguage?.Language == UiLanguage.English ? "Started" : "開始");
        viewModel.IsRunning.Should().BeTrue();
        viewModel.StartCommand.CanExecute(null).Should().BeFalse();
        viewModel.StopCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Start_WhenConfiguredCredentialsPresent_PassesThemToController()
    {
        var translationController = new FakeTranslationController();
        var viewModel = CreateViewModel(
            credentialsProvider: new FakeSpeechCredentialsProvider(SpeechCredentialsResult.Success(new SpeechCredentials("ui-region", "ui-key"))),
            translationController: translationController);
        viewModel.AzureRegion = "ui-region";
        viewModel.AzureApiKey = "ui-key";

        await ExecuteAsync(viewModel.StartCommand);

        translationController.LastStartCredentials.Should().BeEquivalentTo(new SpeechCredentials("ui-region", "ui-key"));
    }

    [Fact]
    public async Task Start_WhenSystemAudioSelected_PassesSystemAudioInputToController()
    {
        var translationController = new FakeTranslationController();
        var viewModel = CreateViewModel(translationController: translationController);
        viewModel.SelectedAudioInputSource = viewModel.AvailableAudioInputSources.Single(source => source.Source == AudioInputSource.SystemAudio);

        await ExecuteAsync(viewModel.StartCommand);

        translationController.LastAudioInputSource.Should().Be(AudioInputSource.SystemAudio);
    }

    [Fact]
    public async Task Start_WhenMicrophoneAndSystemAudioSelected_PassesCombinedInputToController()
    {
        var translationController = new FakeTranslationController();
        var viewModel = CreateViewModel(translationController: translationController);
        viewModel.SelectedAudioInputSource = viewModel.AvailableAudioInputSources.Single(source => source.Source == AudioInputSource.MicrophoneAndSystemAudio);

        await ExecuteAsync(viewModel.StartCommand);

        translationController.LastAudioInputSource.Should().Be(AudioInputSource.MicrophoneAndSystemAudio);
    }

    [Fact]
    public async Task Start_WhenTranscriptionOnlySelected_PassesTranscriptionModeToController()
    {
        var translationController = new FakeTranslationController();
        var workerFactory = new FakeDesktopTranslationWorkerFactory(new FakeDesktopTranslationWorker());
        var viewModel = CreateViewModel(
            translationController: translationController,
            workerFactory: workerFactory);
        viewModel.SelectedRecognitionMode = viewModel.AvailableRecognitionModes.Single(mode => mode.Mode == RecognitionMode.TranscriptionOnly);

        await ExecuteAsync(viewModel.StartCommand);

        translationController.LastRecognitionMode.Should().Be(RecognitionMode.TranscriptionOnly);
        workerFactory.LastRecognitionMode.Should().Be(RecognitionMode.TranscriptionOnly);
        viewModel.IsTranslationMode.Should().BeFalse();
        viewModel.TranslationColumnVisibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public async Task Start_SavesCurrentSelectionsAsPreferences()
    {
        var preferencesStore = new FakeAppPreferencesStore();
        var viewModel = CreateViewModel(appPreferencesStore: preferencesStore);
        viewModel.SelectedUiLanguage = viewModel.AvailableUiLanguages.Single(option => option.Language == UiLanguage.English);
        viewModel.SelectedRecognitionMode = viewModel.AvailableRecognitionModes.Single(mode => mode.Mode == RecognitionMode.TranscriptionOnly);
        viewModel.SelectedSourceLanguage = viewModel.AvailableLanguages.Single(language => language.Code == "ja-JP");
        viewModel.SelectedTargetLanguage = viewModel.AvailableLanguages.Single(language => language.Code == "en-US");
        viewModel.SelectedAudioInputSource = viewModel.AvailableAudioInputSources.Single(source => source.Source == AudioInputSource.SystemAudio);
        viewModel.IsRecordingSaveEnabled = false;
        viewModel.RecordingFileName = "build2026";

        await ExecuteAsync(viewModel.StartCommand);

        preferencesStore.SavedPreferences.Should().BeEquivalentTo(new AppPreferences(
            nameof(UiLanguage.English),
            "ja-JP",
            "en-US",
            nameof(AudioInputSource.SystemAudio),
            nameof(RecognitionMode.TranscriptionOnly),
            false,
            "build2026"));
    }

    [Fact]
    public async Task InitializeAsync_WhenPreferencesExist_RestoresLastSelections()
    {
        var viewModel = CreateViewModel(appPreferencesStore: new FakeAppPreferencesStore
        {
            LoadedPreferences = new AppPreferences(
                nameof(UiLanguage.English),
                "ja-JP",
                "en-US",
                nameof(AudioInputSource.SystemAudio),
                nameof(RecognitionMode.TranscriptionOnly),
                false,
                "build2026")
        });

        await viewModel.InitializeAsync();

        viewModel.SelectedUiLanguage!.Language.Should().Be(UiLanguage.English);
        viewModel.SelectedSourceLanguage!.Code.Should().Be("ja-JP");
        viewModel.SelectedTargetLanguage!.Code.Should().Be("en-US");
        viewModel.SelectedAudioInputSource!.Source.Should().Be(AudioInputSource.SystemAudio);
        viewModel.SelectedRecognitionMode!.Mode.Should().Be(RecognitionMode.TranscriptionOnly);
        viewModel.IsRecordingSaveEnabled.Should().BeFalse();
        viewModel.RecordingFileName.Should().Be("build2026");
        viewModel.SourceTextHeader.Should().Be("Transcript");
    }

    [Fact]
    public async Task Start_WhenRecordingFileNamePrefixIsInvalid_ShowsErrorAndDoesNotStart()
    {
        var translationController = new FakeTranslationController();
        var workerFactory = new FakeDesktopTranslationWorkerFactory(new FakeDesktopTranslationWorker());
        var viewModel = CreateViewModel(
            recordingFileService: new FakeRecordingFileService
            {
                NormalizeFileNameException = new ArgumentException("ファイル名には英数字、ハイフン、アンダースコアのみ使用できます。", "fileName")
            },
            translationController: translationController,
            workerFactory: workerFactory);
        viewModel.RecordingFileName = "bad name";

        await ExecuteAsync(viewModel.StartCommand);

        translationController.StartCallCount.Should().Be(0);
        workerFactory.CreateCallCount.Should().Be(0);
        viewModel.IsRunning.Should().BeFalse();
        viewModel.StatusMessage.Should().StartWith("ファイル名 prefix が不正です:");
        viewModel.ActivityLogs.Should().Contain(viewModel.StatusMessage);
    }

    [Fact]
    public async Task Stop_CallsTranslationController()
    {
        var translationController = new FakeTranslationController();
        var viewModel = CreateViewModel(translationController: translationController);

        await ExecuteAsync(viewModel.StartCommand);
        await ExecuteAsync(viewModel.StopCommand);

        translationController.StopCallCount.Should().Be(1);
        viewModel.StatusMessage.Should().Be(viewModel.SelectedUiLanguage?.Language == UiLanguage.English ? "Stopped" : "停止");
        viewModel.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Stop_WhenControllerThrows_ShowsErrorAndKeepsRunning()
    {
        var translationController = new FakeTranslationController
        {
            StopException = new InvalidOperationException("stop failed"),
            KeepRunningOnStopFailure = true
        };
        var viewModel = CreateViewModel(translationController: translationController);

        await ExecuteAsync(viewModel.StartCommand);
        await ExecuteAsync(viewModel.StopCommand);

        viewModel.StatusMessage.Should().Be(viewModel.SelectedUiLanguage?.Language == UiLanguage.English ? "Failed to stop: stop failed" : "停止に失敗しました: stop failed");
        viewModel.ActivityLogs.Should().Contain(viewModel.StatusMessage);
        viewModel.IsRunning.Should().BeTrue();
        viewModel.StartCommand.CanExecute(null).Should().BeFalse();
        viewModel.StopCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task WorkerTranslationEvent_AddsTranslationLog()
    {
        var worker = new FakeDesktopTranslationWorker();
        var viewModel = CreateViewModel(workerFactory: new FakeDesktopTranslationWorkerFactory(worker));

        await ExecuteAsync(viewModel.StartCommand);
        worker.RaiseTranslationLogged(new TranslationLogItem("hello", "こんにちは"));

        viewModel.TranslationLogs.Should().ContainSingle();
        viewModel.TranslationLogs[0].SourceText.Should().Be("hello");
        viewModel.TranslationLogs[0].TranslatedText.Should().Be("こんにちは");
    }

    [Fact]
    public async Task CopyAllLogs_CopiesAllLogsInDisplayedOrder()
    {
        var worker = new FakeDesktopTranslationWorker();
        var clipboardService = new FakeClipboardService();
        var viewModel = CreateViewModel(
            clipboardService: clipboardService,
            workerFactory: new FakeDesktopTranslationWorkerFactory(worker));

        await ExecuteAsync(viewModel.StartCommand);
        worker.RaiseTranslationLogged(new TranslationLogItem("first", "最初"));
        worker.RaiseTranslationLogged(new TranslationLogItem("second", "次"));

        await ExecuteAsync(viewModel.CopyAllLogsCommand);

        clipboardService.LastText.Should().Contain("second").And.Contain("first");
        clipboardService.LastText!.IndexOf("second", StringComparison.Ordinal)
            .Should()
            .BeLessThan(clipboardService.LastText.IndexOf("first", StringComparison.Ordinal));
        viewModel.StatusMessage.Should().Be("コピーしました。");
    }

    [Fact]
    public async Task CopyTranslationLog_CopiesSingleLogBlock()
    {
        var worker = new FakeDesktopTranslationWorker();
        var clipboardService = new FakeClipboardService();
        var viewModel = CreateViewModel(
            clipboardService: clipboardService,
            workerFactory: new FakeDesktopTranslationWorkerFactory(worker));

        await ExecuteAsync(viewModel.StartCommand);
        worker.RaiseTranslationLogged(new TranslationLogItem("hello", "こんにちは"));

        await ExecuteAsync(viewModel.CopyTranslationLogCommand, viewModel.TranslationLogs[0]);

        clipboardService.LastText.Should().Be($"Source:{Environment.NewLine}hello{Environment.NewLine}{Environment.NewLine}Translation:{Environment.NewLine}こんにちは");
        viewModel.StatusMessage.Should().Be("コピーしました。");
    }

    [Fact]
    public async Task CopyTranslationLog_WhenTranscriptOnly_CopiesSourceOnly()
    {
        var worker = new FakeDesktopTranslationWorker();
        var clipboardService = new FakeClipboardService();
        var viewModel = CreateViewModel(
            clipboardService: clipboardService,
            workerFactory: new FakeDesktopTranslationWorkerFactory(worker));

        await ExecuteAsync(viewModel.StartCommand);
        worker.RaiseTranslationLogged(new TranslationLogItem("transcript", string.Empty));

        await ExecuteAsync(viewModel.CopyTranslationLogCommand, viewModel.TranslationLogs[0]);

        clipboardService.LastText.Should().Be("transcript");
    }

    [Fact]
    public async Task CopyAllLogs_WhenNoLogs_ShowsMessageWithoutClipboardWrite()
    {
        var clipboardService = new FakeClipboardService();
        var viewModel = CreateViewModel(clipboardService: clipboardService);

        await ExecuteAsync(viewModel.CopyAllLogsCommand);

        clipboardService.LastText.Should().BeNull();
        viewModel.StatusMessage.Should().Be("コピーするログがありません。");
    }

    [Fact]
    public async Task CopyAllLogs_WhenClipboardFails_ShowsError()
    {
        var worker = new FakeDesktopTranslationWorker();
        var clipboardService = new FakeClipboardService
        {
            SetTextException = new InvalidOperationException("clipboard locked")
        };
        var viewModel = CreateViewModel(
            clipboardService: clipboardService,
            workerFactory: new FakeDesktopTranslationWorkerFactory(worker));
        await ExecuteAsync(viewModel.StartCommand);
        worker.RaiseTranslationLogged(new TranslationLogItem("hello", "こんにちは"));

        await ExecuteAsync(viewModel.CopyAllLogsCommand);

        viewModel.StatusMessage.Should().Be("コピーに失敗しました: clipboard locked");
        viewModel.ActivityLogs.Should().Contain(viewModel.StatusMessage);
    }

    [Fact]
    public async Task WorkerTranslationEvent_AddsNewestTranslationLogFirst()
    {
        var worker = new FakeDesktopTranslationWorker();
        var viewModel = CreateViewModel(workerFactory: new FakeDesktopTranslationWorkerFactory(worker));

        await ExecuteAsync(viewModel.StartCommand);
        worker.RaiseTranslationLogged(new TranslationLogItem("first", "最初"));
        worker.RaiseTranslationLogged(new TranslationLogItem("second", "次"));

        viewModel.TranslationLogs.Select(item => item.SourceText).Should().Equal("second", "first");
    }

    [Fact]
    public async Task WorkerTranslationEvent_KeepsRecentTranslationLogsToLatestThree()
    {
        var worker = new FakeDesktopTranslationWorker();
        var viewModel = CreateViewModel(workerFactory: new FakeDesktopTranslationWorkerFactory(worker));

        await ExecuteAsync(viewModel.StartCommand);
        worker.RaiseTranslationLogged(new TranslationLogItem("one", "1"));
        worker.RaiseTranslationLogged(new TranslationLogItem("two", "2"));
        worker.RaiseTranslationLogged(new TranslationLogItem("three", "3"));
        worker.RaiseTranslationLogged(new TranslationLogItem("four", "4"));

        viewModel.RecentTranslationLogs.Select(item => item.SourceText).Should().Equal("four", "three", "two");
        viewModel.TranslationLogs.Select(item => item.SourceText).Should().StartWith("four", "three", "two", "one");
    }

    [Fact]
    public void SelectedUiLanguage_WhenEnglish_UpdatesUiLabelsAndAudioInputLabels()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedUiLanguage = viewModel.AvailableUiLanguages.Single(option => option.Language == UiLanguage.English);

        viewModel.SourceLanguageLabel.Should().Be("Speaker language");
        viewModel.TargetLanguageLabel.Should().Be("Target language");
        viewModel.StartButtonText.Should().Be("Start");
        viewModel.StopButtonText.Should().Be("Stop");
        viewModel.SettingsButtonText.Should().Be("Settings");
        viewModel.SettingsWindowTitle.Should().Be("Settings");
        viewModel.ShowRecentTranslationsButtonText.Should().Be("Open live notes window");
        viewModel.TranslationsWindowTitle.Should().Be("Live notes (latest 3)");
        viewModel.TranslationLogHeader.Should().Be("Translation log");
        viewModel.StatusLabel.Should().Be("Status");
        viewModel.StatusMessage.Should().Be("Stopped");
        viewModel.AvailableAudioInputSources.Select(option => option.DisplayName)
            .Should()
            .Equal("Microphone", "PC audio (default playback device)", "Microphone + PC audio");
    }

    [Fact]
    public void AvailableLanguages_ContainsMajorSpeechTranslationLanguages()
    {
        var viewModel = CreateViewModel();

        viewModel.AvailableLanguages.Select(language => language.Code)
            .Should()
            .Contain(["en-US", "ja-JP", "zh-CN", "zh-TW", "ko-KR", "fr-FR", "de-DE", "es-ES", "it-IT", "pt-BR", "id-ID", "th-TH", "vi-VN"]);
    }

    [Fact]
    public async Task WorkerStatusEvents_UpdateStatusAndLogs()
    {
        var worker = new FakeDesktopTranslationWorker();
        var viewModel = CreateViewModel(workerFactory: new FakeDesktopTranslationWorkerFactory(worker));

        await ExecuteAsync(viewModel.StartCommand);
        worker.RaiseStatusChanged(DesktopTranslationStatus.NoMatch, "NoMatch");
        worker.RaiseMessageLogged("Session stopped.");
        worker.RaiseStatusChanged(DesktopTranslationStatus.SessionStopped, "セッション停止");

        viewModel.StatusMessage.Should().Be("セッション停止");
        viewModel.ActivityLogs.Should().Contain("Session stopped.");
        viewModel.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task WorkerStatusEvents_AddsNewestStatusLogFirst()
    {
        var worker = new FakeDesktopTranslationWorker();
        var viewModel = CreateViewModel(workerFactory: new FakeDesktopTranslationWorkerFactory(worker));

        await ExecuteAsync(viewModel.StartCommand);
        worker.RaiseMessageLogged("first");
        worker.RaiseMessageLogged("second");

        viewModel.ActivityLogs.Take(2).Should().Equal("second", "first");
    }

    [Fact]
    public async Task Start_WhenControllerCompletesAsynchronously_RaisesPropertyChangesOnCapturedContext()
    {
        await RunOnSynchronizationContextAsync(async uiThreadId =>
        {
            var translationController = new FakeTranslationController { StartShouldYield = true };
            var viewModel = CreateViewModel(
                dispatcher: new SynchronizationContextDispatcher(SynchronizationContext.Current!),
                translationController: translationController);
            var propertyChangedThreadIds = new List<int>();
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(MainViewModel.IsRunning) or nameof(MainViewModel.StatusMessage))
                {
                    propertyChangedThreadIds.Add(Environment.CurrentManagedThreadId);
                }
            };

            await ExecuteAsync(viewModel.StartCommand);

            propertyChangedThreadIds.Should().NotBeEmpty();
            propertyChangedThreadIds.Should().OnlyContain(threadId => threadId == uiThreadId);
        });
    }

    [Fact]
    public async Task WorkerTranslationEvent_FromBackgroundThread_UpdatesCollectionOnUiThread()
    {
        await RunOnSynchronizationContextAsync(async uiThreadId =>
        {
            var worker = new FakeDesktopTranslationWorker();
            var viewModel = CreateViewModel(
                dispatcher: new SynchronizationContextDispatcher(SynchronizationContext.Current!),
                workerFactory: new FakeDesktopTranslationWorkerFactory(worker));
            int? collectionChangedThreadId = null;

            viewModel.TranslationLogs.CollectionChanged += (_, _) => collectionChangedThreadId = Environment.CurrentManagedThreadId;

            await ExecuteAsync(viewModel.StartCommand);
            await Task.Run(() => worker.RaiseTranslationLogged(new TranslationLogItem("hello", "こんにちは")));

            collectionChangedThreadId.Should().Be(uiThreadId);
            viewModel.TranslationLogs.Should().ContainSingle();
        });
    }

    private static MainViewModel CreateViewModel(
        IUiDispatcher? dispatcher = null,
        ISpeechCredentialsProvider? credentialsProvider = null,
        IAzureAiServiceSettingsStore? settingsStore = null,
        IRecordingFileService? recordingFileService = null,
        IRecordingFolderPicker? recordingFolderPicker = null,
        IRecordingFolderSettingsStore? recordingFolderSettingsStore = null,
        IAppPreferencesStore? appPreferencesStore = null,
        IClipboardService? clipboardService = null,
        ITranslationController? translationController = null,
        IDesktopTranslationWorkerFactory? workerFactory = null)
    {
        var viewModel = new MainViewModel(
            dispatcher ?? new ImmediateDispatcher(),
            credentialsProvider ?? new FakeSpeechCredentialsProvider(SpeechCredentialsResult.Success(new SpeechCredentials("japaneast", "test-key"))),
            settingsStore ?? new FakeAzureAiServiceSettingsStore(),
            recordingFileService ?? new FakeRecordingFileService(),
            recordingFolderPicker ?? new FakeRecordingFolderPicker(null),
            recordingFolderSettingsStore ?? new FakeRecordingFolderSettingsStore(),
            appPreferencesStore ?? new FakeAppPreferencesStore(),
            clipboardService ?? new FakeClipboardService(),
            translationController ?? new FakeTranslationController(),
            workerFactory ?? new FakeDesktopTranslationWorkerFactory(new FakeDesktopTranslationWorker()));

        viewModel.SelectedUiLanguage = viewModel.AvailableUiLanguages.Single(option => option.Language == UiLanguage.Japanese);
        return viewModel;
    }

    private static Task ExecuteAsync(ICommand command)
    {
        return ((AsyncRelayCommand)command).ExecuteAsync(null);
    }

    private static Task ExecuteAsync(ICommand command, object? parameter)
    {
        return ((ParameterizedAsyncRelayCommand)command).ExecuteAsync(parameter);
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Invoke(Action action) => action();
    }

    private sealed class FakeSpeechCredentialsProvider : ISpeechCredentialsProvider
    {
        private readonly SpeechCredentialsResult _result;

        public FakeSpeechCredentialsProvider(SpeechCredentialsResult result)
        {
            _result = result;
        }

        public SpeechCredentialsResult GetCredentials(string? preferredRegion = null, string? preferredKey = null) => _result;
    }

    private sealed class FakeAzureAiServiceSettingsStore : IAzureAiServiceSettingsStore
    {
        public AzureAiServiceSettings? LoadedSettings { get; init; }
        public AzureAiServiceSettings? SavedSettings { get; private set; }
        public int SaveCallCount { get; private set; }

        public Task<AzureAiServiceSettings?> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LoadedSettings);
        }

        public Task SaveAsync(AzureAiServiceSettings settings, CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            SavedSettings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSecretProtector : ISecretProtector
    {
        public byte[] Protect(string plaintext) => Encoding.UTF8.GetBytes(plaintext);

        public string Unprotect(byte[] protectedData) => Encoding.UTF8.GetString(protectedData);
    }

    private sealed class FakeTranslationController : ITranslationController
    {
        public int StartCallCount { get; private set; }
        public int StopCallCount { get; private set; }
        public bool IsRunning { get; private set; }
        public bool StartShouldYield { get; init; }
        public Exception? StopException { get; init; }
        public bool KeepRunningOnStopFailure { get; init; }
        public SpeechCredentials? LastStartCredentials { get; private set; }
        public AudioInputSource? LastAudioInputSource { get; private set; }
        public RecognitionMode? LastRecognitionMode { get; private set; }

        public Task StartAsync(SpeechCredentials credentials, string sourceLanguage, string targetLanguage, AudioInputSource audioInputSource, RecognitionMode recognitionMode, IDesktopTranslationWorker worker, CancellationToken cancellationToken = default)
        {
            return StartAsyncCore();

            async Task StartAsyncCore()
            {
                if (StartShouldYield)
                {
                    await Task.Yield();
                }

                StartCallCount++;
                LastStartCredentials = credentials;
                LastAudioInputSource = audioInputSource;
                LastRecognitionMode = recognitionMode;
                IsRunning = true;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            return StopAsyncCore();

            Task StopAsyncCore()
            {
                StopCallCount++;

                if (StopException is not null)
                {
                    if (!KeepRunningOnStopFailure)
                    {
                        IsRunning = false;
                    }

                    throw StopException;
                }

                IsRunning = false;
                return Task.CompletedTask;
            }
        }
    }

    private sealed class FakeDesktopTranslationWorkerFactory : IDesktopTranslationWorkerFactory
    {
        private readonly IDesktopTranslationWorker _worker;
        public int CreateCallCount { get; private set; }
        public string? LastRecordingFileName { get; private set; }
        public RecognitionMode? LastRecognitionMode { get; private set; }

        public FakeDesktopTranslationWorkerFactory(IDesktopTranslationWorker worker)
        {
            _worker = worker;
        }

        public IDesktopTranslationWorker Create(string targetLanguage, string? recordingFileName, UiLanguage uiLanguage, RecognitionMode recognitionMode)
        {
            CreateCallCount++;
            LastRecordingFileName = recordingFileName;
            LastRecognitionMode = recognitionMode;
            return _worker;
        }
    }

    private sealed class FakeRecordingFileService : IRecordingFileService
    {
        private string _recordingsFolderPath = @"C:\recordings";
        public Exception? NormalizeFileNameException { get; init; }
        public Exception? OpenRecordingsFolderException { get; init; }
        public int OpenRecordingsFolderCallCount { get; private set; }
        public string RecordingsFolderPath => _recordingsFolderPath;
        public string RecordingsDirectory => _recordingsFolderPath;

        public string? NormalizeFileName(string? fileName)
        {
            if (NormalizeFileNameException is not null)
            {
                throw NormalizeFileNameException;
            }

            return string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();
        }

        public void AppendTranslation(string? fileName, string sourceText, string translatedText)
        {
        }

        public void AppendTranscription(string? fileName, string sourceText)
        {
        }

        public string OpenRecordingsFolder()
        {
            OpenRecordingsFolderCallCount++;

            if (OpenRecordingsFolderException is not null)
            {
                throw OpenRecordingsFolderException;
            }

            return RecordingsFolderPath;
        }

        public void SetRecordingsDirectory(string directoryPath)
        {
            _recordingsFolderPath = directoryPath;
        }
    }

    private sealed class FakeRecordingFolderPicker : IRecordingFolderPicker
    {
        private readonly string? _selectedFolder;

        public FakeRecordingFolderPicker(string? selectedFolder)
        {
            _selectedFolder = selectedFolder;
        }

        public string? PickFolder(string initialDirectory) => _selectedFolder;
    }

    private sealed class FakeRecordingFolderSettingsStore : IRecordingFolderSettingsStore
    {
        public string? LoadedDirectoryPath { get; init; }
        public string? SavedDirectoryPath { get; private set; }

        public Task<string?> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LoadedDirectoryPath);
        }

        public Task SaveAsync(string directoryPath, CancellationToken cancellationToken = default)
        {
            SavedDirectoryPath = directoryPath;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAppPreferencesStore : IAppPreferencesStore
    {
        public AppPreferences? LoadedPreferences { get; init; }
        public AppPreferences? SavedPreferences { get; private set; }

        public Task<AppPreferences?> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(LoadedPreferences);
        }

        public Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default)
        {
            SavedPreferences = preferences;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public Exception? SetTextException { get; init; }
        public string? LastText { get; private set; }

        public Task SetTextAsync(string text)
        {
            if (SetTextException is not null)
            {
                throw SetTextException;
            }

            LastText = text;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDesktopTranslationWorker : IDesktopTranslationWorker
    {
        public TranslationRecognizerWorkerBase RecognizerWorker { get; } = new NoOpTranslationRecognizerWorker();

        public SpeechRecognizerWorkerBase SpeechRecognizerWorker { get; } = new NoOpSpeechRecognizerWorker();

        public event EventHandler<string>? MessageLogged;

        public event EventHandler<WorkerStatusChangedEventArgs>? StatusChanged;

        public event EventHandler<TranslationLogItem>? TranslationLogged;

        public void RaiseMessageLogged(string message) => MessageLogged?.Invoke(this, message);

        public void RaiseStatusChanged(DesktopTranslationStatus status, string message) =>
            StatusChanged?.Invoke(this, new WorkerStatusChangedEventArgs(status, message));

        public void RaiseTranslationLogged(TranslationLogItem item) => TranslationLogged?.Invoke(this, item);
    }

    private sealed class NoOpTranslationRecognizerWorker : TranslationRecognizerWorkerBase
    {
        public override void OnCanceled(TranslationRecognitionCanceledEventArgs e)
        {
        }

        public override void OnRecognized(TranslationRecognitionEventArgs e)
        {
        }

        public override void OnRecognizing(TranslationRecognitionEventArgs e)
        {
        }

        public override void OnSessionStarted(SessionEventArgs e)
        {
        }

        public override void OnSessionStopped(SessionEventArgs e)
        {
        }

        public override void OnSpeechEndDetected(RecognitionEventArgs e)
        {
        }

        public override void OnSpeechStartDetected(RecognitionEventArgs e)
        {
        }
    }

    private sealed class NoOpSpeechRecognizerWorker : SpeechRecognizerWorkerBase
    {
        public override void OnCanceled(SpeechRecognitionCanceledEventArgs e)
        {
        }

        public override void OnRecognized(SpeechRecognitionEventArgs e)
        {
        }

        public override void OnRecognizing(SpeechRecognitionEventArgs e)
        {
        }

        public override void OnSessionStarted(SessionEventArgs e)
        {
        }

        public override void OnSessionStopped(SessionEventArgs e)
        {
        }

        public override void OnSpeechEndDetected(RecognitionEventArgs e)
        {
        }

        public override void OnSpeechStartDetected(RecognitionEventArgs e)
        {
        }
    }

    private sealed class SynchronizationContextDispatcher : IUiDispatcher
    {
        private readonly SynchronizationContext _synchronizationContext;

        public SynchronizationContextDispatcher(SynchronizationContext synchronizationContext)
        {
            _synchronizationContext = synchronizationContext;
        }

        public void Invoke(Action action)
        {
            if (SynchronizationContext.Current == _synchronizationContext)
            {
                action();
                return;
            }

            _synchronizationContext.Send(_ => action(), null);
        }
    }

    private static Task RunOnSynchronizationContextAsync(Func<int, Task> testAction)
    {
        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            Exception? exception = null;
            var synchronizationContext = new PumpingSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synchronizationContext);

            try
            {
                var task = testAction(Environment.CurrentManagedThreadId);
                task.ContinueWith(
                    completedTask =>
                    {
                        exception = completedTask.Exception?.GetBaseException();
                        synchronizationContext.Complete();
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
                synchronizationContext.RunOnCurrentThread();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }

            if (exception is not null)
            {
                completionSource.SetException(exception);
                return;
            }

            completionSource.SetResult();
        });

        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return completionSource.Task;
    }

    private sealed class PumpingSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _workItems = new();
        private readonly AutoResetEvent _workItemsWaiting = new(false);
        private readonly int _threadId = Environment.CurrentManagedThreadId;
        private bool _completed;

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_workItems)
            {
                _workItems.Enqueue((d, state));
            }

            _workItemsWaiting.Set();
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            if (Environment.CurrentManagedThreadId == _threadId)
            {
                d(state);
                return;
            }

            using var completed = new ManualResetEventSlim();
            Exception? capturedException = null;
            Post(_ =>
            {
                try
                {
                    d(state);
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
                finally
                {
                    completed.Set();
                }
            }, null);

            completed.Wait();

            if (capturedException is not null)
            {
                throw capturedException;
            }
        }

        public void Complete()
        {
            _completed = true;
            _workItemsWaiting.Set();
        }

        public void RunOnCurrentThread()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State)? workItem = null;

                lock (_workItems)
                {
                    if (_workItems.Count > 0)
                    {
                        workItem = _workItems.Dequeue();
                    }
                    else if (_completed)
                    {
                        return;
                    }
                }

                if (workItem is { } item)
                {
                    item.Callback(item.State);
                    continue;
                }

                _workItemsWaiting.WaitOne();
            }
        }
    }
}
