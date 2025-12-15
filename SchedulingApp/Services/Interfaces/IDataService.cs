using SchedulingApp.Models;

namespace SchedulingApp.Services.Interfaces
{
    public interface IDataService
    {
        List<StaffModel> LoadStaff();
        void SaveStaff(List<StaffModel> staff);
        List<ShiftModel> LoadShifts();
        void SaveShifts(List<ShiftModel> shifts);
        RulesModel LoadRules();
        void SaveRules(RulesModel rules);
        string ScheduleFile { get; }
        List<ScheduleItemModel> LoadSchedule();
        void SaveSchedule(List<ScheduleItemModel> schedule);
    }
}