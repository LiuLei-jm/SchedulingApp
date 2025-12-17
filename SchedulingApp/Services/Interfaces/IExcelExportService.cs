using SchedulingApp.Models;

namespace SchedulingApp.Services.Interfaces
{
    public interface IExcelExportService
    {
        void ExportScheduleToExcel(Dictionary<string, List<ScheduleExportModel>> schedule, string filePath, DateTime startDate, DateTime endDate);
        Dictionary<string, List<ScheduleExportModel>> ImportScheduleFromExcel(string filePath);
    }
}