using Antlr4.Runtime;

namespace LabCalculator;

/// <summary>
/// Слухач помилок ANTLR: на першій же синтаксичній помилці кидає ExpressionException
/// з українським повідомленням і позицією помилки в тексті клітинки
/// (аналог ThrowExceptionErrorListener із посібника).
/// </summary>
internal sealed class ThrowExceptionErrorListener : BaseErrorListener
{
    private const int EndOfInput = -1;

    private readonly string expression;
    private readonly int offset;

    /// <param name="expression">Текст виразу без знака «=».</param>
    /// <param name="offset">Позиція першого символу виразу в тексті клітинки.</param>
    public ThrowExceptionErrorListener(string expression, int offset)
    {
        this.expression = expression;
        this.offset = offset;
    }

    public override void SyntaxError(
        TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
        int line, int charPositionInLine, string msg, RecognitionException e)
    {
        bool isEnd = offendingSymbol == null || offendingSymbol.Type == EndOfInput;
        int start = isEnd ? expression.Length : offendingSymbol!.StartIndex;
        string text = isEnd ? "" : offendingSymbol!.Text ?? "";
        int type = isEnd ? EndOfInput : offendingSymbol!.Type;

        List<int> expected = GetExpectedTokens(recognizer);
        string message = BuildMessage(type, text, expected);

        throw new ExpressionException(message, offset + start, Math.Max(1, text.Length));
    }

    private static List<int> GetExpectedTokens(IRecognizer recognizer)
    {
        var result = new List<int>();
        try
        {
            if (recognizer is Parser parser)
            {
                var set = parser.GetExpectedTokens();
                for (int t = EndOfInput; t <= LabCalculatorParser.ERROR_CHAR; t++)
                {
                    if (t != 0 && set.Contains(t))
                        result.Add(t);
                }
            }
        }
        catch (Exception)
        {
            // Якщо очікувані лексеми визначити не вдалося — повідомлення буде загальнішим.
        }
        return result;
    }

    private static string BuildMessage(int type, string text, List<int> expected)
    {
        bool expectsOperand = expected.Contains(LabCalculatorParser.NUMBER);
        bool expectsEnd = expected.Contains(EndOfInput);
        bool expectsRightParen = expected.Contains(LabCalculatorParser.RPAREN);
        bool expectsComma = expected.Contains(LabCalculatorParser.COMMA);

        const string operandHint = "число, ім'я клітинки, функція MAX/MIN або «(»";

        // Кінець виразу
        if (type == EndOfInput)
        {
            if (expectsOperand)
                return $"Вираз закінчився передчасно: очікувалося {operandHint}.";
            if (expectsRightParen)
                return "Не закрито дужку «(».";
            if (expectsComma)
                return "Вираз закінчився передчасно: очікувалася кома між аргументами функції.";
            return "Вираз закінчився передчасно.";
        }

        // Некоректні символи та імена
        if (type == LabCalculatorParser.ERROR_CHAR)
        {
            char c = text.Length > 0 ? text[0] : '?';
            if (c == '.')
                return "Дробові числа не підтримуються: дозволені лише цілі числа.";
            if (char.IsLetter(c))
                return $"Літера «{c}» не є латинською. Використовуйте латинські літери A–Z.";
            return $"Недопустимий символ «{text}».";
        }

        if (type == LabCalculatorParser.NAME)
            return $"Невідоме ім'я «{text}». Ім'я клітинки складається з латинських літер і номера рядка " +
                   "(наприклад, A1); підтримуються функції MAX та MIN.";

        // Унарні операції
        if ((type == LabCalculatorParser.ADD || type == LabCalculatorParser.SUB) && expectsOperand)
            return "Унарні операції «+» та «-» у цьому варіанті не підтримуються.";

        if (expectsOperand)
            return $"Очікувалося {operandHint}, а знайдено «{text}».";

        // Після завершеного операнда знайдено щось зайве
        if (type == LabCalculatorParser.RPAREN && expectsEnd)
            return "Зайва закриваюча дужка «)».";

        if (IsComparison(type) && expectsEnd)
            return "У виразі може бути лише одна операція порівняння.";

        if (IsComparison(type) && (expectsRightParen || expectsComma))
            return "Усередині дужок та аргументів функцій можуть бути лише арифметичні вирази: порівняння там не допускається.";

        if (type == LabCalculatorParser.COMMA && expectsEnd)
            return "Кома дозволена лише між аргументами функцій MAX і MIN (дробові числа не підтримуються).";

        if (type is LabCalculatorParser.NUMBER or LabCalculatorParser.CELL
            or LabCalculatorParser.LPAREN or LabCalculatorParser.MAX or LabCalculatorParser.MIN)
            return "Пропущено операцію (+, -, *, /, ^) між операндами.";

        string hint = expectsRightParen && expectsComma ? "Очікувалася операція, «,» або «)»."
            : expectsRightParen ? "Очікувалася операція або «)»."
            : expectsComma ? "Очікувалася операція або «,»."
            : "Очікувалася операція або кінець виразу.";
        return $"Неочікуваний елемент «{text}». {hint}";
    }

    private static bool IsComparison(int type)
        => type is LabCalculatorParser.EQ or LabCalculatorParser.NEQ or LabCalculatorParser.LT
            or LabCalculatorParser.GT or LabCalculatorParser.LE or LabCalculatorParser.GE;
}
