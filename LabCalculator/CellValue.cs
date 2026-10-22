using System.Globalization;
using System.Numerics;

namespace LabCalculator;

/// <summary>
/// Значення виразу: або ціле число довільної довжини (BigInteger), або логічне значення.
/// </summary>
public readonly struct CellValue
{
    private readonly BigInteger number;
    private readonly bool logical;

    private CellValue(BigInteger number, bool logical, bool isLogical)
    {
        this.number = number;
        this.logical = logical;
        IsLogical = isLogical;
    }

    /// <summary>true, якщо значення логічне; false — якщо число.</summary>
    public bool IsLogical { get; }

    /// <summary>Числове значення (має сенс, лише коли IsLogical == false).</summary>
    public BigInteger Number => number;

    /// <summary>Логічне значення (має сенс, лише коли IsLogical == true).</summary>
    public bool Logical => logical;

    public static CellValue FromNumber(BigInteger value) => new(value, false, false);

    public static CellValue FromLogical(bool value) => new(BigInteger.Zero, value, true);

    public override string ToString()
    {
        if (IsLogical)
            return logical ? "ІСТИНА" : "НЕПРАВДА";
        return number.ToString(CultureInfo.InvariantCulture);
    }
}
