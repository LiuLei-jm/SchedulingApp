using ClosedXML.Excel;
using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;

namespace SchedulingApp.Services.Implementations
{
    public class StaffExcelService : IStaffExcelService
    {
        public void ExportStaffTemplate(string filePath)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("人员信息模板");

            // 设置表头
            worksheet.Cell(1, 1).Value = "姓名";
            worksheet.Cell(1, 2).Value = "工号";
            worksheet.Cell(1, 3).Value = "组别";

            // 设置表头样式
            var headerRange = worksheet.Range(1, 1, 1, 3);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            // 添加一些示例行
            worksheet.Cell(2, 1).Value = "张三";
            worksheet.Cell(2, 2).Value = "001";
            worksheet.Cell(2, 3).Value = "A组";

            worksheet.Cell(3, 1).Value = "李四";
            worksheet.Cell(3, 2).Value = "002";
            worksheet.Cell(3, 3).Value = "B组";

            // 调整列宽
            worksheet.Columns(1, 3).AdjustToContents();

            workbook.SaveAs(filePath);
        }

        public List<StaffModel> ImportStaffFromExcel(string filePath)
        {
            var staffList = new List<StaffModel>();

            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1); // 默认取第一个工作表

            // 验证表头
            var headerRow = worksheet.Row(1);
            var header1 = worksheet.Cell(1, 1).Value.ToString().Trim();
            var header2 = worksheet.Cell(1, 2).Value.ToString().Trim();
            var header3 = worksheet.Cell(1, 3).Value.ToString().Trim();

            if (header1 != "姓名" && header1 != "Name")
                throw new Exception("Excel文件表头格式不正确");

            if (header2 != "工号" && header2 != "ID")
                throw new Exception("Excel文件表头格式不正确");

            if (header3 != "组别" && header3 != "Group")
                throw new Exception("Excel文件表头格式不正确");

            // 从第2行开始读取数据
            var currentRow = 2;
            while (!string.IsNullOrWhiteSpace(worksheet.Cell(currentRow, 1).Value.ToString()))
            {
                var name = worksheet.Cell(currentRow, 1).Value.ToString().Trim();
                var id = worksheet.Cell(currentRow, 2).Value.ToString().Trim();
                var group = worksheet.Cell(currentRow, 3).Value.ToString().Trim();

                // 验证必要字段不为空
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id))
                {
                    throw new Exception($"第{currentRow}行数据不完整，姓名和工号不能为空");
                }

                staffList.Add(new StaffModel
                {
                    Name = name,
                    Id = id,
                    Group = string.IsNullOrWhiteSpace(group) ? "" : group
                });

                currentRow++;
            }

            return staffList;
        }
    }
}