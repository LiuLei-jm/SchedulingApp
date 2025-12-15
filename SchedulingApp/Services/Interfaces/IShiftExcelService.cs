using SchedulingApp.Models;

namespace SchedulingApp.Services.Interfaces
{
    public interface IShiftExcelService
    {
        void ExportShiftToTemplate(string filePath);
        List<ShiftModel> ImportShiftFromExcel(string filePath);
    }
}
