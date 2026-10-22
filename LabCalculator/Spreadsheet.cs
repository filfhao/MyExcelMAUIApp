using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace LabCalculator;

/// <summary>Відомості про помилку в клітинці.</summary>
public sealed record CellErrorInfo(string CellName, string Stage, ExpressionException Exception)
{
    public string Describe()
    {
        return Exception.Position >= 0
            ? $"{CellName}: {Stage}, позиція {Exception.Position + 1} — {Exception.Message}"
            : $"{CellName}: {Stage} — {Exception.Message}";
    }
}

/// <summary>
/// Модель електронної таблиці (не залежить від інтерфейсу).
/// Клітинка містить: порожнє значення, ціле число, текст або вираз (текст, що починається з «=»).
/// Обчислення виконується у два етапи: 1) синтаксична перевірка всіх виразів із локалізацією помилок,
/// 2) обчислення значень (із підтримкою посилань на інші клітинки та виявленням циклів).
/// </summary>
public sealed class Spreadsheet
{
    public const int MaxRows = 100;
    public const int MaxColumns = 26;

    private const string SyntaxStage = "синтаксична помилка";
    private const string EvaluationStage = "помилка обчислення";
    private const string ErrorText = "#ПОМИЛКА";

    private enum CellKind { Empty, Number, Text, Formula }

    private sealed class SheetCell
    {
        public string Raw = "";
        public CellKind Kind;
        public BigInteger Constant;
        public ParsedFormula? Parsed;
        public CellValue? Value;
        public ExpressionException? Error;
        public bool IsSyntaxError;
        public bool IsEvaluated;
        public bool IsEvaluating;
    }

    private sealed class Context : IEvaluationContext
    {
        private readonly Spreadsheet owner;

        public Context(Spreadsheet owner) => this.owner = owner;

        public CellValue GetCellValue(int row, int column) => owner.GetCellValue(row, column);
    }

    private readonly List<List<SheetCell>> cells = new();
    private readonly Context context;
    private bool isDirty = true;

    public Spreadsheet(int rows, int columns)
    {
        if (rows < 1 || rows > MaxRows)
            throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns < 1 || columns > MaxColumns)
            throw new ArgumentOutOfRangeException(nameof(columns));

