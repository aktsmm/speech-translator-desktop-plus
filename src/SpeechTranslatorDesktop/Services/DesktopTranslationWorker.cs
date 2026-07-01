using SpeechTranslatorDesktop.Models;
using SpeechTranslatorShared;

namespace SpeechTranslatorDesktop.Services;

public sealed class DesktopTranslationWorker : TranslationRecognizerWorkerBase, IDesktopTranslationWorker
{
    private readonly string _targetLanguage;
    private readonly string? _recordingFileName;
    private readonly IRecordingFileService _recordingFileService;
    private readonly UiLanguage _uiLanguage;
    private readonly RecognitionMode _recognitionMode;
    private readonly bool _labelMicrophoneAsSelf;
    private readonly SpeechRecognizerWorkerBase _speechRecognizerWorker;
    private readonly TranslationRecognizerWorkerBase _microphoneRecognizerWorker;
    private readonly TranslationRecognizerWorkerBase _systemAudioRecognizerWorker;
    private readonly SpeechRecognizerWorkerBase _microphoneSpeechRecognizerWorker;
    private readonly SpeechRecognizerWorkerBase _systemAudioSpeechRecognizerWorker;

    public DesktopTranslationWorker(
        string targetLanguage,
        string? recordingFileName,
        IRecordingFileService recordingFileService,
        UiLanguage uiLanguage = UiLanguage.Japanese,
        RecognitionMode recognitionMode = RecognitionMode.Translation,
        AudioInputSource audioInputSource = AudioInputSource.Microphone)
    {
        if (string.IsNullOrWhiteSpace(targetLanguage))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(targetLanguage));
        }

        _targetLanguage = targetLanguage;
        _recordingFileName = recordingFileName;
        _recordingFileService = recordingFileService ?? throw new ArgumentNullException(nameof(recordingFileService));
        _uiLanguage = uiLanguage;
        _recognitionMode = recognitionMode;
        _labelMicrophoneAsSelf = audioInputSource == AudioInputSource.MicrophoneAndSystemAudio;
        _speechRecognizerWorker = new SpeechWorker(this, AudioSourceKind.Unspecified, emitTerminalStatus: true);
        _microphoneRecognizerWorker = new SourceTranslationWorker(this, AudioSourceKind.Microphone);
        _systemAudioRecognizerWorker = new SourceTranslationWorker(this, AudioSourceKind.SystemAudio);
        _microphoneSpeechRecognizerWorker = new SpeechWorker(this, AudioSourceKind.Microphone, emitTerminalStatus: false);
        _systemAudioSpeechRecognizerWorker = new SpeechWorker(this, AudioSourceKind.SystemAudio, emitTerminalStatus: false);
    }

    public TranslationRecognizerWorkerBase RecognizerWorker => this;

    public TranslationRecognizerWorkerBase MicrophoneRecognizerWorker => _microphoneRecognizerWorker;

    public TranslationRecognizerWorkerBase SystemAudioRecognizerWorker => _systemAudioRecognizerWorker;

    public SpeechRecognizerWorkerBase SpeechRecognizerWorker => _speechRecognizerWorker;

    public SpeechRecognizerWorkerBase MicrophoneSpeechRecognizerWorker => _microphoneSpeechRecognizerWorker;

    public SpeechRecognizerWorkerBase SystemAudioSpeechRecognizerWorker => _systemAudioSpeechRecognizerWorker;

    public event EventHandler<string>? MessageLogged;

    public event EventHandler<WorkerStatusChangedEventArgs>? StatusChanged;

    public event EventHandler<TranslationLogItem>? TranslationLogged;

    public override void OnRecognizing(TranslationRecognitionEventArgs e)
    {
        RaiseStatusChanged(DesktopTranslationStatus.Recognizing, Text("認識中", "Recognizing"));
    }

    public override void OnRecognized(TranslationRecognitionEventArgs e)
    {
        var result = e.Result;

        if (result.Reason == ResultReason.TranslatedSpeech)
        {
            var translatedText = result.Translations.TryGetValue(_targetLanguage, out var value)
                ? value
                : result.Translations.Values.FirstOrDefault() ?? string.Empty;
            HandleTranslatedSpeech(result.Text, translatedText);
            return;
        }

        if (result.Reason == ResultReason.RecognizedSpeech)
        {
            RaiseStatusChanged(DesktopTranslationStatus.RecognizedSpeech, Text("認識のみ", "Recognized only"));
            MessageLogged?.Invoke(this, Text($"認識のみ: {result.Text}", $"Recognized only: {result.Text}"));
            return;
        }

        if (result.Reason == ResultReason.NoMatch)
        {
            RaiseStatusChanged(DesktopTranslationStatus.NoMatch, "NoMatch");
            MessageLogged?.Invoke(this, "NOMATCH: Speech could not be recognized.");
        }
    }

    public override void OnCanceled(TranslationRecognitionCanceledEventArgs e)
    {
        var message = e.Reason == CancellationReason.Error
            ? $"Cancel/Error: {e.ErrorDetails}"
            : $"Canceled: {e.Reason}";

        RaiseStatusChanged(DesktopTranslationStatus.Canceled, "Cancel/Error");
        MessageLogged?.Invoke(this, message);
    }

    public override void OnSpeechStartDetected(RecognitionEventArgs e)
    {
        RaiseStatusChanged(DesktopTranslationStatus.SpeechStartDetected, Text("音声開始を検出", "Speech start detected"));
    }

    public override void OnSpeechEndDetected(RecognitionEventArgs e)
    {
        RaiseStatusChanged(DesktopTranslationStatus.SpeechEndDetected, Text("音声終了を検出", "Speech end detected"));
    }

    public override void OnSessionStarted(SessionEventArgs e)
    {
        RaiseStatusChanged(DesktopTranslationStatus.SessionStarted, Text("セッション開始", "Session started"));
    }

    public override void OnSessionStopped(SessionEventArgs e)
    {
        RaiseStatusChanged(DesktopTranslationStatus.SessionStopped, Text("セッション停止", "Session stopped"));
    }

    internal void HandleTranslatedSpeech(string sourceText, string translatedText, AudioSourceKind audioSource = AudioSourceKind.Unspecified)
    {
        if (string.IsNullOrWhiteSpace(sourceText) || string.IsNullOrWhiteSpace(translatedText))
        {
            return;
        }

        var translation = new TranslationLogItem(sourceText.Trim(), translatedText.Trim(), audioSource, GetSpeakerLabel(audioSource));
        TranslationLogged?.Invoke(this, translation);
        MessageLogged?.Invoke(this, Text($"原文: {translation.DisplaySourceText}", $"Source: {translation.DisplaySourceText}"));
        MessageLogged?.Invoke(this, Text($"翻訳: {translation.TranslatedText}", $"Translation: {translation.TranslatedText}"));

        try
        {
            _recordingFileService.AppendTranslation(_recordingFileName, translation.SourceText, translation.TranslatedText, translation.SpeakerLabel);
            RaiseStatusChanged(DesktopTranslationStatus.TranslatedSpeech, Text("翻訳成功", "Translation succeeded"));
        }
        catch (Exception ex)
        {
            RaiseStatusChanged(DesktopTranslationStatus.Error, Text($"記録ファイルの保存に失敗しました: {ex.Message}", $"Failed to save recording file: {ex.Message}"));
        }
    }

    internal void HandleTranscribedSpeech(string sourceText, AudioSourceKind audioSource = AudioSourceKind.Unspecified)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return;
        }

        var transcript = new TranslationLogItem(sourceText.Trim(), string.Empty, audioSource, GetSpeakerLabel(audioSource));
        TranslationLogged?.Invoke(this, transcript);
        MessageLogged?.Invoke(this, Text($"書き起こし: {transcript.DisplaySourceText}", $"Transcript: {transcript.DisplaySourceText}"));

        try
        {
            _recordingFileService.AppendTranscription(_recordingFileName, transcript.SourceText, transcript.SpeakerLabel);
            RaiseStatusChanged(DesktopTranslationStatus.TranslatedSpeech, Text("書き起こし成功", "Transcription succeeded"));
        }
        catch (Exception ex)
        {
            RaiseStatusChanged(DesktopTranslationStatus.Error, Text($"記録ファイルの保存に失敗しました: {ex.Message}", $"Failed to save recording file: {ex.Message}"));
        }
    }

    private void RaiseStatusChanged(DesktopTranslationStatus status, string message)
    {
        StatusChanged?.Invoke(this, new WorkerStatusChangedEventArgs(status, message));
    }

    private string Text(string japanese, string english)
    {
        return _uiLanguage == UiLanguage.English ? english : japanese;
    }

    private string? GetSpeakerLabel(AudioSourceKind audioSource)
    {
        return _labelMicrophoneAsSelf && audioSource == AudioSourceKind.Microphone
            ? Text("自分", "Me")
            : null;
    }

    internal void NotifyCombinedSessionStopped()
    {
        RaiseStatusChanged(DesktopTranslationStatus.SessionStopped, Text("セッション停止", "Session stopped"));
    }

    private sealed class SourceTranslationWorker : TranslationRecognizerWorkerBase
    {
        private readonly DesktopTranslationWorker _owner;
        private readonly AudioSourceKind _audioSource;

        public SourceTranslationWorker(DesktopTranslationWorker owner, AudioSourceKind audioSource)
        {
            _owner = owner;
            _audioSource = audioSource;
        }

        public override void OnRecognizing(TranslationRecognitionEventArgs e)
        {
            _owner.RaiseStatusChanged(DesktopTranslationStatus.Recognizing, _owner.Text("認識中", "Recognizing"));
        }

        public override void OnRecognized(TranslationRecognitionEventArgs e)
        {
            var result = e.Result;

            if (result.Reason == ResultReason.TranslatedSpeech)
            {
                var translatedText = result.Translations.TryGetValue(_owner._targetLanguage, out var value)
                    ? value
                    : result.Translations.Values.FirstOrDefault() ?? string.Empty;
                _owner.HandleTranslatedSpeech(result.Text, translatedText, _audioSource);
                return;
            }

            if (result.Reason == ResultReason.RecognizedSpeech)
            {
                _owner.RaiseStatusChanged(DesktopTranslationStatus.RecognizedSpeech, _owner.Text("認識のみ", "Recognized only"));
                _owner.MessageLogged?.Invoke(_owner, _owner.Text($"認識のみ: {result.Text}", $"Recognized only: {result.Text}"));
                return;
            }

            if (result.Reason == ResultReason.NoMatch)
            {
                _owner.RaiseStatusChanged(DesktopTranslationStatus.NoMatch, "NoMatch");
                _owner.MessageLogged?.Invoke(_owner, "NOMATCH: Speech could not be recognized.");
            }
        }

        public override void OnCanceled(TranslationRecognitionCanceledEventArgs e)
        {
            var message = e.Reason == CancellationReason.Error
                ? $"Cancel/Error: {e.ErrorDetails}"
                : $"Canceled: {e.Reason}";

            _owner.MessageLogged?.Invoke(_owner, message);
        }

        public override void OnSpeechStartDetected(RecognitionEventArgs e)
        {
            _owner.RaiseStatusChanged(DesktopTranslationStatus.SpeechStartDetected, _owner.Text("音声開始を検出", "Speech start detected"));
        }

        public override void OnSpeechEndDetected(RecognitionEventArgs e)
        {
            _owner.RaiseStatusChanged(DesktopTranslationStatus.SpeechEndDetected, _owner.Text("音声終了を検出", "Speech end detected"));
        }

        public override void OnSessionStarted(SessionEventArgs e)
        {
            _owner.RaiseStatusChanged(DesktopTranslationStatus.SessionStarted, _owner.Text("セッション開始", "Session started"));
        }

        public override void OnSessionStopped(SessionEventArgs e)
        {
            _owner.MessageLogged?.Invoke(_owner, _owner.Text("入力セッションを停止しました。", "Input session stopped."));
        }
    }

    private sealed class SpeechWorker : SpeechRecognizerWorkerBase
    {
        private readonly DesktopTranslationWorker _owner;
        private readonly AudioSourceKind _audioSource;
        private readonly bool _emitTerminalStatus;

        public SpeechWorker(DesktopTranslationWorker owner, AudioSourceKind audioSource, bool emitTerminalStatus)
        {
            _owner = owner;
            _audioSource = audioSource;
            _emitTerminalStatus = emitTerminalStatus;
        }

        public override void OnRecognizing(SpeechRecognitionEventArgs e)
        {
            _owner.RaiseStatusChanged(DesktopTranslationStatus.Recognizing, _owner.Text("認識中", "Recognizing"));
        }

        public override void OnRecognized(SpeechRecognitionEventArgs e)
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                _owner.HandleTranscribedSpeech(e.Result.Text, _audioSource);
                return;
            }

            if (e.Result.Reason == ResultReason.NoMatch)
            {
                _owner.RaiseStatusChanged(DesktopTranslationStatus.NoMatch, "NoMatch");
                _owner.MessageLogged?.Invoke(_owner, "NOMATCH: Speech could not be recognized.");
            }
        }

        public override void OnCanceled(SpeechRecognitionCanceledEventArgs e)
        {
            var message = e.Reason == CancellationReason.Error
                ? $"Cancel/Error: {e.ErrorDetails}"
                : $"Canceled: {e.Reason}";

            if (_emitTerminalStatus)
            {
                _owner.RaiseStatusChanged(DesktopTranslationStatus.Canceled, "Cancel/Error");
            }

            _owner.MessageLogged?.Invoke(_owner, message);
        }

        public override void OnSpeechStartDetected(RecognitionEventArgs e)
        {
            _owner.RaiseStatusChanged(DesktopTranslationStatus.SpeechStartDetected, _owner.Text("音声開始を検出", "Speech start detected"));
        }

        public override void OnSpeechEndDetected(RecognitionEventArgs e)
        {
            _owner.RaiseStatusChanged(DesktopTranslationStatus.SpeechEndDetected, _owner.Text("音声終了を検出", "Speech end detected"));
        }

        public override void OnSessionStarted(SessionEventArgs e)
        {
            _owner.RaiseStatusChanged(DesktopTranslationStatus.SessionStarted, _owner.Text("セッション開始", "Session started"));
        }

        public override void OnSessionStopped(SessionEventArgs e)
        {
            if (_emitTerminalStatus)
            {
                _owner.RaiseStatusChanged(DesktopTranslationStatus.SessionStopped, _owner.Text("セッション停止", "Session stopped"));
            }
            else
            {
                _owner.MessageLogged?.Invoke(_owner, _owner.Text("入力セッションを停止しました。", "Input session stopped."));
            }
        }
    }
}
