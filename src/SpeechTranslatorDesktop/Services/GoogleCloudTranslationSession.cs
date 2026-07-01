using Google.Cloud.Speech.V2;
using Google.Cloud.Translate.V3;
using Google.Api.Gax.ResourceNames;
using Google.Apis.Auth.OAuth2;
using Google.Protobuf;
using Grpc.Core;
using NAudio.Wave;
using SpeechTranslatorDesktop.Models;
using SpeechTranslatorShared;

namespace SpeechTranslatorDesktop.Services;

public sealed class GoogleCloudTranslationSession : ITranslationSession
{
    private readonly GoogleCloudAudioInput _audioInput;
    private readonly IDesktopTranslationWorker _worker;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _sessionCancellation;
    private readonly Task _streamingTask;
    private readonly object _completionSyncRoot = new();
    private int _disposeRequested;
    private int _stopRequested;

    private GoogleCloudTranslationSession(GoogleCloudAudioInput audioInput, IDesktopTranslationWorker worker, CancellationTokenSource sessionCancellation, Task streamingTask)
    {
        _audioInput = audioInput;
        _worker = worker;
        _sessionCancellation = sessionCancellation;
        _streamingTask = streamingTask;
        IsRunning = true;
    }

    public Task Completion => _completion.Task;

    public bool IsRunning { get; private set; }

