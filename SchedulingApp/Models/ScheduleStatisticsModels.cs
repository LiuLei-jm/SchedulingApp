using CommunityToolkit.Mvvm.ComponentModel;

namespace SchedulingApp.Models
{
    public class DailyShiftStats
    {
        public string ShiftType { get; set; } = string.Empty;
        public Dictionary<string, double> DateCounts { get; set; } = new Dictionary<string, double>();
    }

    public class StaffShiftStats
    {
        public string StaffName { get; set; } = string.Empty;
        public Dictionary<string, double> ShiftCounts { get; set; } = new Dictionary<string, double>();
    }

    // View model for binding to UI - contains all the statistics data
    public class ScheduleStatisticsViewModel : ObservableObject
    {
        public List<DailyShiftStats> DailyShiftCounts { get; set; } = new List<DailyShiftStats>();
        public List<DailyShiftStats> DailyTotals { get; set; } = new List<DailyShiftStats>(); // For Total Working and Total
        public List<StaffShiftStats> StaffShiftCounts { get; set; } = new List<StaffShiftStats>();
    }
}