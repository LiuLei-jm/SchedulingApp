using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SchedulingApp.Models
{
    public partial class SchedulingRuleModel : ObservableObject, IDataErrorInfo
    {
        [ObservableProperty]
        private string _ruleName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ShiftRequirementModel> _weekdayShifts = [];

        [ObservableProperty]
        private ObservableCollection<ShiftRequirementModel> _holidayShifts = [];

        [ObservableProperty]
        private ObservableCollection<string> _applicableStaff = [];

        public string Error => string.Empty;

        public string this[string columnName]
        {
            get
            {
                if (columnName == nameof(RuleName))
                {
                    if (string.IsNullOrWhiteSpace(RuleName))
                        return "规则名称不能为空";
                }
                return string.Empty;
            }
        }
    }
}