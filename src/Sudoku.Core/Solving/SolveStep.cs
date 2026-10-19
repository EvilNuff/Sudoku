using Sudoku.Core.Model;

namespace Sudoku.Core.Solving;

public readonly record struct Placement(Position Position, int Digit);

public readonly record struct Elimination(Position Position, int Digit);

/// <summary>
/// One deduction found by a technique: what it places or eliminates, which cells form the pattern
/// (for hint highlighting), and a plain-language explanation.
/// </summary>
public sealed record SolveStep(
    string Technique,
    Difficulty Difficulty,
    IReadOnlyList<Placement> Placements,
    IReadOnlyList<Elimination> Eliminations,
    IReadOnlyList<Position> PatternCells,
    string Explanation)
{
    public bool MakesProgress => Placements.Count > 0 || Eliminations.Count > 0;
}

/// <summary>A human solving technique. Implementations return null when the pattern is absent.</summary>
public interface ITechnique
{
    string Name { get; }
    Difficulty Difficulty { get; }
    SolveStep? Find(CandidateGrid grid);
}
