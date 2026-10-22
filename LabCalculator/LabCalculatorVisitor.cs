using System.Globalization;
using System.Numerics;
using Antlr4.Runtime;

namespace LabCalculator;

/// <summary>
/// Обчислення виразу обходом дерева розбору ANTLR (етап 2).
/// Значення — цілі числа довільної довжини (BigInteger) або логічні значення.
/// </summary>
public sealed class LabCalculatorVisitor : LabCalculatorBaseVisitor<CellValue>
{
    private const int MaxExponent = 100_000;
    private const long MaxResultBits = 200_000;

    private readonly IEvaluationContext cellsContext;
    private readonly int offset;

    /// <param name="cellsContext">Джерело значень клітинок.</param>
    /// <param name="offset">Позиція початку виразу в тексті клітинки (для вказування місця помилки).</param>
    public LabCalculatorVisitor(IEvaluationContext cellsContext, int offset)
    {
        this.cellsContext = cellsContext;
        this.offset = offset;
    }

    public override CellValue VisitCompileUnit(LabCalculatorParser.CompileUnitContext context)
        => Visit(context.formula());

    // ------------------------------------------------------------ порівняння

    public override CellValue VisitFormula(LabCalculatorParser.FormulaContext context)
    {
        CellValue left = Visit(context.left);
        if (context.op == null)
            return left;

        CellValue right = Visit(context.right);
        int type = context.op.Type;

        // = та <> допускають числа або логічні значення (однакового типу)
        if (type == LabCalculatorParser.EQ || type == LabCalculatorParser.NEQ)
        {
            if (left.IsLogical != right.IsLogical)
                throw ErrorAt(context.op, "Не можна порівнювати число з логічним значенням.");

            bool equal = left.IsLogical ? left.Logical == right.Logical : left.Number == right.Number;
            return CellValue.FromLogical(type == LabCalculatorParser.EQ ? equal : !equal);
        }

        const string title = "Операція порівняння";
        int cmp = RequireNumber(left, context.left, title).CompareTo(RequireNumber(right, context.right, title));

        bool result = type switch
        {
            LabCalculatorParser.LT => cmp < 0,
            LabCalculatorParser.GT => cmp > 0,
            LabCalculatorParser.LE => cmp <= 0,
            _ => cmp >= 0
        };
        return CellValue.FromLogical(result);
    }

    // ------------------------------------------------------------ арифметика

    public override CellValue VisitParenthesizedExpr(LabCalculatorParser.ParenthesizedExprContext context)
        => Visit(context.expression());

    public override CellValue VisitExponentialExpr(LabCalculatorParser.ExponentialExprContext context)
    {
        const string title = "Операція «^»";
        BigInteger a = RequireNumber(Visit(context.expression(0)), context.expression(0), title);
        BigInteger b = RequireNumber(Visit(context.expression(1)), context.expression(1), title);
        IToken op = context.POW().Symbol;

        if (b.Sign < 0)
            throw ErrorAt(op, "Піднесення до від'ємного степеня не підтримується (результат не є цілим числом).");

        if (b > MaxExponent)
            throw ErrorAt(op, $"Занадто великий показник степеня (максимум {MaxExponent}).");

        int exponent = (int)b;
        if (BigInteger.Abs(a) > BigInteger.One && a.GetBitLength() * exponent > MaxResultBits)
            throw ErrorAt(op, "Результат піднесення до степеня занадто великий.");

        return CellValue.FromNumber(BigInteger.Pow(a, exponent));
    }

    public override CellValue VisitMultiplicativeExpr(LabCalculatorParser.MultiplicativeExprContext context)
    {
        bool multiply = context.op.Type == LabCalculatorParser.MUL;
        string title = multiply ? "Операція «*»" : "Операція «/»";
        BigInteger a = RequireNumber(Visit(context.expression(0)), context.expression(0), title);
        BigInteger b = RequireNumber(Visit(context.expression(1)), context.expression(1), title);

        if (multiply)
            return CellValue.FromNumber(a * b);

        if (b.IsZero)
            throw ErrorAt(context.op, "Ділення на нуль.");

        // Цілочисельне ділення (дробова частина відкидається)
        return CellValue.FromNumber(BigInteger.Divide(a, b));
    }

    public override CellValue VisitAdditiveExpr(LabCalculatorParser.AdditiveExprContext context)
    {
        bool add = context.op.Type == LabCalculatorParser.ADD;
        string title = add ? "Операція «+»" : "Операція «-»";
        BigInteger a = RequireNumber(Visit(context.expression(0)), context.expression(0), title);
        BigInteger b = RequireNumber(Visit(context.expression(1)), context.expression(1), title);

        return CellValue.FromNumber(add ? a + b : a - b);
    }

    public override CellValue VisitFunctionExpr(LabCalculatorParser.FunctionExprContext context)
    {
        bool isMax = context.func.Type == LabCalculatorParser.MAX;
        string title = isMax ? "Функція MAX" : "Функція MIN";
        BigInteger a = RequireNumber(Visit(context.expression(0)), context.expression(0), title);
        BigInteger b = RequireNumber(Visit(context.expression(1)), context.expression(1), title);

        return CellValue.FromNumber(isMax ? BigInteger.Max(a, b) : BigInteger.Min(a, b));
    }

    // ------------------------------------------------------------ операнди

    public override CellValue VisitNumberExpr(LabCalculatorParser.NumberExprContext context)
        => CellValue.FromNumber(BigInteger.Parse(context.GetText(), NumberStyles.None, CultureInfo.InvariantCulture));

    public override CellValue VisitIdentifierExpr(LabCalculatorParser.IdentifierExprContext context)
    {
        string text = context.GetText();
        if (!CellAddress.TryParse(text, out int row, out int column))
            throw ErrorAt(context, $"Невідоме ім'я «{text}».");

        try
        {
            return cellsContext.GetCellValue(row, column);
        }
        catch (ExpressionException inner)
        {
            throw ErrorAt(context, $"Клітинка {CellAddress.GetName(row, column)}: {inner.Message}");
        }
    }

    // ------------------------------------------------------------ допоміжні методи

    private BigInteger RequireNumber(CellValue value, ParserRuleContext operand, string title)
    {
        if (value.IsLogical)
            throw ErrorAt(operand, $"{title} потребує числових операндів, а тут логічне значення.");
        return value.Number;
    }

    private ExpressionException ErrorAt(IToken token, string message)
        => new(message, offset + token.StartIndex, Math.Max(1, token.Text?.Length ?? 1));

    private ExpressionException ErrorAt(ParserRuleContext context, string message)
    {
        int start = context.Start.StartIndex;
        int stop = context.Stop?.StopIndex ?? context.Start.StopIndex;
        return new ExpressionException(message, offset + start, Math.Max(1, stop - start + 1));
    }
}
