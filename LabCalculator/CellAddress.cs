using System.Globalization;

namespace LabCalculator;

/// <summary>
/// Допоміжні методи для імен клітинок у стилі Excel: A1, B12, AA3 тощо.
/// </summary>
public static class CellAddress
{
    private const int MaxLetters = 3;

    /// <summary>Назва стовпчика за номером (з 1): 1 → A, 26 → Z, 27 → AA.</summary>
    public static string GetColumnName(int columnNumber)
    {
        string name = "";
        while (columnNumber > 0)
        {
            int remainder = (columnNumber - 1) % 26;
            name = (char)('A' + remainder) + name;
            columnNumber = (columnNumber - 1) / 26;
        }
        return name;
    }

    /// <summary>Ім'я клітинки за індексами рядка та стовпчика (обидва з 0).</summary>
    public static string GetName(int row, int column)
        => GetColumnName(column + 1) + (row + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Розбирає ім'я клітинки (регістр літер не важливий). Індекси повертаються з 0.
    /// Перевіряє лише форму запису, а не належність клітинки до таблиці.
    /// </summary>
    public static bool TryParse(string name, out int row, out int column)
    {
        row = -1;
        column = -1;

        int i = 0;
        long columnNumber = 0;
        while (i < name.Length && char.IsAsciiLetter(name[i]))
        {
            columnNumber = columnNumber * 26 + (char.ToUpperInvariant(name[i]) - 'A' + 1);
            i++;
            if (i > MaxLetters)
                return false;
        }

        if (i == 0 || i == name.Length)
            return false;

        for (int k = i; k < name.Length; k++)
        {
            if (!char.IsAsciiDigit(name[k]))
                return false;
        }

        // Дуже великий номер рядка (переповнення int) вважаємо коректним за формою, але поза межами таблиці.
        if (!int.TryParse(name.AsSpan(i), NumberStyles.None, CultureInfo.InvariantCulture, out int rowNumber))
            rowNumber = int.MaxValue;

        if (rowNumber == 0)
            return false;

        row = rowNumber - 1;
        column = (int)columnNumber - 1;
        return true;
    }
}
