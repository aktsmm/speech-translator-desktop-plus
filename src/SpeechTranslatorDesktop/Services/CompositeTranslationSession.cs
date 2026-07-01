using SpeechTranslatorShared;

namespace SpeechTranslatorDesktop.Services;

public sealed class CompositeTranslationSession : ITranslationSession
{
    private readonly IReadOnlyList<ITranslationSession> _sessions;
    private readonly Action? _onCompleted;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposeRequested;
    private int _remainingSessions;
    private int _stopRequested;

    public CompositeTranslationSession(IEnumerable<ITranslationSession> sessions, Action? onCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        _sessions = sessions.ToArray();
        if (_sessions.Count == 0)
        {
            throw new ArgumentException("At least one session is required.", nameof(sessions));
        }

        _onCompleted = onCompleted;
        _remainingSessions = _sessions.Count;

        foreach (var session in _sessions)
        {
            _ = ObserveChildCompletionAsync(session);
        }
    }

    public Task Completion => _completion.Task;

    public bool IsRunning => _sessions.Any(session => session.IsRunning);

    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) == 0)
        {
            await StopAllAsync().ConfigureAwait(false);
        }

        await Completion.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) == 1)
        {
            return;
        }

        if (Interlocked.Exchange(ref _stopRequested, 1) == 0)
        {
            try
            {
                await StopAllAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        }

        await DisposeAllAsync().ConfigureAwait(false);
    }

    private async Task ObserveChildCompletionAsync(ITranslationSession completedSession)
    {
        try
        {
            await completedSession.Completion.ConfigureAwait(false);
        }
        finally
        {
            if (Interlocked.Exchange(ref _stopRequested, 1) == 0)
            {
                try
                {
                    await StopAllExceptAsync(completedSession).ConfigureAwait(false);
                }
                catch
                {
                    // Completion must not hang if sibling shutdown fails; explicit StopAsync still surfaces stop errors.
                }
            }

            if (Interlocked.Decrement(ref _remainingSessions) == 0)
            {
                _onCompleted?.Invoke();
                _completion.TrySetResult();
            }
        }
    }

    private Task StopAllAsync()
    {
        return StopSessionsAsync(_sessions);
    }

    private Task StopAllExceptAsync(ITranslationSession completedSession)
    {
        return StopSessionsAsync(_sessions.Where(session => !ReferenceEquals(session, completedSession)));
    }

    private static async Task StopSessionsAsync(IEnumerable<ITranslationSession> sessions)
    {
        var exceptions = new List<Exception>();
        foreach (var session in sessions)
        {
            try
            {
                if (session.IsRunning)
                {
                    await session.StopAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }

    private async Task DisposeAllAsync()
    {
        var exceptions = new List<Exception>();
        foreach (var session in _sessions)
        {
            try
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
}
