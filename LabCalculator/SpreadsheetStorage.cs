using System.Text.Encodings.Web;
using System.Text.Json;

namespace LabCalculator;

/// <summary>
/// Збереження таблиці у локальний файл (JSON) та читання з нього.
/// Не залежить від інтерфейсу: працює лише зі шляхами та моделлю Spreadsheet.
/// </summary>
public static class SpreadsheetStorage
{
    public const string FileExtension = ".json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Формат файлу.</summary>
    internal sealed class TableFile
    {
        public int Version { get; set; } = 1;
        public int Rows { get; set; }
        public int Columns { get; set; }
        public List<List<string>> Cells { get; set; } = new();
    }

    public static string BuildPath(string directory, string fileName)
        => Path.Combine(directory, fileName + FileExtension);

    /// <summary>Чи можна використати рядок як ім'я файлу (без розширення).</summary>
    public static bool IsValidFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return false;

        if (name.EndsWith('.') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        return true;
    }

    /// <summary>Імена збережених таблиць (без розширення) у каталозі.</summary>
    public static IReadOnlyList<string> ListFiles(string directory)
    {
        if (!Directory.Exists(directory))
            return Array.Empty<string>();

        return Directory.GetFiles(directory, "*" + FileExtension)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static void Save(Spreadsheet sheet, string path)
    {
        var file = new TableFile
        {
            Rows = sheet.RowCount,
            Columns = sheet.ColumnCount,
            Cells = sheet.ToRaw()
        };

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
    }

    /// <exception cref="InvalidDataException">Файл має некоректний формат.</exception>
    public static Spreadsheet Load(string path)
    {
        TableFile? file;
        try
        {
            file = JsonSerializer.Deserialize<TableFile>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException)
        {
            throw new InvalidDataException("Файл має некоректний формат (очікувався JSON із таблицею).");
        }

        return Spreadsheet.FromRaw(file?.Cells);
    }
}
