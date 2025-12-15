using SchedulingApp.Models;

namespace SchedulingApp.Services.Interfaces
{
    public interface IStaffExcelService
    {
        void ExportStaffTemplate(string filePath);
        List<StaffModel> ImportStaffFromExcel(string filePath);
    }
}