        context = new Context(this);
        for (int r = 0; r < rows; r++)
            cells.Add(CreateRow(columns));
    }

    public int RowCount => cells.Count;

    public int ColumnCount => cells[0].Count;

    // ------------------------------------------------------------ доступ до даних

    public string GetRaw(int row, int column) => cells[row][column].Raw;

    public void SetRaw(int row, int column, string? text)
    {
        string value = text ?? "";
        SheetCell cell = cells[row][column];
        if (cell.Raw == value)
            return;

        cell.Raw = value;
        isDirty = true;
    }

    /// <summary>Текст для режиму ЗНАЧЕННЯ.</summary>
    public string GetDisplayText(int row, int column)
    {
        EnsureCalculated();
        SheetCell cell = cells[row][column];
        switch (cell.Kind)
        {
            case CellKind.Empty:
                return "";
            case CellKind.Number:
                return cell.Constant.ToString(CultureInfo.InvariantCulture);
            case CellKind.Text:
                return cell.Raw;
            default:
                return cell.Error != null ? ErrorText : cell.Value!.Value.ToString();
        }
    }

    /// <summary>Помилка в клітинці або null, якщо помилки немає.</summary>
    public CellErrorInfo? GetError(int row, int column)
    {
        EnsureCalculated();
        SheetCell cell = cells[row][column];
        if (cell.Kind != CellKind.Formula || cell.Error == null)
            return null;

        return new CellErrorInfo(
            CellAddress.GetName(row, column),
            cell.IsSyntaxError ? SyntaxStage : EvaluationStage,
            cell.Error);
    }

    /// <summary>Усі помилки таблиці (у порядку рядків).</summary>
    public IReadOnlyList<CellErrorInfo> GetErrors()
    {
        var errors = new List<CellErrorInfo>();
        for (int r = 0; r < RowCount; r++)
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                CellErrorInfo? error = GetError(r, c);
                if (error != null)
                    errors.Add(error);
            }
        }
        return errors;
    }

    public int FormulaCount
    {
        get
        {
            EnsureCalculated();
            return cells.Sum(row => row.Count(cell => cell.Kind == CellKind.Formula));
        }
    }

    /// <summary>Текстовий звіт про результат обчислення (для діалогу «Порахувати»).</summary>
    public string BuildReport(int maxErrorsToShow = 12)
    {
        Recalculate();
        IReadOnlyList<CellErrorInfo> errors = GetErrors();

        var sb = new StringBuilder();
        sb.AppendLine($"Виразів у таблиці: {FormulaCount}.");

        if (errors.Count == 0)
        {
            sb.Append("Помилок не знайдено.");
            return sb.ToString();
        }

        sb.AppendLine($"Помилок: {errors.Count}.");
        sb.AppendLine();
        foreach (CellErrorInfo error in errors.Take(maxErrorsToShow))
            sb.AppendLine("• " + error.Describe());

        if (errors.Count > maxErrorsToShow)
            sb.Append($"… та ще {errors.Count - maxErrorsToShow}.");

        return sb.ToString().TrimEnd();
    }

    // ------------------------------------------------------------ зміна розмірів

    public bool AddRow()
    {
        if (RowCount >= MaxRows)
            return false;

        cells.Add(CreateRow(ColumnCount));
        isDirty = true;
        return true;
    }

    public bool RemoveLastRow()
    {
        if (RowCount <= 1)
            return false;

        cells.RemoveAt(RowCount - 1);
        isDirty = true;
        return true;
    }

    public bool AddColumn()
    {
        if (ColumnCount >= MaxColumns)
            return false;

        foreach (List<SheetCell> row in cells)
            row.Add(new SheetCell());
        isDirty = true;
        return true;
    }

    public bool RemoveLastColumn()
    {
        if (ColumnCount <= 1)
            return false;

        foreach (List<SheetCell> row in cells)
            row.RemoveAt(row.Count - 1);
        isDirty = true;
        return true;
    }

    public bool IsLastRowEmpty()
        => cells[RowCount - 1].All(cell => string.IsNullOrWhiteSpace(cell.Raw));

    public bool IsLastColumnEmpty()
        => cells.All(row => string.IsNullOrWhiteSpace(row[ColumnCount - 1].Raw));

    // ------------------------------------------------------------ збереження / завантаження

    /// <summary>Вміст усіх клітинок «як введено» (для збереження у файл).</summary>
    public List<List<string>> ToRaw()
        => cells.Select(row => row.Select(cell => cell.Raw).ToList()).ToList();

    /// <summary>Створює таблицю з уже прочитаних даних; кидає InvalidDataException за некоректних розмірів.</summary>
    public static Spreadsheet FromRaw(IReadOnlyList<IReadOnlyList<string?>>? data)
    {
        if (data == null || data.Count == 0)
            throw new InvalidDataException("Файл не містить даних таблиці.");

        if (data.Count > MaxRows)
            throw new InvalidDataException($"Забагато рядків у файлі (максимум {MaxRows}).");

        int columns = Math.Max(1, data.Max(row => row?.Count ?? 0));
        if (columns > MaxColumns)
            throw new InvalidDataException($"Забагато стовпчиків у файлі (максимум {MaxColumns}).");

        var sheet = new Spreadsheet(data.Count, columns);
        for (int r = 0; r < data.Count; r++)
        {
            IReadOnlyList<string?>? row = data[r];
            if (row == null)
                continue;

            for (int c = 0; c < row.Count; c++)
                sheet.cells[r][c].Raw = row[c] ?? "";
        }
        return sheet;
    }

    // ------------------------------------------------------------ обчислення

    /// <summary>Повний перерахунок таблиці у два етапи.</summary>
    public void Recalculate()
    {
        // Етап 1: класифікація клітинок і синтаксична перевірка виразів
        foreach (List<SheetCell> row in cells)
        {
            foreach (SheetCell cell in row)
                Classify(cell);
        }

        // Етап 2: обчислення значень
        foreach (List<SheetCell> row in cells)
        {
            foreach (SheetCell cell in row)
                EnsureEvaluated(cell);
        }

        isDirty = false;
    }

    private void EnsureCalculated()
    {
        if (isDirty)
            Recalculate();
    }

    private void Classify(SheetCell cell)
    {
        cell.Parsed = null;
        cell.Value = null;
        cell.Error = null;
        cell.IsSyntaxError = false;
        cell.IsEvaluated = false;
        cell.IsEvaluating = false;

        string raw = cell.Raw;
        string trimmed = raw.Trim();

        if (trimmed.Length == 0)
        {
            cell.Kind = CellKind.Empty;
            cell.IsEvaluated = true;
        }
        else if (trimmed[0] == '=')
        {
            cell.Kind = CellKind.Formula;
            try
            {
                cell.Parsed = Calculator.Parse(raw, RowCount, ColumnCount);
            }
            catch (ExpressionException ex)
            {
                cell.Error = ex;
                cell.IsSyntaxError = true;
                cell.IsEvaluated = true;
            }
        }
        else if (BigInteger.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out BigInteger number))
        {
            cell.Kind = CellKind.Number;
            cell.Constant = number;
            cell.IsEvaluated = true;
        }
        else
        {
            cell.Kind = CellKind.Text;
            cell.IsEvaluated = true;
        }
    }

    private void EnsureEvaluated(SheetCell cell)
    {
        if (cell.IsEvaluated)
            return;

        // Сюди потрапляють лише клітинки з коректним синтаксисом.
        cell.IsEvaluating = true;
        try
        {
            cell.Value = Calculator.Evaluate(cell.Parsed!, context);
        }
        catch (ExpressionException ex)
        {
            cell.Error = ex;
        }
        catch (InsufficientExecutionStackException)
        {
            cell.Error = new ExpressionException("Занадто довгий ланцюжок посилань між клітинками.", -1, 0);
        }
        finally
        {
            cell.IsEvaluating = false;
            cell.IsEvaluated = true;
        }
    }

    /// <summary>Значення клітинки для використання в інших виразах.</summary>
    private CellValue GetCellValue(int row, int column)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();

        SheetCell cell = cells[row][column];

        if (cell.IsEvaluating)
            throw new ExpressionException("циклічне посилання", -1, 0);

        EnsureEvaluated(cell);

        switch (cell.Kind)
        {
            case CellKind.Empty:
                return CellValue.FromNumber(BigInteger.Zero);

            case CellKind.Number:
                return CellValue.FromNumber(cell.Constant);

            case CellKind.Text:
                throw new ExpressionException($"містить текст «{Shorten(cell.Raw.Trim())}», а не число", -1, 0);

            default:
                if (cell.Error != null)
                    throw new ExpressionException($"помилка — {cell.Error.Message}", -1, 0);
                return cell.Value!.Value;
        }
    }

    private static string Shorten(string text) => text.Length <= 20 ? text : text[..20] + "…";

    private static List<SheetCell> CreateRow(int columns)
    {
        var row = new List<SheetCell>(columns);
        for (int c = 0; c < columns; c++)
            row.Add(new SheetCell());
        return row;
    }
}
