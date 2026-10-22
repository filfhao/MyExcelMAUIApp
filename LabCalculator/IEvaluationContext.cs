namespace LabCalculator;

/// <summary>Доступ до значень інших клітинок під час обчислення виразу.</summary>
public interface IEvaluationContext
{
    /// <summary>
    /// Значення клітинки (індекси з 0).
    /// Кидає ExpressionException, якщо значення отримати не можна (цикл, помилка, текст).
    /// </summary>
    CellValue GetCellValue(int row, int column);
}
