using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Windows;

namespace SchedulingApp.ViewModels
{
    public partial class ScheduleViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly ISchedulingService _schedulingService;
        private readonly IExcelExportService _excelExportService;

        public ScheduleViewModel(
            IDataService dataService,
            ISchedulingService schedulingService,
            IExcelExportService excelExportService
        )
        {
            _dataService = dataService;
            _schedulingService = schedulingService;
            _excelExportService = excelExportService;
            // Load rules when needed for schedule generation
        }

        // 排班相关属性
        [ObservableProperty]
        private DateTime _startDate = DateTime.Today;

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today.AddDays(13);

        [ObservableProperty]
        private ObservableCollection<ScheduleItemModel> _scheduleItems =
            new ObservableCollection<ScheduleItemModel>();

        // New properties for the updated table format
        private Dictionary<string, ScheduleDataModel>? _currentScheduleData;
        public ObservableCollection<StaffScheduleRow> StaffWithSchedules { get; set; } =
            new ObservableCollection<StaffScheduleRow>();
        public ObservableCollection<string> DateHeaders { get; set; } =
            new ObservableCollection<string>();

        // Properties for statistics
        public ObservableCollection<DailyShiftStats> DailyStatistics { get; set; } =
            new ObservableCollection<DailyShiftStats>();
        public ObservableCollection<StaffShiftStats> StaffStatistics { get; set; } =
            new ObservableCollection<StaffShiftStats>();

        [ObservableProperty]
        private Visibility _dailyStatisticsVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility _staffStatisticsVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private bool _canExport = false;

        [ObservableProperty]
        private bool _canUpload = true;

        // 命令
        [RelayCommand]
        private void GenerateSchedule()
        {
            try
            {
                var staffList = StaffData.ToList();
                var shiftList = _dataService.LoadShifts();

                var rules = _dataService.LoadRules();
                // Auto-populate weekends if CustomHolidays is empty
                FillWeekendsIfEmpty(rules, StartDate, EndDate);
                // Use the new person-based schedule generation method
                _currentScheduleData = _schedulingService.GeneratePersonBasedSchedule(
                    staffList,
                    shiftList,
                    rules,
                    StartDate,
                    EndDate
                );

                // Clear the old format data
                ScheduleItems.Clear();

                // Generate the new table format
                StaffWithSchedules.Clear();
                DateHeaders.Clear();

                // Add date headers
                var currentDate = StartDate;
                while (currentDate <= EndDate)
                {
                    DateHeaders.Add(currentDate.ToString("MM-dd"));
                    currentDate = currentDate.AddDays(1);
                }

                // Determine which staff should be included in the schedule
                // Only include staff who are mentioned in scheduling rules
                var staffToInclude = new List<StaffModel>();

                // Check if any scheduling rules exist
                if (rules.SchedulingRules != null && rules.SchedulingRules.Any())
                {
                    // Include only staff that are in applicable scheduling rules
                    var applicableStaffNames = new HashSet<string>();
                    foreach (var rule in rules.SchedulingRules)
                    {
                        if (rule.ApplicableStaff != null && rule.ApplicableStaff.Any())
                        {
                            foreach (var staffName in rule.ApplicableStaff)
                            {
                                applicableStaffNames.Add(staffName);
                            }
                        }
                        else
                        {
                            // If no specific staff are mentioned, include all staff
                            foreach (var staff in staffList)
                            {
                                applicableStaffNames.Add(staff.Name);
                            }
                        }
                    }

                    staffToInclude = staffList
                        .Where(s => applicableStaffNames.Contains(s.Name))
                        .ToList();
                }
                else
                {
                    // If no scheduling rules, check old-style rules (Weekday/Holiday)
                    // Include staff that need to be scheduled based on old rules
                    staffToInclude = staffList.ToList();
                }

                // Add staff rows with their schedules (only for those who should be scheduled)
                foreach (var person in staffToInclude)
                {
                    var staffRow = new StaffScheduleRow
                    {
                        Name = person.Name,
                        Id = person.Id,
                        Group = person.Group,
                    };

                    // Add each date's shift for this person
                    currentDate = StartDate;
                    while (currentDate <= EndDate)
                    {
                        var dateStr = currentDate.ToString("yyyy-MM-dd");
                        var dateHeader = currentDate.ToString("MM-dd");

                        var shiftName =
                            _currentScheduleData.ContainsKey(person.Name)
                            && _currentScheduleData[person.Name].Shifts.ContainsKey(dateStr)
                                ? _currentScheduleData[person.Name].Shifts[dateStr]
                                : "";

                        var shiftColor =
                            shiftName != ""
                                ? shiftList.FirstOrDefault(s => s.ShiftName == shiftName)?.Color
                                    ?? "#FFFFFF"
                                : "#FFFFFF";

                        staffRow.DateShifts[dateHeader] = new ScheduleShiftInfo
                        {
                            ShiftName = shiftName,
                            ShiftColor = shiftColor,
                        };

                        currentDate = currentDate.AddDays(1);
                    }

                    StaffWithSchedules.Add(staffRow);
                }

                // Trigger property changed for both collections to ensure UI updates
                OnPropertyChanged(nameof(DateHeaders));
                OnPropertyChanged(nameof(StaffWithSchedules));

                CanExport = true;
                OnPropertyChanged(nameof(CanExport));
                ExportScheduleCommand.NotifyCanExecuteChanged();

                // Update statistics after schedule is generated
                UpdateStatistics();

                // Success notification
                Growl.InfoGlobal("排班表生成成功！");
            }
            catch (Exception ex)
            {
                // 在实际应用中，应使用消息框或通知服务
                Console.WriteLine($"生成排班表失败: {ex.Message}");
                Growl.ErrorGlobal($"生成排班表失败: {ex.Message}");
            }
        }


