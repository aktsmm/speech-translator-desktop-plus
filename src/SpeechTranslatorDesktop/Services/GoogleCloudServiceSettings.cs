namespace SpeechTranslatorDesktop.Services;

public sealed record GoogleCloudServiceSettings(
    string ProjectId,
    string Location,
    string SpeechModel,
    string? CredentialsPath)
{
    public const string DefaultLocation = "us";
    public const string DefaultSpeechModel = "chirp_3";

    public static GoogleCloudServiceSettings Empty { get; } = new(string.Empty, DefaultLocation, DefaultSpeechModel, null);
}
