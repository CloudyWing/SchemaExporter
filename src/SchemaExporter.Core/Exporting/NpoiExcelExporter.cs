using System.Drawing;
using CloudyWing.SpreadsheetExporter;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using SpreadsheetHorizontalAlignment = CloudyWing.SpreadsheetExporter.HorizontalAlignment;
using SpreadsheetVerticalAlignment = CloudyWing.SpreadsheetExporter.VerticalAlignment;

namespace CloudyWing.SchemaExporter.Core.Exporting;

/// <summary>
/// Exports spreadsheets to XLSX files through NPOI.
/// </summary>
internal sealed class NpoiExcelExporter : ExporterBase {
    private const int ExcelColumnWidthUnit = 256;

    private static readonly Dictionary<DataValidationOperator, int> ValidationOperatorMap =
        new() {
            [DataValidationOperator.Between] = OperatorType.BETWEEN,
            [DataValidationOperator.NotBetween] = OperatorType.NOT_BETWEEN,
            [DataValidationOperator.Equal] = OperatorType.EQUAL,
            [DataValidationOperator.NotEqual] = OperatorType.NOT_EQUAL,
            [DataValidationOperator.GreaterThan] = OperatorType.GREATER_THAN,
            [DataValidationOperator.LessThan] = OperatorType.LESS_THAN,
            [DataValidationOperator.GreaterThanOrEqual] = OperatorType.GREATER_OR_EQUAL,
            [DataValidationOperator.LessThanOrEqual] = OperatorType.LESS_OR_EQUAL
        };

    private static readonly Dictionary<SpreadsheetHorizontalAlignment, NPOI.SS.UserModel.HorizontalAlignment> HorizontalAlignmentMap =
        new() {
            [SpreadsheetHorizontalAlignment.General] = NPOI.SS.UserModel.HorizontalAlignment.General,
            [SpreadsheetHorizontalAlignment.Left] = NPOI.SS.UserModel.HorizontalAlignment.Left,
            [SpreadsheetHorizontalAlignment.Center] = NPOI.SS.UserModel.HorizontalAlignment.Center,
            [SpreadsheetHorizontalAlignment.Right] = NPOI.SS.UserModel.HorizontalAlignment.Right,
            [SpreadsheetHorizontalAlignment.Justify] = NPOI.SS.UserModel.HorizontalAlignment.Justify
        };

    private static readonly Dictionary<SpreadsheetVerticalAlignment, NPOI.SS.UserModel.VerticalAlignment> VerticalAlignmentMap =
        new() {
            [SpreadsheetVerticalAlignment.Top] = NPOI.SS.UserModel.VerticalAlignment.Top,
            [SpreadsheetVerticalAlignment.Middle] = NPOI.SS.UserModel.VerticalAlignment.Center,
            [SpreadsheetVerticalAlignment.Bottom] = NPOI.SS.UserModel.VerticalAlignment.Bottom
        };

    private readonly Dictionary<CellStyle, ICellStyle> cellStyles = [];
    private readonly Dictionary<CellFont, IFont> fonts = [];
    private readonly Lock syncLock = new();
    private XSSFWorkbook? workbook;

    /// <inheritdoc />
    public override string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <inheritdoc />
    public override string FileNameExtension => ".xlsx";

    /// <inheritdoc />
    protected override byte[] ExecuteExport(IEnumerable<SheeterContext> contexts) {
        lock (syncLock) {
            workbook = new XSSFWorkbook();

            try {
                if (DefaultFont.HasValue) {
                    SetDefaultFont(DefaultFont.Value);
                }

                foreach (SheeterContext context in contexts) {
                    CreateSheet(context);
                }

                if (HasPassword) {
                    throw new NotImplementedException(
                        "NPOI currently does not support the output of xlsx file with passwords."
                    );
                }

                using MemoryStream memoryStream = new();
                workbook.Write(memoryStream);

                return memoryStream.ToArray();
            } finally {
                workbook.Close();
                workbook = null;
                cellStyles.Clear();
                fonts.Clear();
            }
        }
    }

    private void SetDefaultFont(CellFont font) {
        IFont defaultFont = Workbook.GetFontAt(0);
        if (!string.IsNullOrWhiteSpace(font.Name)) {
            defaultFont.FontName = font.Name;
        }

        if (font.Size != 0) {
            defaultFont.FontHeightInPoints = font.Size;
        }

        if (font.Color != Color.Empty) {
            ((XSSFFont)defaultFont).SetColor(CreateColor(font.Color));
        }

        defaultFont.IsBold = (font.Style & FontStyles.IsBold) == FontStyles.IsBold;
        defaultFont.IsItalic = (font.Style & FontStyles.IsItalic) == FontStyles.IsItalic;
        if ((font.Style & FontStyles.HasUnderline) == FontStyles.HasUnderline) {
            defaultFont.Underline = FontUnderlineType.Single;
        }
        defaultFont.IsStrikeout = (font.Style & FontStyles.IsStrikeout) == FontStyles.IsStrikeout;
    }

