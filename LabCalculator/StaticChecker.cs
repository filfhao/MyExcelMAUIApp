namespace LabCalculator;

/// <summary>
/// Додаткова перевірка дерева розбору на етапі 1 (до обчислення):
/// вираз має бути логічним, а посилання на клітинки — вказувати на клітинки таблиці.
/// </summary>
internal sealed class StaticChecker : LabCalculatorBaseVisitor<int>
{
    private readonly int offset;
    private readonly int rowCount;
    private readonly int columnCount;

    public StaticChecker(int offset, int rowCount, int columnCount)
    {
        this.offset = offset;
        this.rowCount = rowCount;
        this.columnCount = columnCount;
    }

    public override int VisitFormula(LabCalculatorParser.FormulaContext context)
    {
        if (Calculator.RequireLogicalResult && context.op == null)
        {
            int start = context.Start.StartIndex;
            int stop = context.Stop?.StopIndex ?? start;
            throw new ExpressionException(
                "У цьому варіанті вираз має бути логічним: додайте операцію порівняння (=, <>, <, >, <=, >=).",
                offset + start, stop - start + 1);
        }

        return base.VisitFormula(context);
    }

    public override int VisitIdentifierExpr(LabCalculatorParser.IdentifierExprContext context)
    {
        string text = context.GetText();
        int position = offset + context.Start.StartIndex;

        if (!CellAddress.TryParse(text, out int row, out int column))
            throw new ExpressionException(
                $"Невідоме ім'я «{text}». Ім'я клітинки складається з латинських літер і номера рядка, наприклад A1.",
                position, text.Length);

        if (row >= rowCount || column >= columnCount)
            throw new ExpressionException(
                $"Клітинки «{text.ToUpperInvariant()}» немає в таблиці (розмір таблиці: {rowCount} рядків × {columnCount} стовпчиків).",
                position, text.Length);

        return 0;
    }
}
