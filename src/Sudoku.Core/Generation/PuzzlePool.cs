using System.Collections.Concurrent;
using Sudoku.Core.Model;

namespace Sudoku.Core.Generation;

/// <summary>
/// Keeps a few ready-made puzzles per difficulty so starting a game is instant, and refills
/// them on background threads.
/// </summary>
public sealed class PuzzlePool : IDisposable
{
    private readonly PuzzleGenerator _generator;
    private readonly int _targetSize;
    private readonly Dictionary<Difficulty, ConcurrentQueue<Puzzle>> _queues;
    private readonly Dictionary<Difficulty, SemaphoreSlim> _refillLocks;
    private readonly CancellationTokenSource _shutdown = new();
    private volatile bool _disposed;

    public PuzzlePool(PuzzleGenerator generator, int targetSize = 2)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentOutOfRangeException.ThrowIfLessThan(targetSize, 1);
        _generator = generator;
        _targetSize = targetSize;
        _queues = Enum.GetValues<Difficulty>().ToDictionary(d => d, _ => new ConcurrentQueue<Puzzle>());
        _refillLocks = Enum.GetValues<Difficulty>().ToDictionary(d => d, _ => new SemaphoreSlim(1, 1));
    }

    public int CountOf(Difficulty difficulty) => _queues[difficulty].Count;

    /// <summary>Returns a pooled puzzle if available, otherwise generates one on a background thread.</summary>
    public async Task<Puzzle> TakeAsync(Difficulty difficulty, CancellationToken cancellationToken = default)
    {
        if (!_queues[difficulty].TryDequeue(out var puzzle))
            puzzle = await Task.Run(() => _generator.Generate(difficulty, cancellationToken: cancellationToken), cancellationToken).ConfigureAwait(false);

        StartRefill(difficulty);
        return puzzle;
    }

    /// <summary>Tops up every difficulty in the background; safe to call repeatedly.</summary>
    public void WarmUp()
    {
        foreach (var difficulty in Enum.GetValues<Difficulty>())
            StartRefill(difficulty);
    }

    private void StartRefill(Difficulty difficulty)
    {
        if (!_disposed)
            _ = RefillAsync(difficulty);
    }

    private async Task RefillAsync(Difficulty difficulty)
    {
        var gate = _refillLocks[difficulty];
        if (!await gate.WaitAsync(0).ConfigureAwait(false))
            return; // a refill for this difficulty is already running
        try
        {
            var token = _shutdown.Token;
            // _shutdown is cancelled but never disposed, so the token stays valid for in-flight refills.
            while (_queues[difficulty].Count < _targetSize && !token.IsCancellationRequested)
            {
                var puzzle = await Task.Run(() => _generator.Generate(difficulty, cancellationToken: token), token).ConfigureAwait(false);
                _queues[difficulty].Enqueue(puzzle);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        catch (PuzzleGenerationException)
        {
            // TakeAsync generates on demand, so a failed background refill only costs latency.
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _shutdown.Cancel();
    }
}