    private void CreateSheet(SheeterContext context) {
        ISheet sheet = Workbook.CreateSheet(context.SheetName);
        if (context.DefaultRowHeight.HasValue) {
            sheet.DefaultRowHeightInPoints = (float)context.DefaultRowHeight.Value;
        }

        SetSheetCells(sheet, context.Cells);
        SetSheetColumnWidths(sheet, context.ColumnWidths);
        SetSheetRowHeights(sheet, context.RowHeights);

        if (context.FreezePanes.HasValue) {
            sheet.CreateFreezePane(context.FreezePanes.Value.X, context.FreezePanes.Value.Y);
        }

        if (context.IsAutoFilterEnabled && context.Cells.Count > 0) {
            int maxRow = 0;
            int maxCol = 0;
            foreach (Cell cell in context.Cells) {
                int endRow = cell.Point.Y + cell.Size.Height - 1;
                int endCol = cell.Point.X + cell.Size.Width - 1;
                if (endRow > maxRow) {
                    maxRow = endRow;
                }

                if (endCol > maxCol) {
                    maxCol = endCol;
                }
            }

            sheet.SetAutoFilter(new CellRangeAddress(0, maxRow, 0, maxCol));
        }

        if (context.IsProtected) {
            sheet.ProtectSheet(context.Password);
        }

        sheet.PrintSetup.Landscape = context.PageSettings.PageOrientation == PageOrientation.Landscape;
        sheet.PrintSetup.PaperSize = (short)(int)context.PageSettings.PaperSize;
        sheet.ForceFormulaRecalculation = true;

        OnSheetCreated(new SheetCreatedEventArgs(sheet, context));
    }

    private void SetSheetCells(ISheet sheet, IReadOnlyList<Cell> cells) {
        foreach (Cell cell in cells) {
            IRow excelRow = sheet.GetRow(cell.Point.Y) ?? sheet.CreateRow(cell.Point.Y);
            ICell excelCell = excelRow.GetCell(cell.Point.X) ?? excelRow.CreateCell(cell.Point.X);
            string formula = cell.GetFormula();

            if (string.IsNullOrWhiteSpace(formula)) {
                SetValueToCell(excelCell, cell.GetValue());
            } else {
                excelCell.CellFormula = formula;
            }

            excelCell.CellStyle = ParseCellStyle(cell.GetCellStyle());

            DataValidation dataValidation = cell.GetDataValidation();
            if (dataValidation is not null) {
                SetDataValidation(sheet, cell.Point, cell.Size, dataValidation);
            }

            if (cell.Size.Width > 1 || cell.Size.Height > 1) {
                MergeRegion(
                    sheet,
                    cell.Point.X,
                    cell.Point.X + cell.Size.Width - 1,
                    cell.Point.Y,
                    cell.Point.Y + cell.Size.Height - 1
                );
            }
        }
    }

    private ICellStyle ParseCellStyle(CellStyle cellStyle) {
        if (cellStyles.TryGetValue(cellStyle, out ICellStyle? existingStyle)) {
            return existingStyle;
        }

        ICellStyle excelCellStyle = Workbook.CreateCellStyle();
        excelCellStyle.Alignment = HorizontalAlignmentMap[cellStyle.HorizontalAlignment];
        excelCellStyle.VerticalAlignment = VerticalAlignmentMap[cellStyle.VerticalAlignment];

        if (cellStyle.HasBorder) {
            excelCellStyle.BorderBottom = BorderStyle.Thin;
            excelCellStyle.BorderLeft = BorderStyle.Thin;
            excelCellStyle.BorderRight = BorderStyle.Thin;
            excelCellStyle.BorderTop = BorderStyle.Thin;
        }

        excelCellStyle.WrapText = cellStyle.WrapText;

        if (cellStyle.BackgroundColor != Color.Empty) {
            excelCellStyle.FillPattern = FillPattern.SolidForeground;
            ((XSSFCellStyle)excelCellStyle).SetFillForegroundColor(CreateColor(cellStyle.BackgroundColor));
        }

        if (cellStyle.Font != CellFont.Empty) {
            excelCellStyle.SetFont(ParseFont(cellStyle.Font));
        }

        if (!string.IsNullOrWhiteSpace(cellStyle.DataFormat)) {
            excelCellStyle.DataFormat = ParseDataFormat(cellStyle.DataFormat);
        }

        excelCellStyle.IsLocked = cellStyle.IsLocked;
        cellStyles.Add(cellStyle, excelCellStyle);

        return excelCellStyle;
    }