        [RelayCommand(CanExecute = nameof(CanExportSchedule))]
        private void ExportSchedule()
        {
            if (_currentScheduleData == null)
            {
                Console.WriteLine("没有可导出的排班数据");
                Growl.InfoGlobal("没有可导出的排班数据");
                return;
            }

            try
            {
                // Generate default file name based on start and end dates
                string startDateStr = StartDate.ToString("yyyyMMdd");
                string endDateStr = EndDate.ToString("yyyyMMdd");
                string defaultFileName = $"排班表{startDateStr}-{endDateStr}.xlsx";

                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    FileName = defaultFileName  // Set the default file name
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // Convert the current schedule data to the expected format for export
                    // Create the schedule in the date -> staff format expected by the export service
                    var scheduleForExport = new Dictionary<string, List<ScheduleExportModel>>();

                    // Get all the dates that were in the original range
                    var currentDate = StartDate;
                    while (currentDate <= EndDate)
                    {
                        var dateStr = currentDate.ToString("yyyy-MM-dd");
                        scheduleForExport[dateStr] = new List<ScheduleExportModel>();

                        // Find all staff assigned to this date
                        foreach (var staffRow in StaffWithSchedules)
                        {
                            var dateHeader = currentDate.ToString("MM-dd");
                            if (staffRow.DateShifts.ContainsKey(dateHeader))
                            {
                                var shiftInfo = staffRow.DateShifts[dateHeader];
                                if (!string.IsNullOrEmpty(shiftInfo.ShiftName))
                                {
                                    // Only add staff who have an assigned shift (not empty)
                                    var staff = StaffData.FirstOrDefault(s =>
                                        s.Name == staffRow.Name
                                    );
                                    if (staff != null)
                                    {
                                        scheduleForExport[dateStr]
                                            .Add(
                                                new ScheduleExportModel
                                                {
                                                    Name = staff.Name,
                                                    Id = staff.Id,
                                                    Group = staff.Group,
                                                    ShiftType = shiftInfo.ShiftName,  // Include the actual shift type
                                                    ShiftColor = shiftInfo.ShiftColor  // Include the shift color
                                                }
                                            );
                                    }
                                }
                            }
                        }

                        currentDate = currentDate.AddDays(1);
                    }

                    _excelExportService.ExportScheduleToExcel(
                        scheduleForExport,
                        saveFileDialog.FileName,
                        StartDate,
                        EndDate
                    );

                    // Save schedule data to JSON file as well
                    SaveScheduleToJSON(scheduleForExport);

                    // 显示成功消息（在实际应用中，应使用消息框服务）
                    Console.WriteLine($"排班表已导出到: {saveFileDialog.FileName}");
                    Growl.InfoGlobal($"排班表已导出到: {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                // 在实际应用中，应使用消息框或通知服务
                Console.WriteLine($"导出排班表失败: {ex.Message}");
                Growl.ErrorGlobal($"导出排班表失败: {ex.Message}");
            }
        }

        // Overload for date-based schedule
        private void SaveScheduleToJSON(Dictionary<string, List<ScheduleExportModel>> scheduleForExport)
        {
            try
            {
                // Convert the schedule export data to ScheduleItemModel format for saving
                var scheduleItems = new List<ScheduleItemModel>();

                foreach (var dateEntry in scheduleForExport)
                {
                    var dateStr = dateEntry.Key;
                    var staffList = dateEntry.Value;

                    foreach (var staff in staffList)
                    {
                        // Use the shift color from the export model if available, otherwise fall back to loading from shifts
                        var shiftColor = !string.IsNullOrEmpty(staff.ShiftColor)
                            ? staff.ShiftColor
                            : _dataService.LoadShifts().FirstOrDefault(s => s.ShiftName == staff.ShiftType)?.Color ?? "#FFFFFF";

                        var scheduleItem = new ScheduleItemModel
                        {
                            Date = dateStr,
                            Shift = staff.ShiftType,
                            PersonName = staff.Name,
                            ShiftColor = shiftColor
                        };

                        scheduleItems.Add(scheduleItem);
                    }
                }

                // Use the DataService to save the schedule to JSON
                _dataService.SaveSchedule(scheduleItems);

                Console.WriteLine($"排班数据已保存到: {_dataService.ScheduleFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存排班数据到JSON失败: {ex.Message}");
                Growl.ErrorGlobal($"保存排班数据到JSON失败: {ex.Message}");
            }
        }

        private void SaveScheduleToJSON(Dictionary<string, ScheduleDataModel> scheduleData)
        {
            try
            {
                // Convert the person-based schedule data to ScheduleItemModel format for saving
                var scheduleItems = new List<ScheduleItemModel>();

                foreach (var personEntry in scheduleData)
                {
                    var personName = personEntry.Key;
                    var personSchedule = personEntry.Value;

                    foreach (var dateShift in personSchedule.Shifts)
                    {
                        var dateStr = dateShift.Key;
                        var shiftName = dateShift.Value;

                        // Only add non-empty shifts
                        if (!string.IsNullOrEmpty(shiftName))
                        {
                            var shiftModel = _dataService.LoadShifts().FirstOrDefault(s => s.ShiftName == shiftName);
                            var shiftColor = shiftModel?.Color ?? "#FFFFFF";

                            var scheduleItem = new ScheduleItemModel
                            {
                                Date = dateStr,
                                Shift = shiftName,
                                PersonName = personName,
                                ShiftColor = shiftColor
                            };

                            scheduleItems.Add(scheduleItem);
                        }
                    }
                }

                // Use the DataService to save the schedule to JSON
                _dataService.SaveSchedule(scheduleItems);

                Console.WriteLine($"排班数据已保存到: {_dataService.ScheduleFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存排班数据到JSON失败: {ex.Message}");
                Growl.ErrorGlobal($"保存排班数据到JSON失败: {ex.Message}");
            }
        }

        private bool CanExportSchedule() => CanExport;

        [RelayCommand(CanExecute = nameof(CanUploadSchedule))]
        private void UploadSchedule()
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    Title = "选择要上传的排班表文件"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // Load the schedule from Excel file using the Excel export service
                    var scheduleData = _excelExportService.ImportScheduleFromExcel(openFileDialog.FileName);

                    if (scheduleData == null || scheduleData.Count == 0)
                    {
                        Growl.InfoGlobal("文件中没有找到有效的排班数据");
                        return;
                    }

                    // Get the date range from the imported data
                    var allDates = new List<DateTime>();
                    foreach (var dateEntry in scheduleData)
                    {
                        if (DateTime.TryParse(dateEntry.Key, out DateTime date))
                        {
                            allDates.Add(date);
                        }
                    }

                    if (allDates.Count == 0)
                    {
                        Growl.InfoGlobal("未能解析排班数据中的日期");
                        return;
                    }

                    var startDate = allDates.Min();
                    var endDate = allDates.Max();

                    // Update the date range properties
                    StartDate = startDate;
                    EndDate = endDate;

                    // Convert to person-based schedule for display
                    var staffList = StaffData.ToList();
                    var shiftList = _dataService.LoadShifts();

                    var scheduleDataForDisplay = new Dictionary<string, ScheduleDataModel>();

                    // Initialize schedule for each staff member
                    foreach (var person in staffList)
                    {
                        scheduleDataForDisplay[person.Name] = new ScheduleDataModel
                        {
                            Id = person.Id,
                            Group = person.Group,
                            Shifts = new Dictionary<string, string>()
                        };

                        // Initialize all dates as empty
                        var currentDate = startDate;
                        while (currentDate <= endDate)
                        {
                            var dateStr = currentDate.ToString("yyyy-MM-dd");
                            scheduleDataForDisplay[person.Name].Shifts[dateStr] = string.Empty;
                            currentDate = currentDate.AddDays(1);
                        }
                    }

                    // Fill in the actual schedule data
                    foreach (var dateEntry in scheduleData)
                    {
                        var dateStr = dateEntry.Key;
                        var assignedStaff = dateEntry.Value;

                        foreach (var staff in assignedStaff)
                        {
                            // Find the staff in our staff data
                            var matchingStaff = staffList.FirstOrDefault(s => s.Name == staff.Name);
                            if (matchingStaff != null && scheduleDataForDisplay.ContainsKey(matchingStaff.Name))
                            {
                                scheduleDataForDisplay[matchingStaff.Name].Shifts[dateStr] = staff.ShiftType;
                            }
                        }
                    }

                    // Update the current schedule data
                    _currentScheduleData = scheduleDataForDisplay;

                    // Clear existing data
                    ScheduleItems.Clear();
                    StaffWithSchedules.Clear();
                    DateHeaders.Clear();

                    // Add date headers
                    var current = startDate;
                    while (current <= endDate)
                    {
                        DateHeaders.Add(current.ToString("MM-dd"));
                        current = current.AddDays(1);
                    }

                    // Add staff rows with their schedules
                    foreach (var person in staffList)
                    {
                        if (scheduleDataForDisplay.ContainsKey(person.Name))
                        {
                            var staffRow = new StaffScheduleRow
                            {
                                Name = person.Name,
                                Id = person.Id,
                                Group = person.Group,
                            };

                            // Add each date's shift for this person
                            current = startDate;
                            while (current <= endDate)
                            {
                                var dateStr = current.ToString("yyyy-MM-dd");
                                var dateHeader = current.ToString("MM-dd");

                                var shiftName = scheduleDataForDisplay[person.Name].Shifts.ContainsKey(dateStr)
                                    ? scheduleDataForDisplay[person.Name].Shifts[dateStr]
                                    : "";

                                var shiftColor = shiftName != ""
                                    ? shiftList.FirstOrDefault(s => s.ShiftName == shiftName)?.Color
                                        ?? "#FFFFFF"
                                    : "#FFFFFF";

                                staffRow.DateShifts[dateHeader] = new ScheduleShiftInfo
                                {
                                    ShiftName = shiftName,
                                    ShiftColor = shiftColor,
                                };

                                current = current.AddDays(1);
                            }

                            StaffWithSchedules.Add(staffRow);
                        }
                    }

                    // Trigger property changed for both collections to ensure UI updates
                    OnPropertyChanged(nameof(DateHeaders));
                    OnPropertyChanged(nameof(StaffWithSchedules));
                    OnPropertyChanged(nameof(StartDate));
                    OnPropertyChanged(nameof(EndDate));

                    CanExport = true;
                    OnPropertyChanged(nameof(CanExport));
                    ExportScheduleCommand.NotifyCanExecuteChanged();

                    // Update statistics after schedule is loaded
                    UpdateStatistics();

                    // Save schedule data to JSON file as well
                    SaveScheduleToJSON(scheduleDataForDisplay);

                    // Success notification
                    Growl.InfoGlobal("排班表上传成功！");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"上传排班表失败: {ex.Message}");
                Growl.ErrorGlobal($"上传排班表失败: {ex.Message}");
            }
        }

        private bool CanUploadSchedule() => CanUpload;

        [RelayCommand]
        private void ClearSchedule()
        {
            ScheduleItems.Clear();
            StaffWithSchedules.Clear();
            DateHeaders.Clear();
            DailyStatistics.Clear();
            StaffStatistics.Clear();
            DailyStatisticsVisibility = Visibility.Collapsed;
            StaffStatisticsVisibility = Visibility.Collapsed;
            CanExport = false;
            _currentScheduleData = null;
            ExportScheduleCommand?.NotifyCanExecuteChanged();
        }

        public void SetStaffData(ObservableCollection<StaffModel> staff)
        {
            StaffData = staff;
        }

        public void SetShiftsData(ObservableCollection<ShiftModel> shifts)
        {
            ShiftsData = shifts;
        }

        public void FillWeekendsIfEmpty(RulesModel rules, DateTime startDate, DateTime endDate)
        {
            // Only populate if the collection is empty
            if (rules.CustomHolidays.Count == 0)
            {
                rules.CustomHolidays.Clear();
                var currentDate = startDate.Date;
                while (currentDate <= endDate.Date)
                {
                    if (
                        currentDate.DayOfWeek == DayOfWeek.Saturday
                        || currentDate.DayOfWeek == DayOfWeek.Sunday
                    )
                    {
                        var dateStr = currentDate.ToString("yyyy-MM-dd");
                        if (!rules.CustomHolidays.Contains(dateStr))
                        {
                            rules.CustomHolidays.Add(dateStr);
                        }
                    }
                    currentDate = currentDate.AddDays(1);
                }
                _dataService.SaveRules(rules);
            }
        }

        // Method to calculate and update statistics
        private void UpdateStatistics()
        {
            if (StaffWithSchedules == null || DateHeaders == null || DateHeaders.Count == 0)
            {
                DailyStatistics.Clear();
                StaffStatistics.Clear();
                DailyStatisticsVisibility = Visibility.Collapsed;
                StaffStatisticsVisibility = Visibility.Collapsed;
                return;
            }

            // Load rules to check for half-day shifts
            var rules = _dataService.LoadRules();
            var halfDayShifts = rules.HalfDayShifts?.ToList() ?? new List<string>();

            // Calculate daily shift statistics
            CalculateDailyStatistics(halfDayShifts);

            // Calculate staff shift statistics
            CalculateStaffStatistics(halfDayShifts);

            // Show the statistics panels
            DailyStatisticsVisibility = Visibility.Visible;
            StaffStatisticsVisibility = Visibility.Visible;
        }

        private void CalculateDailyStatistics(List<string> halfDayShifts)
        {
            DailyStatistics.Clear();

            if (DateHeaders == null || StaffWithSchedules == null)
                return;

            // Get all unique shift types from the schedule
            var allShiftTypes = new HashSet<string>();
            foreach (var staffRow in StaffWithSchedules)
            {
                foreach (var dateHeader in DateHeaders)
                {
                    if (
                        staffRow.DateShifts.ContainsKey(dateHeader)
                        && !string.IsNullOrEmpty(staffRow.DateShifts[dateHeader].ShiftName)
                    )
                    {
                        allShiftTypes.Add(staffRow.DateShifts[dateHeader].ShiftName);
                    }
                }
            }

            var shiftTypesList = allShiftTypes.ToList();
            shiftTypesList.Sort(); // Sort for consistent display

            // For each shift type, add a row to daily statistics
            foreach (var shiftType in shiftTypesList)
            {
                var dailyStat = new DailyShiftStats
                {
                    ShiftType = shiftType,
                    DateCounts = new Dictionary<string, double>(),
                };

                foreach (var dateHeader in DateHeaders)
                {
                    var count = 0.0;
                    foreach (var staffRow in StaffWithSchedules)
                    {
                        if (staffRow.DateShifts.ContainsKey(dateHeader))
                        {
                            var shiftInfo = staffRow.DateShifts[dateHeader];
                            if (shiftInfo.ShiftName == shiftType)
                            {
                                count += 1.0;
                            }
                        }
                    }
                    dailyStat.DateCounts[dateHeader] = count;
                }

                DailyStatistics.Add(dailyStat);
            }

            // Add rest days statistics (empty shifts) - only add this if there are rest days
            var restStat = new DailyShiftStats
            {
                ShiftType = "休息",
                DateCounts = new Dictionary<string, double>(),
            };

            bool hasRestDays = false;
            foreach (var dateHeader in DateHeaders)
            {
                var restCount = 0.0;
                foreach (var staffRow in StaffWithSchedules)
                {
                    if (staffRow.DateShifts.ContainsKey(dateHeader))
                    {
                        var shiftInfo = staffRow.DateShifts[dateHeader];
                        if (string.IsNullOrEmpty(shiftInfo.ShiftName))
                        {
                            restCount += 1.0; // Full rest day
                        }
                    }
                }
                restStat.DateCounts[dateHeader] = restCount;
                if (restCount > 0)
                    hasRestDays = true;
            }

            // Only add rest statistics if there are actual rest days
            if (hasRestDays)
                DailyStatistics.Add(restStat);

            // Calculate "Total Working" - total staff working that day (half days count as 0.5)
            var totalWorkingStat = new DailyShiftStats { ShiftType = "出勤人数", DateCounts = [] };

            foreach (var dateHeader in DateHeaders)
            {
                var totalWorking = 0.0;
                foreach (var staffRow in StaffWithSchedules)
                {
                    if (staffRow.DateShifts.ContainsKey(dateHeader))
                    {
                        var shiftInfo = staffRow.DateShifts[dateHeader];
                        if (
                            !string.IsNullOrEmpty(shiftInfo.ShiftName)
                            && !shiftInfo.ShiftName.Equals("休息")
                        )
                        {
                            // Only count working staff (not rest days)
                            totalWorking += 1.0;
                        }
                    }
                }
                totalWorkingStat.DateCounts[dateHeader] = totalWorking;
            }
            DailyStatistics.Add(totalWorkingStat);

            // Calculate "Total" - total staff count (including rest days)
            var totalStat = new DailyShiftStats
            {
                ShiftType = "总人数",
                DateCounts = new Dictionary<string, double>(),
            };

            foreach (var dateHeader in DateHeaders)
            {
                // Total staff = total working + rest days
                var total = StaffWithSchedules.Count;
                totalStat.DateCounts[dateHeader] = total;
            }
            DailyStatistics.Add(totalStat);
        }

        private void CalculateStaffStatistics(List<string> halfDayShifts)
        {
            StaffStatistics.Clear();

            if (DateHeaders == null || StaffWithSchedules == null)
                return;

            // Get all possible shift types from the system
            var allShiftTypes = new HashSet<string>();

            // First, add all shifts from the shift definitions
            var shiftList = _dataService.LoadShifts();
            foreach (var shift in shiftList)
            {
                allShiftTypes.Add(shift.ShiftName);
            }

            // Then, add any additional shifts that might be in the schedule
            foreach (var staffRow in StaffWithSchedules)
            {
                foreach (var dateHeader in DateHeaders)
                {
                    if (
                        staffRow.DateShifts.ContainsKey(dateHeader)
                        && !string.IsNullOrEmpty(staffRow.DateShifts[dateHeader].ShiftName)
                    )
                    {
                        allShiftTypes.Add(staffRow.DateShifts[dateHeader].ShiftName);
                    }
                    else if (staffRow.DateShifts.ContainsKey(dateHeader))
                    {
                        // Add "休息" for empty shifts to ensure proper statistics calculation
                        allShiftTypes.Add("休息");
                    }
                }
            }

            var shiftTypesList = allShiftTypes.ToList();
            shiftTypesList.Sort(); // Sort for consistent display

            // For each staff member, calculate their shift distribution
            foreach (var staffRow in StaffWithSchedules)
            {
                var staffStat = new StaffShiftStats
                {
                    StaffName = staffRow.Name,
                    ShiftCounts = new Dictionary<string, double>(),
                };

                // Initialize all shift counts to 0
                foreach (var shiftType in shiftTypesList)
                {
                    staffStat.ShiftCounts[shiftType] = 0;
                }

                // Count shifts and rest days for this staff member
                foreach (var dateHeader in DateHeaders)
                {
                    if (staffRow.DateShifts.ContainsKey(dateHeader))
                    {
                        var shiftInfo = staffRow.DateShifts[dateHeader];
                        var shiftType = string.IsNullOrEmpty(shiftInfo.ShiftName) ? "休息" : shiftInfo.ShiftName;

                        if (staffStat.ShiftCounts.ContainsKey(shiftType))
                        {
                            // Count all shifts as 1 in the general shift counts
                            staffStat.ShiftCounts[shiftType]++;
                        }
                        else
                        {
                            staffStat.ShiftCounts[shiftType] = 1;
                        }
                    }
                }

                // According to the requirement: "在员工班级统计的"休息"中，Rules.HalfDayShifts记作0.5天，其他地方统计都记作1天"
                // In employee statistics, HalfDayShifts should count as 0.5 days for rest calculation
                // We need to adjust the "休息" count by considering that half-day shifts count as 0.5 rest day equivalent
                if (staffStat.ShiftCounts.ContainsKey("休息"))
                {
                    double currentRestCount = staffStat.ShiftCounts["休息"];
                    double halfDayShiftAdjustment = 0.0;

                    // For each half-day shift worked, add 0.5 to the rest equivalent count
                    foreach (var kvp in staffStat.ShiftCounts)
                    {
                        if (halfDayShifts.Contains(kvp.Key) && !kvp.Key.Equals("休息"))
                        {
                            // Each half-day shift is equivalent to 0.5 rest day in the calculation
                            halfDayShiftAdjustment += kvp.Value * 0.5;
                        }
                    }

                    // Update the rest count to include the half-day shift equivalent
                    // This reflects that someone who works half-day shifts effectively has more rest time
                    staffStat.ShiftCounts["休息"] = currentRestCount + halfDayShiftAdjustment;
                }

                StaffStatistics.Add(staffStat);
            }

            // Ensure all staff have all possible shift types in their counts for consistency
            foreach (var staffStat in StaffStatistics)
            {
                foreach (var shiftType in shiftTypesList)
                {
                    if (!staffStat.ShiftCounts.ContainsKey(shiftType))
                    {
                        staffStat.ShiftCounts[shiftType] = 0;
                    }
                }
            }
        }

        // Properties to hold references to shared data
        public ObservableCollection<StaffModel> StaffData { get; private set; } =
            new ObservableCollection<StaffModel>();
        public ObservableCollection<ShiftModel> ShiftsData { get; private set; } =
            new ObservableCollection<ShiftModel>();
    }
}
