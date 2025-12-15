using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace SchedulingApp.Models
{
    public partial class ShiftRequirementModel : ObservableObject, IDataErrorInfo
    {
        [ObservableProperty]
        private string _shiftName = string.Empty;

        [ObservableProperty]
        private int _requiredCount;

        [ObservableProperty]
        private int? _priority; // null means lowest priority (treated as highest number when sorted)

        public string Error => string.Empty;

        public string this[string columnName]
        {
            get
            {
                if (columnName == nameof(ShiftName))
                {
                    if (string.IsNullOrWhiteSpace(ShiftName))
                        return "班次名称不能为空";
                }
                else if (columnName == nameof(RequiredCount))
                {
                    if (RequiredCount < 0)
                        return "需求数量不能为负数";
                }
                else if (columnName == nameof(Priority))
                {
                    if (Priority.HasValue && Priority < 0)
                        return "优先级不能为负数";
                }
                return string.Empty;
            }
        }
    }
}