    private IFont ParseFont(CellFont font) {
        if (fonts.TryGetValue(font, out IFont? existingFont)) {
            return existingFont;
        }

        IFont excelFont = Workbook.CreateFont();
        if (!string.IsNullOrWhiteSpace(font.Name)) {
            excelFont.FontName = font.Name;
        }

        if (font.Size != 0) {
            excelFont.FontHeightInPoints = font.Size;
        }

        if (font.Color != Color.Empty) {
            ((XSSFFont)excelFont).SetColor(CreateColor(font.Color));
        }

        excelFont.IsBold = (font.Style & FontStyles.IsBold) == FontStyles.IsBold;
        excelFont.IsItalic = (font.Style & FontStyles.IsItalic) == FontStyles.IsItalic;
        if ((font.Style & FontStyles.HasUnderline) == FontStyles.HasUnderline) {
            excelFont.Underline = FontUnderlineType.Single;
        }
        excelFont.IsStrikeout = (font.Style & FontStyles.IsStrikeout) == FontStyles.IsStrikeout;

        fonts.Add(font, excelFont);
        return excelFont;
    }

    private short ParseDataFormat(string formatString) {
        IDataFormat dataFormat = Workbook.CreateDataFormat();
        return dataFormat.GetFormat(formatString);
    }

    private static void MergeRegion(ISheet sheet, int firstColumn, int lastColumn, int firstRow, int lastRow) {
        ICellStyle cellStyle = sheet.GetRow(firstRow).GetCell(firstColumn).CellStyle;
        for (int column = firstColumn; column <= lastColumn; column++) {
            for (int row = firstRow; row <= lastRow; row++) {
                if (column == firstColumn && row == firstRow) {
                    continue;
                }

                (sheet.GetRow(row) ?? sheet.CreateRow(row)).CreateCell(column).CellStyle = cellStyle;
            }
        }

        sheet.AddMergedRegion(new CellRangeAddress(firstRow, lastRow, firstColumn, lastColumn));
    }

    private static void SetValueToCell(ICell cell, object value) {
        if (value is null) {
            cell.SetCellValue("");
            return;
        }

        if (value is bool boolValue) {
            cell.SetCellValue(boolValue);
            return;
        }

        if (value is DateTime dateTimeValue) {
            cell.SetCellValue(dateTimeValue);
            return;
        }

        if (value is double doubleValue) {
            cell.SetCellValue(doubleValue);
            return;
        }

        if (value is not string and IConvertible) {
            try {
                cell.SetCellValue(Convert.ToDouble(value));
                return;
            } catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException) {
            }
        }

