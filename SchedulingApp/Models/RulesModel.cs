using System.Collections.ObjectModel;

namespace SchedulingApp.Models
{
    public class RulesModel
    {
        public ObservableCollection<ShiftRequirementModel> Weekday { get; set; } = [];
        public ObservableCollection<ShiftRequirementModel> Holiday { get; set; } = [];
        public int MaxConsecutiveDays { get; set; } = 5;
        public ObservableCollection<string> CustomHolidays { get; set; } = [];
        public ObservableCollection<string> HalfDayShifts { get; set; } = [];
        public int TotalRestDays { get; set; } = 4;
        public List<string> SpecialPersons { get; set; } = [];
        public List<string> VipGroups { get; set; } = [];
        public Dictionary<string, VipSpecialPersonsGroup> VipSpecialPersons { get; set; } = [];
        public Dictionary<string, ShiftFilter> ShiftFilters { get; set; } = [];

        // New property for multiple scheduling rules
        public ObservableCollection<SchedulingRuleModel> SchedulingRules { get; set; } = [];
    }

    public class VipSpecialPersonsGroup
    {
        public List<string> Persons { get; set; } = [];
    }

    public class ShiftFilter
    {
        public List<string> ExcludedPersons { get; set; } = [];
        public List<string> ExcludedGroups { get; set; } = [];
    }
}