namespace LabCalculator;

/// <summary>
/// Помилка у виразі (синтаксична або помилка обчислення).
/// Position — позиція (від 0) у тексті клітинки, де знайдено помилку; -1, якщо позиція невідома.
/// </summary>
public sealed class ExpressionException : Exception
{
    public ExpressionException(string message, int position, int length = 1)
        : base(message)
    {
        Position = position;
        Length = length;
    }

    public int Position { get; }

    public int Length { get; }
}
