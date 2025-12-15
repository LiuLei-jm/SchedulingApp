using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;
using System.Collections.ObjectModel;

namespace SchedulingApp.ViewModels
{
    public partial class RulesViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        public RulesViewModel(IDataService dataService)
        {
            _dataService = dataService;

            // Load all available data first
            LoadAvailableData();
        }

        // 数据属性
        [ObservableProperty]
        private RulesModel _rules = new RulesModel();

        [ObservableProperty]
        private ObservableCollection<string> _selectedCustomHolidays = [];

        [ObservableProperty]
        private DateTime? _newCustomHolidayDate = null;

        [ObservableProperty]
        private ObservableCollection<string> _selectedHalfDayShifts = [];

        [ObservableProperty]
        private string? _newHalfDayShift = null;

        // New properties for multiple rules
        [ObservableProperty]
        private ObservableCollection<SchedulingRuleModel> _schedulingRules = [];

        [ObservableProperty]
        private SchedulingRuleModel? _selectedRule;

        [ObservableProperty]
        private ObservableCollection<StaffModel> _availableStaff = [];

        [ObservableProperty]
        private ObservableCollection<ShiftModel> _availableShifts = [];

        // 命令
        [RelayCommand]
        private void AddWeekdayRequirement()
        {
            Rules.Weekday.Add(new ShiftRequirementModel { ShiftName = "新班次", RequiredCount = 0 });
        }

        [RelayCommand]
        private void RemoveWeekdayRequirement(object parameter)
        {
            if (parameter is ShiftRequirementModel requirement && Rules.Weekday.Contains(requirement))
            {
                Rules.Weekday.Remove(requirement);
            }
        }

        [RelayCommand]
        private void AddHolidayRequirement()
        {
            Rules.Holiday.Add(new ShiftRequirementModel { ShiftName = "新班次", RequiredCount = 0 });
        }

        [RelayCommand]
        private void RemoveHolidayRequirement(object parameter)
        {
            if (parameter is ShiftRequirementModel requirement && Rules.Holiday.Contains(requirement))
            {
                Rules.Holiday.Remove(requirement);
            }
        }