    public static async Task<GoogleCloudTranslationSession> StartAsync(
        GoogleCloudServiceSettings settings,
        string sourceLanguage,
        string targetLanguage,
        AudioInputSource audioInputSource,
        RecognitionMode recognitionMode,
        IDesktopTranslationWorker worker,
        AudioSourceKind audioSource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(worker);

        var speechClientBuilder = new SpeechClientBuilder();
        var translationClientBuilder = new TranslationServiceClientBuilder();
        if (!string.Equals(settings.Location, "global", StringComparison.OrdinalIgnoreCase))
        {
            speechClientBuilder.Endpoint = $"{settings.Location}-speech.googleapis.com";
        }

        if (!string.IsNullOrWhiteSpace(settings.CredentialsPath))
        {
            using var credentialStream = File.OpenRead(settings.CredentialsPath);
            var credential = GoogleCredential.FromStream(credentialStream);
            speechClientBuilder.GoogleCredential = credential;
            translationClientBuilder.GoogleCredential = credential;
        }

        var speechClient = await speechClientBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);
        var translationClient = await translationClientBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);
        var sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var audioInput = GoogleCloudAudioInput.Start(audioInputSource);
        try
        {
            var streamingTask = RunStreamingAsync(
                speechClient,
                translationClient,
                settings,
                sourceLanguage,
                targetLanguage,
                recognitionMode,
                worker,
                audioSource,
                audioInput,
                sessionCancellation.Token);
            var session = new GoogleCloudTranslationSession(audioInput, worker, sessionCancellation, streamingTask);
            session.ObserveStreamingTask();
            return session;
        }
        catch
        {
            audioInput.Dispose();
            sessionCancellation.Dispose();
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) == 1)
        {
            await Completion.ConfigureAwait(false);
            return;
        }

        IsRunning = false;
        _audioInput.Dispose();
        _sessionCancellation.Cancel();
        try
        {
            await _streamingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Complete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) == 1)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _sessionCancellation.Dispose();
    }

    private static async Task RunStreamingAsync(
        SpeechClient speechClient,
        TranslationServiceClient translationClient,
        GoogleCloudServiceSettings settings,
        string sourceLanguage,
        string targetLanguage,
        RecognitionMode recognitionMode,
        IDesktopTranslationWorker worker,
        AudioSourceKind audioSource,
        GoogleCloudAudioInput audioInput,
        CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var call = speechClient.StreamingRecognize();
        try
        {
            await call.WriteAsync(new StreamingRecognizeRequest
            {
                Recognizer = $"projects/{settings.ProjectId}/locations/{settings.Location}/recognizers/_",
                StreamingConfig = new StreamingRecognitionConfig
                {
                    Config = new RecognitionConfig
                    {
                        ExplicitDecodingConfig = new ExplicitDecodingConfig
                        {
                            Encoding = ExplicitDecodingConfig.Types.AudioEncoding.Linear16,
                            SampleRateHertz = GoogleCloudAudioInput.OutputSampleRate,
                            AudioChannelCount = GoogleCloudAudioInput.OutputChannels
                        },
                        LanguageCodes = { NormalizeSpeechLanguageCode(sourceLanguage) },
                        Model = string.IsNullOrWhiteSpace(settings.SpeechModel) ? GoogleCloudServiceSettings.DefaultSpeechModel : settings.SpeechModel,
                        Features = new RecognitionFeatures
                        {
                            EnableAutomaticPunctuation = true
                        }
                    },
                    StreamingFeatures = new StreamingRecognitionFeatures
                    {
                        InterimResults = true
                    }
                }
            }).ConfigureAwait(false);

            var requestTask = Task.Run(async () =>
            {
                await foreach (var chunk in audioInput.ReadAllAsync(linkedCancellation.Token).ConfigureAwait(false))
                {
                    await call.WriteAsync(new StreamingRecognizeRequest
                    {
                        Audio = ByteString.CopyFrom(chunk)
                    }).ConfigureAwait(false);
                }

                await call.WriteCompleteAsync().ConfigureAwait(false);
            }, linkedCancellation.Token);

            var responseStream = call.GetResponseStream();
            while (await responseStream.MoveNextAsync(linkedCancellation.Token).ConfigureAwait(false))
            {
                foreach (var result in responseStream.Current.Results)
                {
                    var transcript = result.Alternatives.FirstOrDefault()?.Transcript;
                    if (string.IsNullOrWhiteSpace(transcript))
                    {
                        continue;
                    }

                    worker.ReportRecognizing();
                    if (!result.IsFinal)
                    {
                        continue;
                    }

                    if (recognitionMode == RecognitionMode.TranscriptionOnly)
                    {
                        worker.ReportTranscribedSpeech(transcript, audioSource);
                    }
                    else
                    {
                        var translatedText = await TranslateAsync(translationClient, settings.ProjectId, settings.Location, sourceLanguage, targetLanguage, transcript, linkedCancellation.Token).ConfigureAwait(false);
                        worker.ReportTranslatedSpeech(transcript, translatedText, audioSource);
                    }
                }
            }

            await requestTask.ConfigureAwait(false);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            worker.ReportError($"Google Cloud provider error: {ex.Message}");
        }
        finally
        {
            linkedCancellation.Cancel();
        }
    }

    private static async Task<string> TranslateAsync(TranslationServiceClient translationClient, string projectId, string location, string sourceLanguage, string targetLanguage, string text, CancellationToken cancellationToken)
    {
        var response = await translationClient.TranslateTextAsync(new TranslateTextRequest
        {
            Parent = LocationName.FromProjectLocation(projectId, location).ToString(),
            SourceLanguageCode = NormalizeLanguageCode(sourceLanguage),
            TargetLanguageCode = NormalizeLanguageCode(targetLanguage),
            Contents = { text }
        }, cancellationToken: cancellationToken).ConfigureAwait(false);

        return response.Translations.FirstOrDefault()?.TranslatedText ?? string.Empty;
    }

    private static string NormalizeLanguageCode(string language)
    {
        return language switch
        {
            "ja-JP" => "ja",
            "en-US" => "en",
            "ko-KR" => "ko",
            "pt-BR" => "pt",
            "id-ID" => "id",
            "vi-VN" => "vi",
            "zh-CN" => "zh-CN",
            "zh-TW" => "zh-TW",
            _ when language.Contains('-', StringComparison.Ordinal) => language[..language.IndexOf('-', StringComparison.Ordinal)],
            _ => language
        };
    }

    private static string NormalizeSpeechLanguageCode(string language)
    {
        return language switch
        {
            "zh-CN" => "cmn-Hans-CN",
            "zh-TW" => "cmn-Hant-TW",
            _ => language
        };
    }

    private void Complete()
    {
        lock (_completionSyncRoot)
        {
            if (!IsRunning && Completion.IsCompleted)
            {
                return;
            }

            IsRunning = false;
            _audioInput.Dispose();
            _sessionCancellation.Cancel();
            _completion.TrySetResult();
        }
    }

    private void ObserveStreamingTask()
    {
        _ = ObserveStreamingTaskAsync();
    }

    private async Task ObserveStreamingTaskAsync()
    {
        try
        {
            await _streamingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _worker.ReportError($"Google Cloud provider error: {ex.Message}");
        }
        finally
        {
            Complete();
        }
    }
}
