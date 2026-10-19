using System.Collections;
using System.Numerics;

namespace Sudoku.Core.Model;

/// <summary>An immutable set of digits 1-9 stored as a 9-bit mask (bit d-1 represents digit d).</summary>
public readonly record struct DigitSet : IEnumerable<int>
{
    public const ushort AllBits = 0x1FF;

    public DigitSet(ushort bits)
    {
        if ((bits & ~AllBits) != 0)
            throw new ArgumentOutOfRangeException(nameof(bits), bits, "Only the low 9 bits may be set.");
        Bits = bits;
    }

    public ushort Bits { get; }

    public static DigitSet Empty => default;
    public static DigitSet All => new(AllBits);

    public int Count => BitOperations.PopCount(Bits);
    public bool IsEmpty => Bits == 0;

    public static DigitSet Of(params ReadOnlySpan<int> digits)
    {
        var set = Empty;
        foreach (var d in digits)
            set = set.With(d);
        return set;
    }

    public bool Contains(int digit) => (Bits & BitFor(digit)) != 0;
    public DigitSet With(int digit) => new((ushort)(Bits | BitFor(digit)));
    public DigitSet Without(int digit) => new((ushort)(Bits & ~BitFor(digit)));
    public DigitSet Toggle(int digit) => new((ushort)(Bits ^ BitFor(digit)));

    public DigitSet Union(DigitSet other) => new((ushort)(Bits | other.Bits));
    public DigitSet Intersect(DigitSet other) => new((ushort)(Bits & other.Bits));
    public DigitSet Except(DigitSet other) => new((ushort)(Bits & ~other.Bits));

    /// <summary>The smallest digit in the set; throws when empty.</summary>
    public int First() => IsEmpty
        ? throw new InvalidOperationException("The set is empty.")
        : BitOperations.TrailingZeroCount(Bits) + 1;

    public IEnumerator<int> GetEnumerator()
    {
        for (var d = 1; d <= 9; d++)
        {
            if (Contains(d))
                yield return d;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => string.Concat(this);

    internal static ushort BitFor(int digit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(digit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digit, 9);
        return (ushort)(1 << (digit - 1));
    }
}
