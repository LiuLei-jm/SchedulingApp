using ClosedXML.Excel;
using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;

namespace SchedulingApp.Services.Implementations
{
    public class ShiftExcelService : IShiftExcelService
    {
        public void ExportShiftToTemplate(string filePath)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("班次信息模板");

            worksheet.Cell(1, 1).Value = "班次名称";
            worksheet.Cell(1, 2).Value = "开始时间";
            worksheet.Cell(1, 3).Value = "结束时间";
            worksheet.Cell(1, 4).Value = "颜色";

            var headerRange = worksheet.Range(1, 1, 1, 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            worksheet.Cell(2, 1).Value = "白班";
            worksheet.Cell(2, 2).Value = "08:00";
            worksheet.Cell(2, 3).Value = "17:00";
            worksheet.Cell(2, 4).Value = "#FFD700";

            worksheet.Cell(3, 1).Value = "晚班";
            worksheet.Cell(3, 2).Value = "16:00";
            worksheet.Cell(3, 3).Value = "24:00";
            worksheet.Cell(3, 4).Value = "#87CEFA";

            worksheet.Columns(1, 4).AdjustToContents();

            workbook.SaveAs(filePath);
        }

        public List<ShiftModel> ImportShiftFromExcel(string filePath)
        {
            var shiftList = new List<ShiftModel>();

            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);

            var headerRow = worksheet.Row(1);
            var header1 = worksheet.Cell(1, 1).Value.ToString().Trim();
            var header2 = worksheet.Cell(1, 2).Value.ToString().Trim();
            var header3 = worksheet.Cell(1, 3).Value.ToString().Trim();
            var header4 = worksheet.Cell(1, 4).Value.ToString().Trim();

            if (header1 != "班次名称" && header1 != "Name")
                throw new Exception("Excel文件表头格式不正确!");
            if (header2 != "开始时间" && header2 != "Start Time")
                throw new Exception("Excel文件表头格式不正确!");
            if (header3 != "结束时间" && header3 != "End Time")
                throw new Exception("Excel文件表头格式不正确!");
            if (header4 != "颜色" && header4 != "Color")
                throw new Exception("Excel文件表头格式不正确!");

            var currentRow = 2;
            while (!string.IsNullOrWhiteSpace(worksheet.Cell(currentRow, 1).Value.ToString()))
            {
                var shiftName = worksheet.Cell(currentRow, 1).Value.ToString().Trim();
                var startTime = worksheet.Cell(currentRow, 2).Value.ToString().Trim();
                var endTime = worksheet.Cell(currentRow, 3).Value.ToString().Trim();
                var color = worksheet.Cell(currentRow, 4).Value.ToString().Trim();

                shiftList.Add(new ShiftModel
                {
                    ShiftName = shiftName,
                    StartTime = startTime,
                    EndTime = endTime,
                    Color = string.IsNullOrWhiteSpace(color) ? "#FFFFFF" : color
                });
                currentRow++;
            }

            return shiftList;
        }
    }
}
