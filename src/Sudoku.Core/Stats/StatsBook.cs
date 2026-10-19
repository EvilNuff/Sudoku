using Sudoku.Core.Model;

namespace Sudoku.Core.Stats;

public sealed record DifficultyStats
{
    public int GamesStarted { get; init; }
    public int GamesCompleted { get; init; }
    public TimeSpan? BestTime { get; init; }
    public TimeSpan? BestTimeNoHints { get; init; }
    public TimeSpan TotalTime { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public TimeSpan? AverageTime => GamesCompleted == 0 ? null : TotalTime / GamesCompleted;
}

/// <param name="IsNewBest">Fastest completion at this difficulty so far.</param>
/// <param name="IsNewNoHintBest">Fastest completion without hints so far (only for hint-free games).</param>
public sealed record CompletionResult(bool IsNewBest, bool IsNewNoHintBest, DifficultyStats Stats);

/// <summary>Personal bests and totals per difficulty.</summary>
public sealed class StatsBook
{
    private readonly Dictionary<Difficulty, DifficultyStats> _stats;

    public StatsBook(IReadOnlyDictionary<Difficulty, DifficultyStats>? stats = null)
    {
        _stats = Enum.GetValues<Difficulty>().ToDictionary(
            d => d,
            d => stats is not null && stats.TryGetValue(d, out var s) ? s : new DifficultyStats());
    }

    public DifficultyStats this[Difficulty difficulty] => _stats[difficulty];

    public IReadOnlyDictionary<Difficulty, DifficultyStats> All => _stats;

    public void RecordStart(Difficulty difficulty) =>
        _stats[difficulty] = _stats[difficulty] with { GamesStarted = _stats[difficulty].GamesStarted + 1 };

    public CompletionResult RecordCompletion(Difficulty difficulty, TimeSpan time, int hintsUsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(time, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegative(hintsUsed);

        var current = _stats[difficulty];
        var isNewBest = current.BestTime is not { } best || time < best;
        var isNewNoHintBest = hintsUsed == 0 && (current.BestTimeNoHints is not { } noHintBest || time < noHintBest);

        var updated = current with
        {
            GamesCompleted = current.GamesCompleted + 1,
            TotalTime = current.TotalTime + time,
            BestTime = isNewBest ? time : current.BestTime,
            BestTimeNoHints = isNewNoHintBest ? time : current.BestTimeNoHints,
        };
        _stats[difficulty] = updated;
        return new CompletionResult(isNewBest, isNewNoHintBest, updated);
    }

    public void Reset()
    {
        foreach (var difficulty in Enum.GetValues<Difficulty>())
            _stats[difficulty] = new DifficultyStats();
    }
}
