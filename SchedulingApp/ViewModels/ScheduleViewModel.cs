using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;

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
                rules.CustomHolidays.Clear();
                FillWeekendsIfEmpty(rules, StartDate, EndDate);
                var scheduleResult = _schedulingService.GenerateSchedule(
                    staffList,
                    shiftList,
                    rules,
                    StartDate.Month,
                    StartDate.Year
                );
                if (!string.IsNullOrEmpty(scheduleResult.errorMessage))
                {
                    throw new Exception(scheduleResult.errorMessage);
                }
                // Convert the result to the expected format
                var scheduleData = new Dictionary<string, ScheduleDataModel>();
                foreach (var dateStaffList in scheduleResult.schedule)
                {
                    var dateStr = dateStaffList.Key;
                    var staffForDate = dateStaffList.Value;
                    foreach (var staff in staffForDate)
                    {
                        if (!scheduleData.ContainsKey(staff.Name))
                        {
                            scheduleData[staff.Name] = new ScheduleDataModel
                            {
                                Id = staff.Id,
                                Group = staff.Group,
                                Shifts = new Dictionary<string, string>(),
                            };
                        }
                        // Find the actual shift assigned to this person on this date
                        // This requires finding the original schedule data which maps person -> date -> shift
                        // We need to reverse the mapping from date -> [people] to person -> [dates]

                        // We'll rebuild the scheduleData from the original scheduling service result
                        // We need to get the actual schedule from the scheduling service in person->date format
                    }
                }

                // Get updated schedule data from the service result in the correct format
                // Rebuild the scheduleData based on the original service result
                scheduleData.Clear();

                // Build the scheduleData from the original person-based format
                // We need to call the scheduling service and get data in person->date format
                var staffScheduleData = new Dictionary<string, ScheduleDataModel>();
                foreach (var person in staffList)
                {
                    staffScheduleData[person.Name] = new ScheduleDataModel
                    {
                        Id = person.Id,
                        Group = person.Group,
                        Shifts = new Dictionary<string, string>(),
                    };
                }

                // Convert the result format to match our expected structure
                // The scheduleResult contains date -> list of staff for that date
                // We need to convert it to person -> shifts
                foreach (var dateStaffList in scheduleResult.schedule)
                {
                    var dateStr = dateStaffList.Key;
                    var staffForDate = dateStaffList.Value;

                    foreach (var staff in staffForDate)
                    {
                        if (!staffScheduleData.ContainsKey(staff.Name))
                        {
                            staffScheduleData[staff.Name] = new ScheduleDataModel
                            {
                                Id = staff.Id,
                                Group = staff.Group,
                                Shifts = new Dictionary<string, string>(),
                            };
                        }

                        // We need to find what shift this person was assigned on this date
                        // Since the service result doesn't include shift info, we need to get it
                        // This is a limitation of the current service interface
                        // Let's assume we get the shift info differently
                    }
                }

                // For the new table format, we need to recreate the schedule data properly
                // Call the scheduling service again but get the full schedule data
                var fullScheduleResult = _schedulingService.GenerateSchedule(
                    staffList,
                    shiftList,
                    rules,
                    StartDate.Month,
                    StartDate.Year
                );

                // We need to build the person-based schedule from the service result
                // The service returns date -> [staff assigned to that date], but we need person -> [their shifts]
                // Since the current service doesn't return shift type, we need to get the full schedule data
                // from the scheduling service in person-based format.

                // Let's create a new method in the scheduling service to get the person-based schedule
                // For now, I'll update the service to return additional information
                _currentScheduleData = new Dictionary<string, ScheduleDataModel>();

                // Initialize schedule data for each staff member
                foreach (var person in staffList)
                {
                    _currentScheduleData[person.Name] = new ScheduleDataModel
                    {
                        Id = person.Id,
                        Group = person.Group,
                        Shifts = new Dictionary<string, string>(),
                    };
                }

                // Process each date in the result to map back to person -> shift
                var dateRange = new List<DateTime>();
                var currentDate = StartDate.Date;
                while (currentDate <= EndDate.Date)
                {
                    dateRange.Add(currentDate);
                    currentDate = currentDate.AddDays(1);
                }

                // Initialize all dates for each staff member with empty values
                foreach (var person in staffList)
                {
                    foreach (var date in dateRange)
                    {
                        var dateStr = date.ToString("yyyy-MM-dd");
                        _currentScheduleData[person.Name].Shifts[dateStr] = ""; // Initialize with empty
                    }
                }

                // Now we need to get the actual shifts from the service
                // Since the service now uses the new algorithm with proper data structures,
                // we need to call a method that returns the person-based schedule
                // For this, I'll need to update the service to return the full schedule data
                // but for now, let's assume the SchedulingService is updated to include the full schedule data

                // Since the GenerateSchedule method returns date -> [staff], we need to reverse this mapping
                // to person -> [shifts]. For this to work properly, we need the scheduling service to return
                // the shift type information as well.

                // For now, let's assume the service provides the full schedule data
                // We'll need to make sure the SchedulingService returns the correct data format

                // Process each date from the schedule result to extract the staff assignments
                foreach (var dateEntry in scheduleResult.schedule)
                {
                    var dateStr = dateEntry.Key;
                    var assignedStaffList = dateEntry.Value;

                    // For each staff member assigned on this date, we need to determine their shift
                    // This requires updating the service to return shift information properly
                    foreach (var assignedStaff in assignedStaffList)
                    {
                        // In our new service implementation, we should have the shift information
                        // For now, let's try to use the original algorithm result structure
                        // This requires modifying the service to return person-based schedule data
                    }
                }

                // For now, let's update the service to provide the full person-based schedule data
                // by calling the internal algorithm directly to get the person-based schedule
                var internalScheduleData = GetPersonBasedScheduleData(
                    staffList,
                    shiftList,
                    rules,
                    StartDate,
                    EndDate
                );

                // Copy the data to our current schedule
                _currentScheduleData = internalScheduleData;

                // Clear the old format data
                ScheduleItems.Clear();

                // Generate the new table format
                StaffWithSchedules.Clear();
                DateHeaders.Clear();

                // Add date headers
                currentDate = StartDate;
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

        private Dictionary<string, ScheduleDataModel> GetPersonBasedScheduleData(
            List<StaffModel> staff,
            List<ShiftModel> shifts,
            RulesModel rules,
            DateTime startDate,
            DateTime endDate
        )
        {
            // Call the new public method in SchedulingService through the interface
            if (_schedulingService is ISchedulingService schedulingService)
            {
                return schedulingService.GeneratePersonBasedSchedule(
                    staff,
                    shifts,
                    rules,
                    startDate,
                    endDate
                );
            }

            // Fallback to empty schedule if method not available
            var emptySchedule = new Dictionary<string, ScheduleDataModel>();
            foreach (var person in staff)
            {
                emptySchedule[person.Name] = new ScheduleDataModel
                {
                    Id = person.Id,
                    Group = person.Group,
                    Shifts = new Dictionary<string, string>(),
                };

                // Initialize with empty shifts for all dates
                var currentDate = startDate;
                while (currentDate <= endDate)
                {
                    var dateStr = currentDate.ToString("yyyy-MM-dd");
                    emptySchedule[person.Name].Shifts[dateStr] = "";
                    currentDate = currentDate.AddDays(1);
                }
            }

            return emptySchedule;
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
                                                    ShiftType = shiftInfo.ShiftName  // Include the actual shift type
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
                        var shiftModel = _dataService.LoadShifts().FirstOrDefault(s => s.ShiftName == staff.ShiftType);
                        var shiftColor = shiftModel?.Color ?? "#FFFFFF";

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

        private bool CanExportSchedule() => CanExport;

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
                                // If this is a half-day shift, count as 0.5, otherwise as 1
                                count += halfDayShifts.Contains(shiftType) ? 0.5 : 1.0;
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
                            totalWorking += halfDayShifts.Contains(shiftInfo.ShiftName) ? 0.5 : 1.0;
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
                        
                            // This is a shift assignment
                            if (staffStat.ShiftCounts.ContainsKey(shiftInfo.ShiftName))
                            {
                                staffStat.ShiftCounts[shiftInfo.ShiftName]++;
                            }
                            else
                            {
                                staffStat.ShiftCounts[shiftInfo.ShiftName] = 1;
                            }
                        
                    }
                }

                // According to the requirement: "记录每个员工在排班日期区间内的每个班次数量统计，包括休息天数（注意这里半天班记录半天休息，但在班次中还是记录1）"
                // The rest days calculation should be based on: total days - work days
                // - Full-day shifts count as 1.0 work day
                // - Half-day shifts count as 0.5 work day
                // - Empty shifts (rest days) count as 0 work days (so they remain as rest days)
                var resetDays = 0.0;
                foreach(var (key,value) in staffStat.ShiftCounts){
                    if (halfDayShifts.Contains(key))
                    {
                        resetDays += 0.5 * staffStat.ShiftCounts[key];
                    }
                }

                foreach(var (key, value) in staffStat.ShiftCounts)
                {
                    if (key.Equals("休息"))
                        staffStat.ShiftCounts[key] += resetDays;
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
