using System.Threading.Channels;
using NAudio.Wave;
using SpeechTranslatorDesktop.Models;

namespace SpeechTranslatorDesktop.Services;

public sealed class GoogleCloudAudioInput : IDisposable
{
    public const int OutputSampleRate = 16000;
    public const int OutputBitsPerSample = 16;
    public const int OutputChannels = 1;

    private readonly Channel<byte[]> _audioChunks = Channel.CreateUnbounded<byte[]>();
    private readonly List<CaptureSource> _captureSources = [];
    private bool _disposed;

    private GoogleCloudAudioInput(IEnumerable<IWaveIn> captures)
    {
        foreach (var capture in captures)
        {
            var source = new CaptureSource(capture, WriteChunk);
            _captureSources.Add(source);
            source.Start();
        }
    }

    public static GoogleCloudAudioInput Start(AudioInputSource source)
    {
        return source switch
        {
            AudioInputSource.Microphone => StartMicrophone(),
            AudioInputSource.SystemAudio => StartSystemAudio(),
            _ => throw new ArgumentException("GoogleCloudAudioInput accepts a single source only.", nameof(source))
        };
    }

    public static GoogleCloudAudioInput StartMicrophone()
    {
        return new GoogleCloudAudioInput([
            new WaveInEvent
            {
                WaveFormat = new WaveFormat(OutputSampleRate, OutputBitsPerSample, OutputChannels),
                BufferMilliseconds = 100
            }
        ]);
    }

    public static GoogleCloudAudioInput StartSystemAudio()
    {
        return new GoogleCloudAudioInput([new WasapiLoopbackCapture()]);
    }

    public IAsyncEnumerable<byte[]> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _audioChunks.Reader.ReadAllAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var source in _captureSources)
        {
            source.Dispose();
        }

        _audioChunks.Writer.TryComplete();
    }

    private void WriteChunk(byte[] outputBytes)
    {
        if (_disposed || outputBytes.Length == 0)
        {
            return;
        }

        foreach (var chunk in SplitChunks(outputBytes, maxBytes: 15 * 1024))
        {
            _audioChunks.Writer.TryWrite(chunk);
        }
    }

    private static IEnumerable<byte[]> SplitChunks(byte[] bytes, int maxBytes)
    {
        for (var offset = 0; offset < bytes.Length; offset += maxBytes)
        {
            var length = Math.Min(maxBytes, bytes.Length - offset);
            var chunk = new byte[length];
            Buffer.BlockCopy(bytes, offset, chunk, 0, length);
            yield return chunk;
        }
    }

    private sealed class CaptureSource : IDisposable
    {
        private readonly IWaveIn _capture;
        private readonly Action<byte[]> _write;
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

        public CaptureSource(IWaveIn capture, Action<byte[]> write)
        {
            _capture = capture;
            _write = write;
            _sourceSampleRate = capture.WaveFormat.SampleRate;
            _sourceChannels = capture.WaveFormat.Channels;
            _sourceIsFloat32 = capture.WaveFormat.BitsPerSample == 32;
            _sourceSamplesPerOutputSample = (double)_sourceSampleRate / OutputSampleRate;
            _capture.DataAvailable += OnDataAvailable;
        }

        public void Start()
        {
            _capture.StartRecording();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _capture.DataAvailable -= OnDataAvailable;

            try
            {
                _capture.StopRecording();
            }
            catch (InvalidOperationException)
            {
            }

            _capture.Dispose();
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

            _write(outputBytes);
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
            var consumedSamples = (int)Math.Max(0, Math.Floor(_nextOutputSourceIndex - _bufferStartIndex) - 1);
            if (consumedSamples <= 0)
            {
                return;
            }

            var count = Math.Min(consumedSamples, _sampleBuffer.Count);
            _sampleBuffer.RemoveRange(0, count);
            _bufferStartIndex += count;
        }
    }
}
