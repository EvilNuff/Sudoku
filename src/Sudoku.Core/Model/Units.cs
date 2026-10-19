using System.Collections.Immutable;

namespace Sudoku.Core.Model;

public enum UnitKind
{
    Row,
    Column,
    Box,
}

/// <summary>A row, column or box: nine cell indices that must contain each digit once.</summary>
public sealed record Unit(UnitKind Kind, int Number, ImmutableArray<int> Cells)
{
    public override string ToString() => $"{Kind.ToString().ToLowerInvariant()} {Number + 1}";
}

/// <summary>Precomputed grid geometry shared by the solver, generator and conflict detection.</summary>
public static class Units
{
    public static ImmutableArray<Unit> Rows { get; }
    public static ImmutableArray<Unit> Columns { get; }
    public static ImmutableArray<Unit> Boxes { get; }

    /// <summary>All 27 units: rows, then columns, then boxes.</summary>
    public static ImmutableArray<Unit> All { get; }

    /// <summary>For each cell index, the 20 other cells that share a row, column or box with it.</summary>
    public static ImmutableArray<ImmutableArray<int>> Peers { get; }

    static Units()
    {
        Rows = [.. Enumerable.Range(0, 9).Select(r =>
            new Unit(UnitKind.Row, r, [.. Enumerable.Range(0, 9).Select(c => r * 9 + c)]))];
        Columns = [.. Enumerable.Range(0, 9).Select(c =>
            new Unit(UnitKind.Column, c, [.. Enumerable.Range(0, 9).Select(r => r * 9 + c)]))];
        Boxes = [.. Enumerable.Range(0, 9).Select(b =>
            new Unit(UnitKind.Box, b, [.. Enumerable.Range(0, 9).Select(i => (b / 3 * 3 + i / 3) * 9 + b % 3 * 3 + i % 3)]))];
        All = [.. Rows, .. Columns, .. Boxes];

        Peers = [.. Enumerable.Range(0, 81).Select(i =>
        {
            var p = Position.FromIndex(i);
            return Enumerable.Range(0, 81)
                .Where(j => j != i && p.SharesUnitWith(Position.FromIndex(j)))
                .ToImmutableArray();
        })];
    }

    public static Unit RowOf(int index) => Rows[index / 9];
    public static Unit ColumnOf(int index) => Columns[index % 9];
    public static Unit BoxOf(int index) => Boxes[Position.FromIndex(index).Box];
}
