using System.Text;
using System.Diagnostics;
using System.Globalization;

namespace SpeechTranslatorDesktop.Services;

public sealed class RecordingFileService : IRecordingFileService
{
    private static readonly HashSet<string> ReservedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private readonly string _rootDirectory;
    private readonly object _appendSyncRoot = new();
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, long> _lastTimestampByFile = new(StringComparer.OrdinalIgnoreCase);
    private string _recordingsDirectory;

    public RecordingFileService(string rootDirectory, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException($"'{nameof(rootDirectory)}' を NULL または空にすることはできません。", nameof(rootDirectory));
        }

        _rootDirectory = rootDirectory;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _recordingsDirectory = Path.Combine(_rootDirectory, "recordings");
    }

    public string RecordingsDirectory => _recordingsDirectory;

    public void AppendTranslation(string? fileName, string sourceText, string translatedText, string? speakerLabel = null)
    {
        var safeFileName = NormalizeFileName(fileName);
        if (safeFileName is null)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sourceText);
        ArgumentException.ThrowIfNullOrWhiteSpace(translatedText);

        AppendEntry(safeFileName, sourceText, translatedText, speakerLabel);
    }

    public void AppendTranscription(string? fileName, string sourceText, string? speakerLabel = null)
    {
        var safeFileName = NormalizeFileName(fileName);
        if (safeFileName is null)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sourceText);

        AppendEntry(safeFileName, sourceText, null, speakerLabel);
    }

    private void AppendEntry(string fileName, string sourceText, string? translatedText, string? speakerLabel)
    {
        lock (_appendSyncRoot)
        {
            var recordingsDirectory = RecordingsDirectory;
            var filePath = GetRecordingFilePath(recordingsDirectory, fileName);
            Directory.CreateDirectory(recordingsDirectory);
            var now = _timeProvider.GetTimestamp();
            var writeTimestamp = !_lastTimestampByFile.TryGetValue(filePath, out var lastTimestamp)
                || _timeProvider.GetElapsedTime(lastTimestamp, now) >= TimeSpan.FromMinutes(5);

            using (var streamWriter = new StreamWriter(filePath, append: true, Encoding.UTF8))
            {
                writeTimestamp |= streamWriter.BaseStream.Length == 0;
                if (writeTimestamp)
                {
                    streamWriter.WriteLine($"[{_timeProvider.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)}]");
                }

                streamWriter.WriteLine(FormatLine(sourceText, speakerLabel));
                if (translatedText is not null)
                {
                    streamWriter.WriteLine(translatedText);
                }

                streamWriter.WriteLine();
            }

            if (writeTimestamp)
            {
                _lastTimestampByFile[filePath] = now;
            }
        }
    }

    public string OpenRecordingsFolder(string? directoryPath = null)
    {
        var recordingsDirectory = string.IsNullOrWhiteSpace(directoryPath)
            ? RecordingsDirectory
            : Path.GetFullPath(directoryPath.Trim());
        Directory.CreateDirectory(recordingsDirectory);

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = recordingsDirectory,
            UseShellExecute = true
        });

        return recordingsDirectory;
    }

    public void SetRecordingsDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("保存フォルダーを指定してください。", nameof(directoryPath));
        }

        _recordingsDirectory = Path.GetFullPath(directoryPath.Trim());
        Directory.CreateDirectory(_recordingsDirectory);
    }

    public string? NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return ValidateFileName(fileName);
    }

    private static string GetRecordingFilePath(string recordingsDirectory, string fileName)
    {
        var recordingsRootPath = Path.GetFullPath(recordingsDirectory);
        var filePath = Path.GetFullPath(Path.Combine(recordingsRootPath, $"{fileName}.txt"));
        var recordingsRootWithSeparator = recordingsRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!filePath.StartsWith(recordingsRootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("recordings 配下に保存できる単純なファイル名を指定してください。", nameof(fileName));
        }

        return filePath;
    }

    private static string FormatLine(string text, string? speakerLabel)
    {
        return string.IsNullOrWhiteSpace(speakerLabel)
            ? text
            : $"{speakerLabel.Trim()}: {text}";
    }

    private static string ValidateFileName(string fileName)
    {
        var trimmedFileName = fileName.Trim();
        var normalizedFileName = trimmedFileName.Normalize(NormalizationForm.FormC);

        if (normalizedFileName.Length == 0)
        {
            throw new ArgumentException("ファイル名を指定してください。", nameof(fileName));
        }

        if (Path.IsPathRooted(normalizedFileName))
        {
            throw new ArgumentException("絶対パスは指定できません。", nameof(fileName));
        }

        if (normalizedFileName.Contains(Path.DirectorySeparatorChar) || normalizedFileName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("ディレクトリ区切り文字は指定できません。", nameof(fileName));
        }

        if (normalizedFileName is "." or ".." || normalizedFileName.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("親ディレクトリ参照は指定できません。", nameof(fileName));
        }

        if (normalizedFileName.Contains('.', StringComparison.Ordinal))
        {
            throw new ArgumentException("拡張子を含まない単純なファイル名を指定してください。", nameof(fileName));
        }

        if (normalizedFileName.EndsWith('.'))
        {
            throw new ArgumentException("末尾にドットは指定できません。", nameof(fileName));
        }

        if (normalizedFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || normalizedFileName.Any(char.IsControl))
        {
            throw new ArgumentException("無効なファイル名です。", nameof(fileName));
        }

        if (!normalizedFileName.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' '))
        {
            throw new ArgumentException("ファイル名には文字、数字、スペース、ハイフン、アンダースコアのみ使用できます。", nameof(fileName));
        }

        if (ReservedFileNames.Contains(normalizedFileName))
        {
            throw new ArgumentException("予約済みのファイル名は指定できません。", nameof(fileName));
        }

        return normalizedFileName;
    }
}