        [RelayCommand]
        private void SaveRules()
        {
            try
            {
                _dataService.SaveRules(Rules);
                Console.WriteLine("排班规则已保存");

                // Also update the scheduling rules in the loaded data
                Rules.SchedulingRules.Clear();
                foreach (var rule in SchedulingRules)
                {
                    Rules.SchedulingRules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存排班规则失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void LoadRules()
        {
            try
            {
                Rules = _dataService.LoadRules();
                Console.WriteLine("排班规则已加载");

                // Update the scheduling rules collection to match the loaded data
                SchedulingRules.Clear();
                foreach (var rule in Rules.SchedulingRules)
                {
                    SchedulingRules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载排班规则失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void AddCustomHoliday()
        {
            if (NewCustomHolidayDate.HasValue)
            {
                string formattedDate = NewCustomHolidayDate.Value.ToString("yyyy-MM-dd");

                if (!Rules.CustomHolidays.Contains(formattedDate))
                {
                    Rules.CustomHolidays.Add(formattedDate);
                    _dataService.SaveRules(Rules);
                    NewCustomHolidayDate = null; // 清空日期选择器
                }
                else
                {
                    Console.WriteLine($"节假日 {formattedDate} 已存在");
                }
            }
        }

        [RelayCommand]
        private void RemoveCustomHoliday(object parameter)
        {
            try
            {
                var holidaysToRemove = new List<string>();

                // Check if parameter is a single string (from × button) or IList<object> (from DataGrid)
                if (parameter is string singleHoliday)
                {
                    // Single item from the × button
                    holidaysToRemove.Add(singleHoliday);
                }
                else
                {
                    // Multiple items from the old DataGrid
                    var selectedHolidays = parameter as IList<object>;

                    if (selectedHolidays == null || selectedHolidays.Count == 0)
                    {
                        Growl.InfoGlobal("请先选择要删除的自定义节假日");
                        return;
                    }

                    foreach (var holiday in selectedHolidays)
                    {
                        if (holiday is string holidayStr)
                        {
                            holidaysToRemove.Add(holidayStr);
                        }
                    }

                    if (holidaysToRemove.Count == 0)
                    {
                        Growl.InfoGlobal("请先选择要删除的自定义节假日");
                        return;
                    }
                }

                string message;
                if (holidaysToRemove.Count == 1)
                {
                    message = $"确定要删除: {holidaysToRemove[0]}吗？";
                }
                else
                {
                    message = $"确定要删除选中的 {holidaysToRemove.Count} 个自定义节假日吗？";
                }

                var result = HandyControl.Controls.MessageBox.Ask("确定要删除吗？", "确认删除");
                if (result == System.Windows.MessageBoxResult.OK)
                {
                    foreach (var day in holidaysToRemove)
                    {
                        Rules.CustomHolidays.Remove(day);
                    }
                    _dataService.SaveRules(Rules);
                    Growl.InfoGlobal("成功删除自定义节假日");
                }
            }
            catch (Exception ex)
            {
                Growl.WarningGlobal($"删除自定义节假日失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ClearCustomHolidays()
        {
            try
            {
                if (Rules.CustomHolidays.Count == 0)
                {
                    Growl.InfoGlobal("没有自定义节假日可清空");
                    return;
                }

                string message = $"确定要清空所有 {Rules.CustomHolidays.Count} 个自定义节假日吗？";

                var result = HandyControl.Controls.MessageBox.Ask("确定要清空吗？", "确认清空");
                if (result == System.Windows.MessageBoxResult.OK)
                {
                    Rules.CustomHolidays.Clear();
                    _dataService.SaveRules(Rules);
                    Growl.InfoGlobal("成功清空所有自定义节假日");
                }
            }
            catch (Exception ex)
            {
                Growl.WarningGlobal($"清空自定义节假日失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void AddHalfDayShift()
        {
            if (!string.IsNullOrEmpty(NewHalfDayShift))
            {
                // Check if the selected shift exists in the available shifts
                var availableShift = AvailableShifts.FirstOrDefault(s => s.ShiftName == NewHalfDayShift);
                if (availableShift != null)
                {
                    if (!Rules.HalfDayShifts.Contains(NewHalfDayShift))
                    {
                        Rules.HalfDayShifts.Add(NewHalfDayShift);
                        _dataService.SaveRules(Rules);
                        NewHalfDayShift = null; // Clear the selection
                    }
                    else
                    {
                        Console.WriteLine($"半天班 {NewHalfDayShift} 已存在");
                    }
                }
                else
                {
                    Console.WriteLine($"所选班次 {NewHalfDayShift} 不存在");
                }
            }
        }

        [RelayCommand]
        private void RemoveHalfDayShift(object parameter)
        {
            try
            {
                var shiftsToRemove = new List<string>();

                // Check if parameter is a single string (from × button) or IList<object> (from DataGrid)
                if (parameter is string singleShift)
                {
                    // Single item from the × button
                    shiftsToRemove.Add(singleShift);
                }
                else
                {
                    // Multiple items from the old DataGrid
                    var selectedShifts = parameter as IList<object>;

                    if (selectedShifts == null || selectedShifts.Count == 0)
                    {
                        Growl.InfoGlobal("请先选择要删除的半天班");
                        return;
                    }

                    foreach (var shift in selectedShifts)
                    {
                        if (shift is string shiftStr)
                        {
                            shiftsToRemove.Add(shiftStr);
                        }
                    }

                    if (shiftsToRemove.Count == 0)
                    {
                        Growl.InfoGlobal("请先选择要删除的半天班");
                        return;
                    }
                }

                string message;
                if (shiftsToRemove.Count == 1)
                {
                    message = $"确定要删除: {shiftsToRemove[0]}吗？";
                }
                else
                {
                    message = $"确定要删除选中的 {shiftsToRemove.Count} 个半天班吗？";
                }

                var result = HandyControl.Controls.MessageBox.Ask("确定要删除吗？", "确认删除");
                if (result == System.Windows.MessageBoxResult.OK)
                {
                    foreach (var shift in shiftsToRemove)
                    {
                        Rules.HalfDayShifts.Remove(shift);
                    }
                    _dataService.SaveRules(Rules);
                    Growl.InfoGlobal("成功删除半天班");
                }
            }
            catch (Exception ex)
            {
                Growl.WarningGlobal($"删除半天班失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ClearHalfDayShifts()
        {
            try
            {
                if (Rules.HalfDayShifts.Count == 0)
                {
                    Growl.InfoGlobal("没有半天班可清空");
                    return;
                }

                string message = $"确定要清空所有 {Rules.HalfDayShifts.Count} 个半天班吗？";

                var result = HandyControl.Controls.MessageBox.Ask("确定要清空吗？", "确认清空");
                if (result == System.Windows.MessageBoxResult.OK)
                {
                    Rules.HalfDayShifts.Clear();
                    _dataService.SaveRules(Rules);
                    Growl.InfoGlobal("成功清空所有半天班");
                }
            }
            catch (Exception ex)
            {
                Growl.WarningGlobal($"清空半天班失败: {ex.Message}");
            }
        }

        // New commands for managing multiple rules
        [RelayCommand]
        private void AddSchedulingRule()
        {
            var newRule = new SchedulingRuleModel
            {
                RuleName = $"规则{SchedulingRules.Count + 1}",
                WeekdayShifts = [],
                HolidayShifts = [],
                ApplicableStaff = []
            };
            SchedulingRules.Add(newRule);
            SelectedRule = newRule;
        }

        [RelayCommand]
        private void RemoveSchedulingRule(object parameter)
        {
            if (parameter is SchedulingRuleModel rule && SchedulingRules.Contains(rule))
            {
                SchedulingRules.Remove(rule);
            }
        }

        [RelayCommand]
        private void AddWeekdayShiftToRule()
        {
            if (SelectedRule != null)
            {
                // For now, add a new shift requirement with an empty shift name
                var newRequirement = new ShiftRequirementModel
                {
                    ShiftName = "", // Will be selected from available shifts
                    RequiredCount = 0
                };
                SelectedRule.WeekdayShifts.Add(newRequirement);
            }
        }

        [RelayCommand]
        private void RemoveWeekdayShiftFromRule(object parameter)
        {
            if (SelectedRule != null && parameter is ShiftRequirementModel shiftReq)
            {
                SelectedRule.WeekdayShifts.Remove(shiftReq);
            }
        }

        [RelayCommand]
        private void AddHolidayShiftToRule()
        {
            if (SelectedRule != null)
            {
                var newRequirement = new ShiftRequirementModel
                {
                    ShiftName = "", // Will be selected from available shifts
                    RequiredCount = 0
                };
                SelectedRule.HolidayShifts.Add(newRequirement);
            }
        }

        [RelayCommand]
        private void RemoveHolidayShiftFromRule(object parameter)
        {
            if (SelectedRule != null && parameter is ShiftRequirementModel shiftReq)
            {
                SelectedRule.HolidayShifts.Remove(shiftReq);
            }
        }

        [RelayCommand]
        private void AddStaffToRule(object parameter)
        {
            var selectedStaffList = parameter as System.Collections.IList;

            if (selectedStaffList == null || selectedStaffList.Count == 0)
            {
                Growl.InfoGlobal("请先选择要添加的员工！");
                return;
            }

            var staffToRule = new List<StaffModel>();
            foreach (var item in selectedStaffList)
            {
                if (item is StaffModel staff)
                {
                    staffToRule.Add(staff);
                }
            }

            if (staffToRule.Count == 0)
            {
                Growl.InfoGlobal("请先选择要添加的员工！");
                return;
            }

            foreach (var staffObj in staffToRule)
            {
                if (!SelectedRule.ApplicableStaff.Contains(staffObj.Name))
                {
                    // Validation: Ensure each staff member is only in one rule
                    if (IsStaffAlreadyInAnotherRule(staffObj.Name))
                    {
                        Growl.WarningGlobal($"员工 {staffObj.Name} 已经在其他规则中，不能重复添加");
                        return;
                    }
                    SelectedRule.ApplicableStaff.Add(staffObj.Name);
                }
            }
            UpdateAvailableStaffForRule(SelectedRule); // Refresh available staff

        }

        [RelayCommand]
        private void RemoveStaffFromRule(object parameter)
        {
            if (SelectedRule != null && parameter is string staffName)
            {
                SelectedRule.ApplicableStaff.Remove(staffName);
                UpdateAvailableStaffForRule(SelectedRule); // Refresh available staff
            }
        }

        // Validation methods
        private bool IsStaffAlreadyInAnotherRule(string staffName)
        {
            foreach (var rule in SchedulingRules)
            {
                if (rule != SelectedRule && rule.ApplicableStaff.Contains(staffName))
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateAvailableStaffForRule(SchedulingRuleModel currentRule)
        {
            var usedStaff = new HashSet<string>();
            foreach (var rule in SchedulingRules)
            {
                foreach (var staffName in rule.ApplicableStaff)
                {
                    if (rule != currentRule)
                    {
                        usedStaff.Add(staffName);
                    }
                }
            }

            // Refresh the AvailableStaff collection to show only available staff for this rule
            var allStaff = _dataService.LoadStaff();
            AvailableStaff.Clear();
            foreach (var staff in allStaff)
            {
                if (!usedStaff.Contains(staff.Name))
                {
                    // Only add staff that isn't already in the current rule
                    if (!currentRule.ApplicableStaff.Contains(staff.Name))
                    {
                        AvailableStaff.Add(staff);
                    }
                }
            }
        }

        private void LoadAvailableData()
        {
            // Load all available shifts and staff from DataService
            var allShifts = _dataService.LoadShifts();
            AvailableShifts.Clear();
            foreach (var shift in allShifts)
            {
                AvailableShifts.Add(shift);
            }

            var allStaff = _dataService.LoadStaff();
            AvailableStaff.Clear();
            foreach (var staff in allStaff)
            {
                AvailableStaff.Add(staff);
            }
        }

        partial void OnRulesChanged(RulesModel value)
        {
            // Update the scheduling rules collection to match the loaded data
            SchedulingRules.Clear();
            foreach (var rule in value.SchedulingRules)
            {
                SchedulingRules.Add(rule);
            }
        }

        partial void OnSelectedRuleChanged(SchedulingRuleModel? value)
        {
            if (value != null)
            {
                UpdateAvailableStaffForRule(value);
            }
        }


    }
}