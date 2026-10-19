namespace Sudoku.Core.Game;

/// <summary>A pausable stopwatch driven by <see cref="TimeProvider"/> so it can be tested deterministically.</summary>
public sealed class GameTimer
{
    private readonly TimeProvider _time;
    private TimeSpan _accumulated;
    private DateTimeOffset? _startedAt;

    public GameTimer(TimeProvider time, TimeSpan alreadyElapsed = default)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentOutOfRangeException.ThrowIfLessThan(alreadyElapsed, TimeSpan.Zero);
        _time = time;
        _accumulated = alreadyElapsed;
    }

    public bool IsRunning => _startedAt is not null;

    public TimeSpan Elapsed => _startedAt is { } start ? _accumulated + (_time.GetUtcNow() - start) : _accumulated;

    public void Start() => _startedAt ??= _time.GetUtcNow();

    public void Pause()
    {
        if (_startedAt is not { } start)
            return;
        _accumulated += _time.GetUtcNow() - start;
        _startedAt = null;
    }
}
