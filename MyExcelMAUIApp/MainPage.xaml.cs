using System.Globalization;
using LabCalculator;
using Label = Microsoft.Maui.Controls.Label;
using MauiApplication = Microsoft.Maui.Controls.Application;
using MauiColor = Microsoft.Maui.Graphics.Color;

namespace MyExcelMAUIApp;

/// <summary>
/// Головна сторінка (користувацький інтерфейс).
/// Тут лише робота з елементами керування; уся логіка таблиці, розбору та обчислення виразів
/// та збереження у файл винесена в проєкт-бібліотеку LabCalculator.
/// </summary>
public partial class MainPage : ContentPage
{
    private const int InitialRows = 20;
    private const int InitialColumns = 8;
    private const string CancelText = "Скасувати";
    private const string OtherFileText = "Обрати інший файл…";

    private static readonly MauiColor CellBackground = Colors.White;
    private static readonly MauiColor ErrorBackground = MauiColor.FromArgb("#FFD9D9");
    private static readonly MauiColor HeaderBackground = MauiColor.FromArgb("#E5EAF0");

    private Spreadsheet sheet = new(InitialRows, InitialColumns);

    // Елементи керування таблиці
    private readonly List<List<Entry>> cells = new();
    private readonly List<Label> rowLabels = new();
    private readonly List<Label> columnLabels = new();

    private int selectedRow = -1;
    private int selectedColumn = -1;
    private bool showValues;      // false — режим ВИРАЗ, true — режим ЗНАЧЕННЯ
    private bool isRefreshing;    // true, поки програма сама змінює вміст клітинок
    private string currentFileName = "table";

    private static string SaveDirectory => FileSystem.AppDataDirectory;

    public MainPage()
    {
        InitializeComponent();
        RebuildGrid();
    }

    // =====================================================================
    //  Побудова таблиці
    // =====================================================================

