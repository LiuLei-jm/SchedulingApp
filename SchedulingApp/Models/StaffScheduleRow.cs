using CommunityToolkit.Mvvm.ComponentModel;

namespace SchedulingApp.Models
{
    public class StaffScheduleRow : ObservableObject
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Group { get; set; }

        // Dictionary to hold shifts for each date
        public Dictionary<string, ScheduleShiftInfo> DateShifts { get; set; } = new Dictionary<string, ScheduleShiftInfo>();
    }

    public class ScheduleShiftInfo
    {
        public string ShiftName { get; set; }
        public string ShiftColor { get; set; }
    }
}