using ClosedXML.Excel;
using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;

namespace SchedulingApp.Services.Implementations
{
    public class ExcelExportService : IExcelExportService
    {
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

            workbook.SaveAs(filePath);
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
                            var currentDate = DateTime.Now;
                            var dateWithYear = new DateTime(currentDate.Year, date.Month, date.Day);

                            dateHeaders.Add(dateWithYear.ToString("yyyy-MM-dd"));
                        }
                        else
                        {
                            dateHeaders.Add(headerValue); // Keep as is if not a valid date
                        }
                    }
                    else
                    {
                        dateHeaders.Add(string.Empty);
                    }
                }

                // Process data rows (start from row 2)
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
                            ShiftType = shiftValue
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