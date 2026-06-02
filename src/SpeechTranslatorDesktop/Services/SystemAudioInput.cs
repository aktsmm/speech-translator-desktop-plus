using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;

namespace SpeechTranslatorDesktop.Services;

public sealed class SystemAudioInput : IDisposable
{
    private const int OutputSampleRate = 16000;
    private const int OutputBitsPerSample = 16;
    private const int OutputChannels = 1;

    private readonly WasapiLoopbackCapture _capture;
    private readonly PushAudioInputStream _stream;
    private readonly List<float> _sampleBuffer = [];
    private readonly object _syncRoot = new();
    private readonly int _sourceSampleRate;
    private readonly int _sourceChannels;
    private readonly bool _sourceIsFloat32;
    private readonly double _sourceSamplesPerOutputSample;
    private long _bufferStartIndex;
    private double _nextOutputSourceIndex;
    private bool _hasOutputPosition;
    private bool _disposed;

    private SystemAudioInput(WasapiLoopbackCapture capture, PushAudioInputStream stream)
    {
        _capture = capture;
        _stream = stream;
        _sourceSampleRate = capture.WaveFormat.SampleRate;
        _sourceChannels = capture.WaveFormat.Channels;
        _sourceIsFloat32 = capture.WaveFormat.BitsPerSample == 32;
        _sourceSamplesPerOutputSample = (double)_sourceSampleRate / OutputSampleRate;
        AudioConfig = AudioConfig.FromStreamInput(stream);

        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
        _capture.StartRecording();
    }

    public AudioConfig AudioConfig { get; }

    public static SystemAudioInput Start()
    {
        var format = AudioStreamFormat.GetWaveFormatPCM(OutputSampleRate, OutputBitsPerSample, OutputChannels);
        var stream = AudioInputStream.CreatePushStream(format);
        try
        {
            return new SystemAudioInput(new WasapiLoopbackCapture(), stream);
        }
        catch
        {
            stream.Close();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _capture.DataAvailable -= OnDataAvailable;
        _capture.RecordingStopped -= OnRecordingStopped;

        try
        {
            _capture.StopRecording();
        }
        catch (InvalidOperationException)
        {
        }

        _capture.Dispose();
        _stream.Close();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_disposed || e.BytesRecorded == 0)
        {
            return;
        }

        var monoSamples = ConvertToMonoSamples(e.Buffer, e.BytesRecorded);
        if (monoSamples.Count == 0)
        {
            return;
        }

        byte[] outputBytes;
        lock (_syncRoot)
        {
            if (!_hasOutputPosition)
            {
                _nextOutputSourceIndex = _bufferStartIndex;
                _hasOutputPosition = true;
            }

            _sampleBuffer.AddRange(monoSamples);
            outputBytes = ResampleAvailableSamples();
        }

        if (outputBytes.Length > 0)
        {
            _stream.Write(outputBytes);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _stream.Close();
    }

    private List<float> ConvertToMonoSamples(byte[] buffer, int bytesRecorded)
    {
        var bytesPerSample = _sourceIsFloat32 ? sizeof(float) : _capture.WaveFormat.BitsPerSample / 8;
        if (bytesPerSample <= 0 || _sourceChannels <= 0)
        {
            return [];
        }

        var frameSize = bytesPerSample * _sourceChannels;
        var frameCount = bytesRecorded / frameSize;
        var samples = new List<float>(frameCount);

        for (var frame = 0; frame < frameCount; frame++)
        {
            var frameOffset = frame * frameSize;
            var monoSample = 0f;
            for (var channel = 0; channel < _sourceChannels; channel++)
            {
                var sampleOffset = frameOffset + channel * bytesPerSample;
                monoSample += ReadSample(buffer, sampleOffset, bytesPerSample);
            }

            samples.Add(monoSample / _sourceChannels);
        }

        return samples;
    }

    private float ReadSample(byte[] buffer, int offset, int bytesPerSample)
    {
        if (_sourceIsFloat32)
        {
            return Math.Clamp(BitConverter.ToSingle(buffer, offset), -1f, 1f);
        }

        return bytesPerSample switch
        {
            2 => BitConverter.ToInt16(buffer, offset) / 32768f,
            3 => Read24BitPcmSample(buffer, offset),
            4 => BitConverter.ToInt32(buffer, offset) / 2147483648f,
            _ => 0f
        };
    }

    private static float Read24BitPcmSample(byte[] buffer, int offset)
    {
        var value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
        if ((value & 0x800000) != 0)
        {
            value |= unchecked((int)0xFF000000);
        }

        return value / 8388608f;
    }

    private byte[] ResampleAvailableSamples()
    {
        if (_sampleBuffer.Count < 2)
        {
            return [];
        }

        using var output = new MemoryStream();
        while (_nextOutputSourceIndex + 1 < _bufferStartIndex + _sampleBuffer.Count)
        {
            var localIndex = _nextOutputSourceIndex - _bufferStartIndex;
            var sampleIndex = (int)Math.Floor(localIndex);
            var fraction = localIndex - sampleIndex;
            var first = _sampleBuffer[sampleIndex];
            var second = _sampleBuffer[sampleIndex + 1];
            var sample = first + (second - first) * fraction;
            var pcm = (short)Math.Clamp((int)Math.Round(sample * short.MaxValue), short.MinValue, short.MaxValue);

            output.WriteByte((byte)(pcm & 0xFF));
            output.WriteByte((byte)((pcm >> 8) & 0xFF));
            _nextOutputSourceIndex += _sourceSamplesPerOutputSample;
        }

        TrimConsumedSamples();
        return output.ToArray();
    }

    private void TrimConsumedSamples()
    {
        var keepFromIndex = Math.Max(_bufferStartIndex, (long)Math.Floor(_nextOutputSourceIndex) - 1);
        var removeCount = (int)(keepFromIndex - _bufferStartIndex);
        if (removeCount <= 0)
        {
            return;
        }

        _sampleBuffer.RemoveRange(0, removeCount);
        _bufferStartIndex += removeCount;
    }
}
