using System.Collections.ObjectModel;

namespace SchedulingApp.Models
{
    public class RulesModel
    {
        public int MaxConsecutiveDays { get; set; } = 5;
        public ObservableCollection<string> CustomHolidays { get; set; } = [];
        public ObservableCollection<string> HalfDayShifts { get; set; } = [];
        public int TotalRestDays { get; set; } = 4;

        // Old properties for backward compatibility
        public ObservableCollection<ShiftRequirementModel> Weekday { get; set; } = new ObservableCollection<ShiftRequirementModel>();
        public ObservableCollection<ShiftRequirementModel> Holiday { get; set; } = new ObservableCollection<ShiftRequirementModel>();

        // New property for multiple scheduling rules
        public ObservableCollection<SchedulingRuleModel> SchedulingRules { get; set; } = [];
    }

    public class ShiftFilter
    {
        public List<string> ExcludedPersons { get; set; } = [];
        public List<string> ExcludedGroups { get; set; } = [];
    }
}