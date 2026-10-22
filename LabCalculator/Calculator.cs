using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace LabCalculator;

/// <summary>Результат етапу 1: дерево розбору та позиція початку виразу в тексті клітинки.</summary>
public sealed class ParsedFormula
{
    internal ParsedFormula(IParseTree tree, int offset)
    {
        Tree = tree;
        Offset = offset;
    }

    internal IParseTree Tree { get; }

    internal int Offset { get; }
}

/// <summary>
/// Обчислення виразів із використанням ANTLR4 (аналог класу Calculator із посібника).
/// Робота у два етапи: Parse (синтаксична перевірка) та Evaluate (обчислення).
/// </summary>
public static class Calculator
{
    /// <summary>
    /// Для варіантів 20–81 вираз є логічним: верхня операція має бути порівнянням.
    /// Якщо викладач дозволить і арифметичні вирази — поставте false.
    /// </summary>
    public const bool RequireLogicalResult = true;

    public const int MaxLength = 500;

    /// <summary>
    /// Етап 1. Розбирає вираз і перевіряє його синтаксис та посилання на клітинки.
    /// </summary>
    /// <param name="text">Повний текст клітинки разом зі знаком «=».</param>
    /// <param name="rowCount">Кількість рядків таблиці.</param>
    /// <param name="columnCount">Кількість стовпчиків таблиці.</param>
    /// <exception cref="ExpressionException">Помилка з позицією в тексті клітинки.</exception>
    public static ParsedFormula Parse(string text, int rowCount, int columnCount)
    {
        if (text.Length > MaxLength)
            throw new ExpressionException($"Вираз занадто довгий (максимум {MaxLength} символів).", MaxLength);

        int offset = text.IndexOf('=') + 1;
        string expression = text[offset..];

        if (string.IsNullOrWhiteSpace(expression))
            throw new ExpressionException("Після знака «=» немає виразу.", offset);

        var lexer = new LabCalculatorLexer(new AntlrInputStream(expression));
        lexer.RemoveErrorListeners();

        var tokens = new CommonTokenStream(lexer);
        var parser = new LabCalculatorParser(tokens);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new ThrowExceptionErrorListener(expression, offset));

        var tree = parser.compileUnit();

        new StaticChecker(offset, rowCount, columnCount).Visit(tree);

        return new ParsedFormula(tree, offset);
    }

    /// <summary>
    /// Етап 2. Обчислює розібраний вираз; значення клітинок бере з context.
    /// </summary>
    public static CellValue Evaluate(ParsedFormula formula, IEvaluationContext context)
        => new LabCalculatorVisitor(context, formula.Offset).Visit(formula.Tree);

    /// <summary>
    /// Зручний метод для виразів без посилань на клітинки, наприклад «=2+3*4&gt;10».
    /// </summary>
    public static CellValue Evaluate(string text)
        => Evaluate(Parse(text, 0, 0), new NoCellsContext());

    private sealed class NoCellsContext : IEvaluationContext
    {
        public CellValue GetCellValue(int row, int column)
            => throw new ExpressionException("посилання на клітинки тут недоступні", -1, 0);
    }
}
