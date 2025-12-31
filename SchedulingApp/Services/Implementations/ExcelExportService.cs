using ClosedXML.Excel;
using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;

namespace SchedulingApp.Services.Implementations
{
    public class ExcelExportService : IExcelExportService
    {
        private readonly IDataService _dataService;

        public ExcelExportService(IDataService dataService)
        {
            _dataService = dataService;
        }

        public void ExportScheduleToExcel(Dictionary<string, List<ScheduleExportModel>> schedule, string filePath, DateTime startDate, DateTime endDate)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("排班表");

            // 设置表头
            worksheet.Cell(1, 1).Value = "姓名";
            worksheet.Cell(1, 2).Value = "工号";
            worksheet.Cell(1, 3).Value = "组别";

            // 添加指定范围内的日期列标题 (from startDate to endDate)
            int colIndex = 4; // 从第4列开始添加日期
            var dateHeaders = new List<string>();

            var currentDate = startDate.Date;
            while (currentDate <= endDate.Date)
            {
                worksheet.Cell(1, colIndex).Value = currentDate.ToString("MM-dd");
                dateHeaders.Add(currentDate.ToString("yyyy-MM-dd")); // Store full date for lookup
                colIndex++;
                currentDate = currentDate.AddDays(1);
            }

            // 设置表头样式
            var headerRange = worksheet.Range(1, 1, 1, colIndex - 1);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            // Fill data - process schedule by date
            int rowIndex = 2; // Start from row 2

            // Get all unique staff members from the schedule
            var allStaff = new Dictionary<string, ScheduleExportModel>();
            foreach (var kvp in schedule)
            {
                var dateStr = kvp.Key;
                var staffList = kvp.Value;
                foreach (var staff in staffList)
                {
                    if (!allStaff.ContainsKey(staff.Name))
                    {
                        allStaff[staff.Name] = staff;
                    }
                }
            }

            // Add each staff member's data
            foreach (var staff in allStaff.Values.OrderBy(s => s.Name))
            {
                worksheet.Cell(rowIndex, 1).Value = staff.Name;
                worksheet.Cell(rowIndex, 2).Value = staff.Id;
                worksheet.Cell(rowIndex, 3).Value = staff.Group;

                // Fill daily shifts for this staff member
                for (int i = 0; i < dateHeaders.Count; i++)
                {
                    var dateStr = dateHeaders[i];
                    string shiftType = "";

                    // Look for the staff member's shift on this date
                    if (schedule.ContainsKey(dateStr))
                    {
                        var staffForDate = schedule[dateStr];
                        var staffForDay = staffForDate.FirstOrDefault(s => s.Name == staff.Name);
                        if (staffForDay != null)
                        {
                            // Use the actual shift type from the export model
                            shiftType = staffForDay.ShiftType;
                        }
                    }

                    worksheet.Cell(rowIndex, i + 4).Value = shiftType;
                }

                rowIndex++;
            }

            // 调整列宽
            worksheet.Columns().AdjustToContents();

