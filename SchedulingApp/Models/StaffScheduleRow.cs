using CommunityToolkit.Mvvm.ComponentModel;

namespace SchedulingApp.Models
{
    public class StaffScheduleRow : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;

        // Dictionary to hold shifts for each date
        public Dictionary<string, ScheduleShiftInfo> DateShifts { get; set; } = new Dictionary<string, ScheduleShiftInfo>();
    }

    public class ScheduleShiftInfo
    {
        public string ShiftName { get; set; } = string.Empty;
        public string ShiftColor { get; set; } = string.Empty;
    }
}