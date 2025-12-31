using SchedulingApp.Models;
using SchedulingApp.Services.Implementations;

namespace SchedulingApp.Services.Interfaces
{
    public interface ISchedulingService
    {
        (Dictionary<string, List<StaffModel>> schedule, string errorMessage) GenerateSchedule(
            List<StaffModel> staff,
            List<ShiftModel> shifts,
            RulesModel rules,
            int month,
            int year);

        Dictionary<string, ScheduleDataModel> GeneratePersonBasedSchedule(
            List<StaffModel> staff,
            List<ShiftModel> shifts,
            RulesModel rules,
            DateTime startDate,
            DateTime endDate);
    }
}