            // Apply cell colors based on shift type
            for (int row = 2; row <= rowIndex - 1; row++)
            {
                for (int col = 4; col <= colIndex - 1; col++)
                {
                    var cellValue = worksheet.Cell(row, col).Value.ToString();
                    if (!string.IsNullOrEmpty(cellValue) && cellValue != " ")
                    {
                        // Find the corresponding staff and date to get the shift color
                        var staffName = worksheet.Cell(row, 1).Value.ToString();
                        var dateIndex = col - 4;
                        if (dateIndex < dateHeaders.Count)
                        {
                            var dateStr = dateHeaders[dateIndex];

                            // Look up the shift color for this staff member on this date
                            if (schedule.ContainsKey(dateStr))
                            {
                                var dateSchedule = schedule[dateStr];
                                var staffSchedule = dateSchedule.FirstOrDefault(s => s.Name == staffName && s.ShiftType == cellValue);
                                if (staffSchedule != null && !string.IsNullOrEmpty(staffSchedule.ShiftColor))
                                {
                                    try
                                    {
                                        var shiftColor = XLColor.FromHtml(staffSchedule.ShiftColor);
                                        worksheet.Cell(row, col).Style.Fill.BackgroundColor = shiftColor;
                                    }
                                    catch
                                    {
                                        // If color parsing fails, skip setting the background
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Get all unique shift types from the schedule for statistics
            var allShiftTypes = new HashSet<string>();
            foreach (var kvp in schedule)
            {
                foreach (var staff in kvp.Value)
                {
                    if (!string.IsNullOrEmpty(staff.ShiftType))
                    {
                        allShiftTypes.Add(staff.ShiftType);
                    }
                }
            }

            // Also include rest shift as a special shift type for statistics
            var rules = _dataService.LoadRules();
            if (!allShiftTypes.Contains(rules.RestShiftName))
            {
                allShiftTypes.Add(rules.RestShiftName);
            }

            // Add employee shift statistics to the RIGHT of the schedule data
            int empStatsStartCol = colIndex; // Start after the schedule data
            int empStatsStartRow = 1; // Start at the same row as schedule

            worksheet.Cell(empStatsStartRow, empStatsStartCol).Value = "员工班次统计";
            worksheet.Cell(empStatsStartRow, empStatsStartCol).Style.Font.Bold = true;
            worksheet.Cell(empStatsStartRow, empStatsStartCol).Style.Fill.BackgroundColor = XLColor.LightBlue;

            // Create headers for staff statistics (starting from the same relative column as dates)
            int currentCol = empStatsStartCol;
            int shiftTypeColStart = currentCol + 1; // Start shift type headers after the title

            foreach (var shiftType in allShiftTypes.OrderBy(s => s))
            {
                worksheet.Cell(empStatsStartRow, shiftTypeColStart).Value = shiftType;
                worksheet.Cell(empStatsStartRow, shiftTypeColStart).Style.Font.Bold = true;
                worksheet.Cell(empStatsStartRow, shiftTypeColStart).Style.Fill.BackgroundColor = XLColor.LightGray;
                shiftTypeColStart++;
            }

            // Add staff rows with count formulas
            int empStatsRow = empStatsStartRow + 1;
            var allStaffNames = allStaff.Values.Select(s => s.Name).OrderBy(n => n).ToList();

            foreach (var staffName in allStaffNames)
            {
                // Add staff name in first column of the employee stats section
                worksheet.Cell(empStatsRow, empStatsStartCol).Value = staffName;

                // For each shift type, add a count formula
                int shiftTypeCol = empStatsStartCol + 1;
                foreach (var shiftType in allShiftTypes.OrderBy(s => s))
                {
                    // We'll create a formula that counts this specific staff member's occurrences of this shift type
                    // For each date column (starting from column 4), check if the staff name matches and the shift type matches
                    var dateColumnsFormula = "";
                    for (int dateCol = 4; dateCol < colIndex; dateCol++)
                    {
                        var colLetter = GetExcelColumnName(dateCol);
                        if (dateColumnsFormula != "")
                            dateColumnsFormula += "+";
                        dateColumnsFormula += $"(A2:A{rowIndex-1}=\"{staffName}\")*({colLetter}2:{colLetter}{rowIndex-1}=\"{shiftType}\")";
                    }

                    // Check if this shift type is a rest shift ("休息" or "休")
                    var exportRules = _dataService.LoadRules();
                    var isRestShift = shiftType == exportRules.RestShiftName || shiftType == "休";

                    string formula;
                    if (isRestShift)
                    {
                        // For rest shifts, we also need to add half-day shifts multiplied by 0.5
                        string halfDayFormula = "";
                        var halfDayShifts = exportRules.HalfDayShifts.ToList();

                        foreach (var halfDayShift in halfDayShifts)
                        {
                            // Create formula for each half-day shift for this staff member
                            string halfDayShiftFormula = "";
                            for (int dateCol = 4; dateCol < colIndex; dateCol++)
                            {
                                var colLetter = GetExcelColumnName(dateCol);
                                if (halfDayShiftFormula != "")
                                    halfDayShiftFormula += "+";
                                halfDayShiftFormula += $"(A2:A{rowIndex-1}=\"{staffName}\")*({colLetter}2:{colLetter}{rowIndex-1}=\"{halfDayShift}\")";
                            }

                            if (halfDayFormula != "")
                                halfDayFormula += "+";
                            halfDayFormula += "0.5*SUMPRODUCT(" + halfDayShiftFormula + ")";
                        }

                        // Combine the main rest shift formula with the half-day shift contribution
                        if (!string.IsNullOrEmpty(halfDayFormula))
                        {
                            formula = "SUMPRODUCT(" + dateColumnsFormula + ")+(" + halfDayFormula + ")";
                        }
                        else
                        {
                            formula = "=SUMPRODUCT(" + dateColumnsFormula + ")";
                        }
                    }
                    else
                    {
                        formula = "=SUMPRODUCT(" + dateColumnsFormula + ")";
                    }

                    worksheet.Cell(empStatsRow, shiftTypeCol).FormulaA1 = formula;
                    shiftTypeCol++;
                }

                empStatsRow++;
            }

            // Add daily shift statistics section BELOW the schedule data (and employee stats if they're taller)
            int dailyStatsStartRow = Math.Max(rowIndex, empStatsRow); // Start after the taller section

            worksheet.Cell(dailyStatsStartRow, 1).Value = "每日班次统计";
            worksheet.Cell(dailyStatsStartRow, 1).Style.Font.Bold = true;
            worksheet.Cell(dailyStatsStartRow, 1).Style.Fill.BackgroundColor = XLColor.LightBlue;

            // Create headers for daily statistics with alignment to original date columns
            int dateHeaderCol = 4; // Start from the original date columns (D)
            foreach (var dateHeader in dateHeaders)
            {
                // Convert date format back to MM-dd format for display
                if (DateTime.TryParse(dateHeader, out DateTime date))
                {
                    worksheet.Cell(dailyStatsStartRow, dateHeaderCol).Value = date.ToString("MM-dd");
                }
                else
                {
                    worksheet.Cell(dailyStatsStartRow, dateHeaderCol).Value = dateHeader;
                }
                worksheet.Cell(dailyStatsStartRow, dateHeaderCol).Style.Font.Bold = true;
                worksheet.Cell(dailyStatsStartRow, dateHeaderCol).Style.Fill.BackgroundColor = XLColor.LightGray;
                dateHeaderCol++;
            }

            // Add shift type rows with count formulas
            int dailyStatsRow = dailyStatsStartRow + 1;
            foreach (var shiftType in allShiftTypes.OrderBy(s => s))
            {
                // Add shift type in first column
                worksheet.Cell(dailyStatsRow, 1).Value = shiftType;

                // For each date, add a count formula for this shift type
                int dateColIndex = 4; // Start from the original date columns (D)
                foreach (var dateStr in dateHeaders)
                {
                    // Count how many times this shift type appears in this specific date column
                    var colLetter = GetExcelColumnName(dateColIndex);
                    var formula = $"=COUNTIF({colLetter}2:{colLetter}{rowIndex-1},\"{shiftType}\")";

                    worksheet.Cell(dailyStatsRow, dateColIndex).FormulaA1 = formula;
                    dateColIndex++;
                }

                dailyStatsRow++;
            }

            workbook.SaveAs(filePath);
        }

        private string GetExcelColumnName(int columnNumber)
        {
            // Convert a column number to an Excel column name (1 -> A, 2 -> B, ..., 27 -> AA, etc.)
            string columnName = "";
            while (columnNumber > 0)
            {
                columnNumber--;
                int remainder = columnNumber % 26;
                columnName = (char)(65 + remainder) + columnName;
                columnNumber = (int)(columnNumber / 26);
            }
            return columnName;
        }

        public Dictionary<string, List<ScheduleExportModel>> ImportScheduleFromExcel(string filePath)
        {
            var schedule = new Dictionary<string, List<ScheduleExportModel>>();

            try
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1); // Use the first worksheet

                if (worksheet == null)
                    return schedule;

                int rowCount = worksheet.RowsUsed().Count();
                int colCount = worksheet.ColumnsUsed().Count();

                if (rowCount < 2 || colCount < 4)
                    return schedule; // Not enough data

                // Read headers (row 1) to identify dates
                var dateHeaders = new List<string>();
                int dateColumnEnd = 3; // Start looking for dates in column D (index 4)

                // We'll use a list to track dates as we parse them to handle cross-year scenarios
                var parsedDateHeaders = new List<DateTime>();

                for (int col = 4; col <= colCount; col++) // Start from column 4 (D)
                {
                    var cell = worksheet.Cell(1, col);
                    var headerValue = cell.Value.ToString();
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        // Convert MM-dd format to yyyy-MM-dd format
                        if (DateTime.TryParseExact(headerValue, "MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime date))
                        {
                            // We need to determine the year. For now, assume current year if date is reasonable
                            // However, for year-end dates (December) and early-year dates (January), we need to be more sophisticated
                            // to handle cross-year periods like Dec 22, 2025 to Jan 4, 2026 correctly
                            var currentDate = DateTime.Now;
                            var dateWithYear = new DateTime(currentDate.Year, date.Month, date.Day);

                            // Handle cross-year scenarios: if we have previously parsed dates,
                            // adjust years so that dates are consistent in the date range
                            // This approach assumes that the schedule period is typically within a short period
                            if (parsedDateHeaders.Count > 0)
                            {
                                // If we already have dates, try to maintain consistency with the existing date range
                                var firstDate = parsedDateHeaders[0];
                                var potentialDate = new DateTime(firstDate.Year, date.Month, date.Day);
                                var potentialDateNextYear = new DateTime(firstDate.Year + 1, date.Month, date.Day);
                                var potentialDatePrevYear = new DateTime(firstDate.Year - 1, date.Month, date.Day);

                                // Choose the date that is closest to the existing date range
                                var closestDate = potentialDate;
                                var minDistance = Math.Abs((potentialDate - firstDate).Days);

                                var nextYearDistance = Math.Abs((potentialDateNextYear - firstDate).Days);
                                if (nextYearDistance < minDistance)
                                {
                                    minDistance = nextYearDistance;
                                    closestDate = potentialDateNextYear;
                                }

                                var prevYearDistance = Math.Abs((potentialDatePrevYear - firstDate).Days);
                                if (prevYearDistance < minDistance)
                                {
                                    minDistance = prevYearDistance;
                                    closestDate = potentialDatePrevYear;
                                }

                                dateWithYear = closestDate;
                            }
                            else
                            {
                                // For the first date, use current year but check if it might be a cross-year scenario
                                dateWithYear = new DateTime(currentDate.Year, date.Month, date.Day);

                                // If it's January and we're still early in the year, this date might be from last year
                                if (date.Month == 1 && currentDate.Month >= 10) // October, November, December
                                {
                                    dateWithYear = new DateTime(currentDate.Year - 1, date.Month, date.Day);
                                }
                                // If it's December and we're early in the year, this date might be from next year
                                else if (date.Month == 12 && currentDate.Month <= 3) // January, February, March
                                {
                                    dateWithYear = new DateTime(currentDate.Year + 1, date.Month, date.Day);
                                }
                            }

                            dateHeaders.Add(dateWithYear.ToString("yyyy-MM-dd"));
                            parsedDateHeaders.Add(dateWithYear); // Track for cross-year logic
                            dateColumnEnd = col; // Track the end of date columns
                        }
                        else
                        {
                            // If this is not a date format, we've reached the statistics section
                            break; // Stop reading date headers here
                        }
                    }
                    else
                    {
                        dateHeaders.Add(string.Empty);
                    }
                }

                // Process data rows (start from row 2) until we reach the statistics section
                for (int row = 2; row <= rowCount; row++)
                {
                    var nameCell = worksheet.Cell(row, 1);
                    var name = nameCell.Value.ToString();
                    var idCell = worksheet.Cell(row, 2);
                    var id = idCell.Value.ToString();
                    var groupCell = worksheet.Cell(row, 3);
                    var group = groupCell.Value.ToString();

                    if (string.IsNullOrEmpty(name))
                        continue; // Skip empty rows

                    // Check if we've reached the "员工班次统计" or "每日班次统计" section by checking if the name cell matches
                    if (name.Equals("员工班次统计", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("每日班次统计", StringComparison.OrdinalIgnoreCase))
                        break; // Stop processing at statistics section

                    // Additional check: if we're in the statistics section, the cells might contain numbers instead of staff names
                    // Skip rows that appear to be statistics by checking if the second column is a number
                    //if (double.TryParse(id, out _))
                    //{
                        // This might be a statistics row, check if this is likely a staff ID or a count
                        // If it's just a count value without a proper name, skip it
                        //continue;
                    //}

                    // Process each date column
                    for (int colIndex = 0; colIndex < dateHeaders.Count; colIndex++)
                    {
                        var dateStr = dateHeaders[colIndex];
                        if (string.IsNullOrEmpty(dateStr))
                            continue;

                        var shiftCell = worksheet.Cell(row, colIndex + 4);
                        var shiftValue = shiftCell.Value.ToString();

                        // Ensure the date key exists in the dictionary
                        if (!schedule.ContainsKey(dateStr))
                        {
                            schedule[dateStr] = new List<ScheduleExportModel>();
                        }

                        // Add the schedule entry
                        schedule[dateStr].Add(new ScheduleExportModel
                        {
                            Name = name,
                            Id = id,
                            Group = group,
                            ShiftType = shiftValue,
                            ShiftColor = "" // Don't import color from Excel, use system configuration instead
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"导入Excel文件失败: {ex.Message}");
                return new Dictionary<string, List<ScheduleExportModel>>(); // Return empty on error
            }

            return schedule;
        }
    }
}