    private void RebuildGrid()
    {
        grid.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();
        cells.Clear();
        rowLabels.Clear();
        columnLabels.Clear();

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = 48 });

        var corner = MakeHeader("");
        Grid.SetRow(corner, 0);
        Grid.SetColumn(corner, 0);
        grid.Children.Add(corner);

        for (int c = 0; c < sheet.ColumnCount; c++)
            AppendColumnToGrid();
        for (int r = 0; r < sheet.RowCount; r++)
            AppendRowToGrid();

        ClearSelection();
        RefreshDisplay();
        UpdateStatus();
    }

    private void AppendRowToGrid()
    {
        int r = cells.Count;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = MakeHeader((r + 1).ToString(CultureInfo.InvariantCulture));
        rowLabels.Add(label);
        Grid.SetRow(label, r + 1);
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var rowCells = new List<Entry>();
        for (int c = 0; c < columnLabels.Count; c++)
        {
            var entry = MakeCell();
            rowCells.Add(entry);
            Grid.SetRow(entry, r + 1);
            Grid.SetColumn(entry, c + 1);
            grid.Children.Add(entry);
        }
        cells.Add(rowCells);
    }

    private void AppendColumnToGrid()
    {
        int c = columnLabels.Count;
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = 115 });

        var header = MakeHeader(CellAddress.GetColumnName(c + 1));
        columnLabels.Add(header);
        Grid.SetRow(header, 0);
        Grid.SetColumn(header, c + 1);
        grid.Children.Add(header);

        for (int r = 0; r < cells.Count; r++)
        {
            var entry = MakeCell();
            cells[r].Add(entry);
            Grid.SetRow(entry, r + 1);
            Grid.SetColumn(entry, c + 1);
            grid.Children.Add(entry);
        }
    }

    private void RemoveLastRowFromGrid()
    {
        foreach (var entry in cells[^1])
            grid.Children.Remove(entry);
        cells.RemoveAt(cells.Count - 1);

        grid.Children.Remove(rowLabels[^1]);
        rowLabels.RemoveAt(rowLabels.Count - 1);

        grid.RowDefinitions.RemoveAt(grid.RowDefinitions.Count - 1);
    }

    private void RemoveLastColumnFromGrid()
    {
        foreach (var row in cells)
        {
            grid.Children.Remove(row[^1]);
            row.RemoveAt(row.Count - 1);
        }

        grid.Children.Remove(columnLabels[^1]);
        columnLabels.RemoveAt(columnLabels.Count - 1);

        grid.ColumnDefinitions.RemoveAt(grid.ColumnDefinitions.Count - 1);
    }

    private static Label MakeHeader(string text)
    {
        return new Label
        {
            Text = text,
            BackgroundColor = HeaderBackground,
            TextColor = Colors.Black,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            Padding = new Thickness(6, 8),
            MinimumHeightRequest = 38
        };
    }

    private Entry MakeCell()
    {
        var entry = new Entry
        {
            Text = "",
            BackgroundColor = CellBackground,
            TextColor = Colors.Black,
            FontSize = 14,
            Margin = 0,
            HeightRequest = 38,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing
        };
        entry.Focused += Cell_Focused;
        entry.Unfocused += Cell_Unfocused;
        entry.TextChanged += Cell_TextChanged;
        return entry;
    }

    // =====================================================================
    //  Відображення даних моделі
    // =====================================================================

    /// <summary>Оновлює вміст і вигляд усіх клітинок відповідно до моделі та поточного режиму.</summary>
    private void RefreshDisplay()
    {
        isRefreshing = true;
        try
        {
            for (int r = 0; r < cells.Count; r++)
            {
                for (int c = 0; c < cells[r].Count; c++)
                {
                    Entry entry = cells[r][c];
                    string text = showValues ? sheet.GetDisplayText(r, c) : sheet.GetRaw(r, c);
                    bool hasError = sheet.GetError(r, c) != null;

                    if (entry.Text != text)
                        entry.Text = text;

                    entry.IsReadOnly = showValues;
                    entry.BackgroundColor = hasError ? ErrorBackground : CellBackground;
                    entry.TextColor = hasError && showValues ? Colors.Red : Colors.Black;
                }
            }
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private void UpdateStatus()
    {
        CellErrorInfo? error = selectedRow >= 0 ? sheet.GetError(selectedRow, selectedColumn) : null;
        if (error != null)
        {
            statusLabel.TextColor = Colors.Red;
            statusLabel.Text = error.Describe();
            return;
        }

        int count = sheet.GetErrors().Count;
        if (count == 0)
        {
            statusLabel.TextColor = Colors.Green;
            statusLabel.Text = "Помилок у таблиці немає.";
        }
        else
        {
            statusLabel.TextColor = Colors.OrangeRed;
            statusLabel.Text = $"Помилок у таблиці: {count}. Виберіть клітинку з червоним фоном, щоб побачити опис помилки.";
        }
    }

    private void SelectCell(int row, int column)
    {
        selectedRow = row;
        selectedColumn = column;
        cellNameLabel.Text = CellAddress.GetName(row, column);
        textInput.Text = sheet.GetRaw(row, column);
        UpdateStatus();

        // Локалізація синтаксичної помилки: ставимо курсор у місце помилки
        CellErrorInfo? error = sheet.GetError(row, column);
        if (!showValues && error != null && error.Exception.Position >= 0)
        {
            Entry entry = cells[row][column];
            int position = Math.Min(error.Exception.Position, (entry.Text ?? "").Length);
            Dispatcher.Dispatch(() => entry.CursorPosition = position);
        }
    }

    private void ClearSelection()
    {
        selectedRow = -1;
        selectedColumn = -1;
        cellNameLabel.Text = "—";
        textInput.Text = "";
    }

    private void ValidateSelection()
    {
        if (selectedRow >= sheet.RowCount || selectedColumn >= sheet.ColumnCount)
            ClearSelection();
    }

    /// <summary>
    /// У режимі ВИРАЗ переносить текст із клітинок у модель
    /// (на випадок, якщо клітинка ще не втратила фокус).
    /// </summary>
    private void SyncModelFromGrid()
    {
        if (showValues)
            return;

        for (int r = 0; r < cells.Count; r++)
        {
            for (int c = 0; c < cells[r].Count; c++)
                sheet.SetRaw(r, c, cells[r][c].Text);
        }
    }

    // =====================================================================
    //  Події клітинок та рядка введення
    // =====================================================================

    private void Cell_Focused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry)
            return;

        int row = Grid.GetRow(entry) - 1;
        int column = Grid.GetColumn(entry) - 1;
        if (row < 0 || row >= sheet.RowCount || column < 0 || column >= sheet.ColumnCount)
            return;

        SelectCell(row, column);
    }

    private void Cell_Unfocused(object? sender, FocusEventArgs e)
    {
        if (showValues || sender is not Entry entry)
            return;

        int row = Grid.GetRow(entry) - 1;
        int column = Grid.GetColumn(entry) - 1;
        if (row < 0 || row >= sheet.RowCount || column < 0 || column >= sheet.ColumnCount)
            return;

        sheet.SetRaw(row, column, entry.Text);
        RefreshDisplay();
        UpdateStatus();
    }

    private void Cell_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (isRefreshing || showValues || sender is not Entry entry)
            return;

        // Рядок «Вираз» показує вміст вибраної клітинки під час набору
        if (Grid.GetRow(entry) - 1 == selectedRow && Grid.GetColumn(entry) - 1 == selectedColumn)
            textInput.Text = e.NewTextValue ?? "";
    }

    private void TextInput_Completed(object? sender, EventArgs e)
    {
        if (selectedRow < 0)
            return;

        SyncModelFromGrid();
        sheet.SetRaw(selectedRow, selectedColumn, textInput.Text);
        RefreshDisplay();
        UpdateStatus();
    }

    private void ModeSwitch_Toggled(object? sender, ToggledEventArgs e)
    {
        if (e.Value)
            SyncModelFromGrid();   // showValues ще false: беремо введений текст

        showValues = e.Value;
        RefreshDisplay();
        UpdateStatus();
    }

    // =====================================================================
    //  Кнопки (обробники лише викликають методи моделі та показують результат)
    // =====================================================================

    private async void CalculateButton_Clicked(object? sender, EventArgs e)
    {
        SyncModelFromGrid();
        string report = sheet.BuildReport();   // етап 1: синтаксис, етап 2: обчислення

        modeSwitch.IsToggled = true;           // показуємо значення
        RefreshDisplay();
        UpdateStatus();

        await DisplayAlertAsync("Результат обчислення", report, "OK");
    }

    private async void SaveButton_Clicked(object? sender, EventArgs e)
    {
        SyncModelFromGrid();

        string? name = await DisplayPromptAsync(
            "Збереження", "Введіть ім'я файлу (без розширення):",
            "Зберегти", CancelText, initialValue: currentFileName);
        if (name == null)
            return;

        name = name.Trim();
        if (!SpreadsheetStorage.IsValidFileName(name))
        {
            await DisplayAlertAsync("Помилка", "Некоректне ім'я файлу. Не використовуйте символи \\ / : * ? \" < > |.", "OK");
            return;
        }

        string path = SpreadsheetStorage.BuildPath(SaveDirectory, name);
        if (File.Exists(path))
        {
            bool overwrite = await DisplayAlertAsync("Збереження", $"Файл «{name}» уже існує. Перезаписати?", "Так", "Ні");
            if (!overwrite)
                return;
        }

        try
        {
            SpreadsheetStorage.Save(sheet, path);
            currentFileName = name;
            await DisplayAlertAsync("Збереження", $"Таблицю збережено у файл:\n{path}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Помилка", $"Не вдалося зберегти таблицю: {ex.Message}", "OK");
        }
    }

    private async void ReadButton_Clicked(object? sender, EventArgs e)
    {
        var options = SpreadsheetStorage.ListFiles(SaveDirectory).ToList();
        options.Add(OtherFileText);

        string choice = await DisplayActionSheetAsync("Оберіть файл таблиці", CancelText, null, options.ToArray());
        if (string.IsNullOrEmpty(choice) || choice == CancelText)
            return;

        string? path;
        if (choice == OtherFileText)
        {
            try
            {
                path = await PickFileAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Помилка", $"Не вдалося відкрити вікно вибору файлу: {ex.Message}", "OK");
                return;
            }
        }
        else
        {
            path = SpreadsheetStorage.BuildPath(SaveDirectory, choice);
        }

        if (path == null)
            return;

        try
        {
            sheet = SpreadsheetStorage.Load(path);
            currentFileName = Path.GetFileNameWithoutExtension(path);
            RebuildGrid();
            await DisplayAlertAsync("Читання", "Таблицю успішно завантажено.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Помилка", $"Не вдалося прочитати файл: {ex.Message}", "OK");
        }
    }

    private static async Task<string?> PickFileAsync()
    {
        var jsonType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.WinUI, new[] { ".json" } },
            { DevicePlatform.MacCatalyst, new[] { "json" } },
            { DevicePlatform.Android, new[] { "application/json" } },
            { DevicePlatform.iOS, new[] { "public.json" } }
        });

        FileResult? result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Оберіть файл таблиці (.json)",
            FileTypes = jsonType
        });
        return result?.FullPath;
    }

    private async void AddRowButton_Clicked(object? sender, EventArgs e)
    {
        SyncModelFromGrid();
        if (!sheet.AddRow())
        {
            await DisplayAlertAsync("Увага", $"Досягнуто максимальної кількості рядків ({Spreadsheet.MaxRows}).", "OK");
            return;
        }

        AppendRowToGrid();
        RefreshDisplay();
        UpdateStatus();
    }

    private async void DeleteRowButton_Clicked(object? sender, EventArgs e)
    {
        SyncModelFromGrid();
        if (sheet.RowCount <= 1)
        {
            await DisplayAlertAsync("Увага", "У таблиці має залишитися хоча б один рядок.", "OK");
            return;
        }

        if (!sheet.IsLastRowEmpty())
        {
            bool confirm = await DisplayAlertAsync("Видалення рядка",
                $"Рядок {sheet.RowCount} містить дані. Видалити його?", "Так", "Ні");
            if (!confirm)
                return;
        }

        sheet.RemoveLastRow();
        RemoveLastRowFromGrid();
        ValidateSelection();
        RefreshDisplay();
        UpdateStatus();
    }

    private async void AddColumnButton_Clicked(object? sender, EventArgs e)
    {
        SyncModelFromGrid();
        if (!sheet.AddColumn())
        {
            await DisplayAlertAsync("Увага", $"Досягнуто максимальної кількості стовпчиків ({Spreadsheet.MaxColumns}).", "OK");
            return;
        }

        AppendColumnToGrid();
        RefreshDisplay();
        UpdateStatus();
    }

    private async void DeleteColumnButton_Clicked(object? sender, EventArgs e)
    {
        SyncModelFromGrid();
        if (sheet.ColumnCount <= 1)
        {
            await DisplayAlertAsync("Увага", "У таблиці має залишитися хоча б один стовпчик.", "OK");
            return;
        }

        if (!sheet.IsLastColumnEmpty())
        {
            bool confirm = await DisplayAlertAsync("Видалення стовпчика",
                $"Стовпчик {CellAddress.GetColumnName(sheet.ColumnCount)} містить дані. Видалити його?", "Так", "Ні");
            if (!confirm)
                return;
        }

        sheet.RemoveLastColumn();
        RemoveLastColumnFromGrid();
        ValidateSelection();
        RefreshDisplay();
        UpdateStatus();
    }

    private async void HelpButton_Clicked(object? sender, EventArgs e)
    {
        await DisplayAlertAsync("Довідка",
            "Робота з таблицею\n" +
            "• Клікніть клітинку й введіть дані. Ціле число — звичайне значення, текст, що починається зі знака «=», — вираз.\n" +
            "• Рядок «Вираз» показує вміст вибраної клітинки; змінивши його, натисніть Enter.\n" +
            "• Перемикач «ВИРАЗ / ЗНАЧЕННЯ» змінює, що показано в таблиці: введені вирази чи обчислені значення.\n\n" +
            "Вирази (варіант 44)\n" +
            "• Цілі числа довільної довжини, круглі дужки, імена клітинок (A1, B12).\n" +
            "• Операції: +  -  *  /  ^ (степінь); «/» — цілочисельне ділення.\n" +
            "• Функції: MAX(x, y), MIN(x, y).\n" +
            "• Порівняння: =  <>  <  >  <=  >=. Вираз має бути логічним, тобто містити порівняння; результат — ІСТИНА або НЕПРАВДА.\n" +
            "• Приклади: =A1+B1*2>10    =MAX(A1, B1)>=5    =2^100=1267650600228229401496703205376\n\n" +
            "Кнопки\n" +
            "• «Порахувати» — перевіряє синтаксис усіх виразів, обчислює їх і показує помилки з позиціями.\n" +
            "• «Зберегти» / «Прочитати» — запис таблиці у файл і читання з файлу (.json).\n" +
            "• «Додати / Видалити рядок (стовпчик)» — змінюють розмір таблиці.",
            "OK");
    }

    private async void AboutButton_Clicked(object? sender, EventArgs e)
    {
        await DisplayAlertAsync("Про програму",
            "My Excel MAUI — навчальна програма для роботи з електронними таблицями " +
            "(аналіз та обчислення виразів).\n\n" +
            "Лабораторна робота 1, варіант 44.\n" +
            "Виконала: Стецюк Катерина, група К-27.\n\n" +
            $"Файли таблиць зберігаються в папці:\n{SaveDirectory}",
            "OK");
    }

    private async void ExitButton_Clicked(object? sender, EventArgs e)
    {
        bool answer = await DisplayAlertAsync("Підтвердження", "Ви дійсно хочете закрити програму?", "Так", "Ні");
        if (answer)
            MauiApplication.Current?.Quit();
    }
}
