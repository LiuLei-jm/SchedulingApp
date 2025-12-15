using ClosedXML.Excel;
using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;
using System.Text.Json;

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
    }
}