        cell.SetCellValue(value.ToString());
    }

    private static void SetSheetColumnWidths(ISheet sheet, IReadOnlyDictionary<int, double> columnWidths) {
        foreach (KeyValuePair<int, double> pair in columnWidths) {
            if (pair.Value <= Constants.AutoFitColumnWidth) {
                sheet.AutoSizeColumn(pair.Key);
            } else if (pair.Value == Constants.HiddenColumn) {
                sheet.SetColumnHidden(pair.Key, true);
            } else {
                sheet.SetColumnWidth(pair.Key, (int)(pair.Value * ExcelColumnWidthUnit));
            }
        }
    }

    private static void SetSheetRowHeights(ISheet sheet, IReadOnlyDictionary<int, double?> rowHeights) {
        foreach (KeyValuePair<int, double?> pair in rowHeights) {
            IRow row = sheet.GetRow(pair.Key) ?? sheet.CreateRow(pair.Key);
            if (pair.Value <= Constants.AutoFitRowHeight) {
                row.Height = -1;
            } else if (pair.Value == Constants.HiddenRow) {
                row.ZeroHeight = true;
            } else if (pair.Value.HasValue) {
                row.HeightInPoints = (float)pair.Value.Value;
            }
        }
    }

    private static void SetDataValidation(ISheet sheet, Point point, Size size, DataValidation validation) {
        CellRangeAddressList addressList = new(
            point.Y,
            point.Y + size.Height - 1,
            point.X,
            point.X + size.Width - 1
        );

        IDataValidationHelper validationHelper = sheet.GetDataValidationHelper();
        IDataValidationConstraint constraint = CreateValidationConstraint(validationHelper, validation);
        IDataValidation dataValidation = validationHelper.CreateValidation(constraint, addressList);

        dataValidation.EmptyCellAllowed = validation.IsBlankAllowed;
        dataValidation.ShowErrorBox = validation.IsErrorAlertShown;
        dataValidation.ShowPromptBox = validation.IsInputPromptShown;

        if (!string.IsNullOrWhiteSpace(validation.ErrorTitle) || !string.IsNullOrWhiteSpace(validation.ErrorMessage)) {
            dataValidation.CreateErrorBox(validation.ErrorTitle ?? "", validation.ErrorMessage ?? "");
        }

        if (!string.IsNullOrWhiteSpace(validation.PromptTitle) || !string.IsNullOrWhiteSpace(validation.PromptMessage)) {
            dataValidation.CreatePromptBox(validation.PromptTitle ?? "", validation.PromptMessage ?? "");
        }

        if (validation.ValidationType == DataValidationType.List && dataValidation is XSSFDataValidation xssfValidation) {
            xssfValidation.SuppressDropDownArrow = !validation.IsDropdownShown;
        }

        sheet.AddValidationData(dataValidation);
    }

    private static IDataValidationConstraint CreateValidationConstraint(IDataValidationHelper helper, DataValidation validation) {
        return validation.ValidationType switch {
            DataValidationType.List => CreateListConstraint(helper, validation),
            DataValidationType.Integer => CreateNumericConstraint(helper, validation, ValidationType.INTEGER),
            DataValidationType.Decimal => CreateNumericConstraint(helper, validation, ValidationType.DECIMAL),
            DataValidationType.Date => CreateDateConstraint(helper, validation),
            DataValidationType.Time => CreateTimeConstraint(helper, validation),
            DataValidationType.TextLength => CreateNumericConstraint(helper, validation, ValidationType.TEXT_LENGTH),
            DataValidationType.Custom => helper.CreateCustomConstraint(validation.Formula),
            _ => throw new ArgumentException($"Unsupported validation type: {validation.ValidationType}")
        };
    }

    private static IDataValidationConstraint CreateListConstraint(IDataValidationHelper helper, DataValidation validation) {
        if (validation.ListItems is null || !validation.ListItems.Any()) {
            throw new ArgumentException("ListItems cannot be null or empty for List validation type.");
        }

        return helper.CreateExplicitListConstraint(validation.ListItems.ToArray());
    }

    private static IDataValidationConstraint CreateNumericConstraint(
        IDataValidationHelper helper,
        DataValidation validation,
        int validationType
    ) {
        (int operatorType, string formula1, string formula2) = PrepareConstraintParameters(validation);
        return helper.CreateNumericConstraint(validationType, operatorType, formula1, formula2);
    }

    private static IDataValidationConstraint CreateDateConstraint(IDataValidationHelper helper, DataValidation validation) {
        (int operatorType, string formula1, string formula2) = PrepareConstraintParameters(validation);
        return helper.CreateDateConstraint(operatorType, formula1, formula2, null);
    }

    private static IDataValidationConstraint CreateTimeConstraint(IDataValidationHelper helper, DataValidation validation) {
        (int operatorType, string formula1, string formula2) = PrepareConstraintParameters(validation);
        return helper.CreateTimeConstraint(operatorType, formula1, formula2);
    }

    private static (int operatorType, string formula1, string formula2) PrepareConstraintParameters(DataValidation validation) {
        if (!validation.Operator.HasValue) {
            throw new ArgumentException($"Operator is required for {validation.ValidationType} validation type.");
        }

        int operatorType = ConvertOperator(validation.Operator.Value);
        string formula1 = !string.IsNullOrWhiteSpace(validation.Formula)
            ? EnsureFormulaPrefix(validation.Formula)
            : validation.Value1?.ToString() ?? "";
        string formula2 = validation.Value2?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(formula1)) {
            throw new ArgumentException(
                $"Either Formula or Value1 is required for {validation.ValidationType} validation type."
            );
        }

        return (operatorType, formula1, formula2);
    }

    private static string EnsureFormulaPrefix(string formula) {
        return formula.StartsWith('=') ? formula : $"={formula}";
    }

    private static int ConvertOperator(DataValidationOperator @operator) {
        if (!ValidationOperatorMap.TryGetValue(@operator, out int result)) {
            throw new ArgumentException($"Unsupported operator: {@operator}");
        }

        return result;
    }

    private static XSSFColor CreateColor(Color color) {
        XSSFColor xssfColor = new() {
            RGB = [color.R, color.G, color.B]
        };
        return xssfColor;
    }

    private XSSFWorkbook Workbook => workbook ?? throw new InvalidOperationException("Workbook has not been initialized.");
}

