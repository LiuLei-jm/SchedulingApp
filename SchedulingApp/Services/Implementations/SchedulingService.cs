using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;

namespace SchedulingApp.Services.Implementations
{
    // Statistics classes for tracking row and column data
    public class StaffStatistics
    {
        public string StaffName { get; set; }
        public Dictionary<string, int> ShiftCounts { get; set; } = new Dictionary<string, int>();
        public double TotalShiftDays { get; set; }
        public int TotalWorkDays { get; set; }
        public double TotalRestDays { get; set; }
    }

    public class DailyStatistics
    {
        public string Date { get; set; }
        public Dictionary<string, int> ShiftCounts { get; set; } = new Dictionary<string, int>();
        public int TotalAssigned { get; set; }
        public int TotalAvailable { get; set; }
    }

    public class SchedulingService : ISchedulingService
    {
        private readonly DataService _dataService;

        private Dictionary<string, int> _dailyShiftCounts = new Dictionary<string, int>();
        private Dictionary<string, Dictionary<string, int>> _staffShiftCounts = new Dictionary<string, Dictionary<string, int>>();

        public SchedulingService(DataService dataService)
        {
            _dataService = dataService;
        }

        public (Dictionary<string, List<StaffModel>> schedule, string errorMessage) GenerateSchedule(
            List<StaffModel> staff,
            List<ShiftModel> shifts,
            RulesModel rules,
            int month,
            int year)
        {
            // Convert the month and year to startDate and endDate
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Generate the schedule using the new algorithm
            var scheduleData = GenerateScheduleWithPriorityAndStats(staff, shifts, rules, startDate, endDate);

            // Convert the result to the format expected by the interface
            var result = new Dictionary<string, List<StaffModel>>();
            string errorMessage = "";

            foreach (var personData in scheduleData)
            {
                var personName = personData.Key;
                var personSchedule = personData.Value;

                // Process each date in the person's schedule
                foreach (var dateShift in personSchedule.Shifts)
                {
                    var dateStr = dateShift.Key;
                    var shiftType = dateShift.Value;

                    if (!result.ContainsKey(dateStr))
                    {
                        result[dateStr] = new List<StaffModel>();
                    }

                    // Find the staff member in the original staff list
                    var staffMember = staff.FirstOrDefault(s => s.Name == personName);
                    if (staffMember != null)
                    {
                        // Create a new StaffModel object for this date's assignment
                        var staffForDate = new StaffModel
                        {
                            Name = staffMember.Name,
                            Id = staffMember.Id,
                            Group = staffMember.Group
                        };

                        result[dateStr].Add(staffForDate);
                    }
                }
            }

            return (result, errorMessage);
        }

        private Dictionary<string, ScheduleDataModel> GenerateScheduleWithPriorityAndStats(
            List<StaffModel> staff,
            List<ShiftModel> shifts,
            RulesModel rules,
            DateTime startDate,
            DateTime endDate)
        {
            var scheduleData = new Dictionary<string, ScheduleDataModel>();

            // Initialize schedule data for each staff member
            foreach (var person in staff)
            {
                scheduleData[person.Name] = new ScheduleDataModel
                {
                    Id = person.Id,
                    Group = person.Group,
                    Shifts = new Dictionary<string, string>()
                };
            }

            // Initialize auxiliary data
            InitializeAuxiliaryData(startDate, endDate, shifts, staff);

            // Generate date range
            var dateRange = new List<DateTime>();
            var currentDate = startDate;
            while (currentDate <= endDate)
            {
                dateRange.Add(currentDate);
                currentDate = currentDate.AddDays(1);
            }

            // Process shifts by priority from rules
            ProcessShiftsByRulesPriority(scheduleData, dateRange, staff, shifts, rules);

            // Fill unassigned dates with lowest priority shifts
            FillUnassignedDates(scheduleData, dateRange);

            return scheduleData;
        }

        private void InitializeAuxiliaryData(DateTime startDate, DateTime endDate, List<ShiftModel> shifts, List<StaffModel> staff)
        {
            _dailyShiftCounts = new Dictionary<string, int>();
            _staffShiftCounts = new Dictionary<string, Dictionary<string, int>>();

            // Initialize daily shift counts
            var currentDate = startDate;
            while (currentDate <= endDate)
            {
                string dateStr = currentDate.ToString("yyyy-MM-dd");
                foreach (var shift in shifts)
                {
                    string key = $"{dateStr}_{shift.ShiftName}";
                    _dailyShiftCounts[key] = 0;
                }
                currentDate = currentDate.AddDays(1);
            }

            // Initialize staff shift counts
            foreach (var person in staff)
            {
                _staffShiftCounts[person.Name] = new Dictionary<string, int>();
                foreach (var shift in shifts)
                {
                    _staffShiftCounts[person.Name][shift.ShiftName] = 0;
                }
            }
        }

        private void ProcessShiftsByRulesPriority(Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> staff, List<ShiftModel> shifts, RulesModel rules)
        {
            // Process scheduling rules from the new SchedulingRules collection if available
            if (rules.SchedulingRules != null && rules.SchedulingRules.Count > 0)
            {
                // Process each scheduling rule
                foreach (var schedulingRule in rules.SchedulingRules)
                {
                    ProcessSchedulingRule(scheduleData, dateRange, staff, shifts, rules, schedulingRule);
                }
            }
            else
            {
                // Fallback to old weekday/holiday rules if new rules aren't available
                ProcessOldStyleRules(scheduleData, dateRange, staff, shifts, rules);
            }
        }

        private void ProcessSchedulingRule(Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> staff, List<ShiftModel> shifts,
            RulesModel rules, SchedulingRuleModel schedulingRule)
        {
            var applicableStaff = schedulingRule.ApplicableStaff?.Any() == true
                ? staff.Where(s => schedulingRule.ApplicableStaff.Contains(s.Name)).ToList()
                : staff.ToList();

            // Initialize statistics tracking
            var staffStats = new Dictionary<string, StaffStatistics>();
            foreach (var person in applicableStaff)
            {
                staffStats[person.Name] = new StaffStatistics
                {
                    StaffName = person.Name,
                    ShiftCounts = new Dictionary<string, int>(),
                    TotalShiftDays = 0,
                    TotalWorkDays = 0,
                    TotalRestDays = 0
                };

                // Initialize shift counts for each shift type
                foreach (var shift in shifts)
                {
                    staffStats[person.Name].ShiftCounts[shift.ShiftName] = 0;
                }
            }

            var dailyStats = new Dictionary<string, DailyStatistics>();
            foreach (var date in dateRange)
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                dailyStats[dateStr] = new DailyStatistics
                {
                    Date = dateStr,
                    ShiftCounts = new Dictionary<string, int>(),
                    TotalAssigned = 0,
                    TotalAvailable = applicableStaff.Count
                };

                // Initialize shift counts for each shift type
                foreach (var shift in shifts)
                {
                    dailyStats[dateStr].ShiftCounts[shift.ShiftName] = 0;
                }
            }

            // Create a map for shift priorities
            var shiftPriorities = new Dictionary<string, int>();
            foreach (var date in dateRange)
            {
                var isHoliday = IsHoliday(date, rules);
                var shiftRequirements = isHoliday ? schedulingRule.HolidayShifts : schedulingRule.WeekdayShifts;

                foreach (var req in shiftRequirements)
                {
                    if (!shiftPriorities.ContainsKey(req.ShiftName))
                    {
                        shiftPriorities[req.ShiftName] = req.Priority ?? int.MaxValue;
                    }
                }
            }

            // Ensure "休息" has higher priority than null-priority shifts
            if (!shiftPriorities.ContainsKey("休息"))
            {
                shiftPriorities["休息"] = int.MaxValue - 1; // Higher than null but lower than defined priorities
            }

            // Process each date in the range according to priority
            foreach (var date in dateRange)
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                var isHoliday = IsHoliday(date, rules);

                // Get shift requirements based on whether it's a holiday or weekday
                var shiftRequirements = isHoliday ? schedulingRule.HolidayShifts : schedulingRule.WeekdayShifts;

                // Sort shifts by priority (lower numbers = higher priority, "休息" has higher priority than null)
                var sortedShiftRequirements = shiftRequirements
                    .OrderBy(req => req.Priority ?? int.MaxValue) // Null priorities come last
                    .ThenBy(req => req.ShiftName == "休息" ? -1 : 0) // "休息" has higher priority than null
                    .ToList();

                // Assign shifts according to priority
                foreach (var shiftRequirement in sortedShiftRequirements)
                {
                    var shiftType = shiftRequirement.ShiftName;
                    var requiredCount = shiftRequirement.RequiredCount;

                    // Assign the required count of this shift
                    AssignShiftForDateWithStats(scheduleData, date, shiftType, requiredCount, applicableStaff, shifts, rules, dateStr, staffStats, dailyStats);
                }
            }

            // After initial assignments, prioritize meeting rest day requirements
            EnsureRestDayRequirements(scheduleData, dateRange, applicableStaff, shifts, rules, staffStats, dailyStats);
        }

        private void ProcessOldStyleRules(Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> staff, List<ShiftModel> shifts, RulesModel rules)
        {
            // Initialize statistics tracking for old-style rules
            var staffStats = new Dictionary<string, StaffStatistics>();
            foreach (var person in staff)
            {
                staffStats[person.Name] = new StaffStatistics
                {
                    StaffName = person.Name,
                    ShiftCounts = new Dictionary<string, int>(),
                    TotalShiftDays = 0,
                    TotalWorkDays = 0,
                    TotalRestDays = 0
                };

                // Initialize shift counts for each shift type
                foreach (var shift in shifts)
                {
                    staffStats[person.Name].ShiftCounts[shift.ShiftName] = 0;
                }
            }

            var dailyStats = new Dictionary<string, DailyStatistics>();
            foreach (var date in dateRange)
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                dailyStats[dateStr] = new DailyStatistics
                {
                    Date = dateStr,
                    ShiftCounts = new Dictionary<string, int>(),
                    TotalAssigned = 0,
                    TotalAvailable = staff.Count
                };

                // Initialize shift counts for each shift type
                foreach (var shift in shifts)
                {
                    dailyStats[dateStr].ShiftCounts[shift.ShiftName] = 0;
                }
            }

            // Process weekday requirements first (non-holidays)
            var weekdayDates = dateRange.Where(date => !IsHoliday(date, rules)).ToList();
            foreach (var date in weekdayDates)
            {
                var dateStr = date.ToString("yyyy-MM-dd");

                // Sort weekday shifts by priority
                var sortedWeekdayShifts = rules.Weekday
                    .OrderBy(req => req.Priority ?? int.MaxValue)
                    .ThenBy(req => req.ShiftName == "休息" ? -1 : 0) // "休息" has higher priority
                    .ToList();

                foreach (var shiftRequirement in sortedWeekdayShifts)
                {
                    var shiftType = shiftRequirement.ShiftName;
                    var requiredCount = shiftRequirement.RequiredCount;

                    AssignShiftForDateWithStats(scheduleData, date, shiftType, requiredCount, staff, shifts, rules, dateStr, staffStats, dailyStats);
                }
            }

            // Process holiday requirements
            var holidayDates = dateRange.Where(date => IsHoliday(date, rules)).ToList();
            foreach (var date in holidayDates)
            {
                var dateStr = date.ToString("yyyy-MM-dd");

                // Sort holiday shifts by priority
                var sortedHolidayShifts = rules.Holiday
                    .OrderBy(req => req.Priority ?? int.MaxValue)
                    .ThenBy(req => req.ShiftName == "休息" ? -1 : 0) // "休息" has higher priority
                    .ToList();

                foreach (var shiftRequirement in sortedHolidayShifts)
                {
                    var shiftType = shiftRequirement.ShiftName;
                    var requiredCount = shiftRequirement.RequiredCount;

                    AssignShiftForDateWithStats(scheduleData, date, shiftType, requiredCount, staff, shifts, rules, dateStr, staffStats, dailyStats);
                }
            }

            // After initial assignments, prioritize meeting rest day requirements
            EnsureRestDayRequirements(scheduleData, dateRange, staff, shifts, rules, staffStats, dailyStats);
        }

        private void AssignShiftForDate(Dictionary<string, ScheduleDataModel> scheduleData, DateTime date,
            string shift, int requiredCount, List<StaffModel> availableStaff, List<ShiftModel> shifts,
            RulesModel rules, string dateStr)
        {
            var eligibleStaff = new List<string>();

            // Find eligible staff for this shift
            foreach (var person in availableStaff)
            {
                var personName = person.Name;
                var personGroup = person.Group;

                // Skip if already assigned for this date
                if (scheduleData[personName].Shifts.ContainsKey(dateStr))
                    continue;

                // Apply constraints
                if (CanAssignShift(personName, shift, date, scheduleData, shifts, rules))
                {
                    // Check VIP/Special group restrictions
                    bool isVipExpert = rules.VipGroups.Contains(personGroup);
                    var specialPersons = rules.SpecialPersons;

                    if (isVipExpert)
                    {
                        // Get VIP special persons for this group
                        var vipSpecialPersons = new List<string>();
                        if (rules.VipSpecialPersons.ContainsKey(personGroup))
                        {
                            vipSpecialPersons = rules.VipSpecialPersons[personGroup].Persons;
                        }

                        if (vipSpecialPersons.Contains(personName))
                        {
                            // VIP special persons can only work 乙1
                            if (shift == "乙1")
                                eligibleStaff.Add(personName);
                        }
                        else if (shift == "乙1" || shift == "丙" || shift == "休息")
                        {
                            // Regular VIP experts can work 乙1, 丙, 休息
                            eligibleStaff.Add(personName);
                        }
                    }
                    else
                    {
                        if (specialPersons.Contains(personName))
                        {
                            if (shift == "乙1")
                            {
                                // Special persons can only work 乙1
                                eligibleStaff.Add(personName);
                            }
                        }
                        else if (shift == "甲2PLUS")
                        {
                            // 甲2PLUS cannot be assigned to VIP experts or special persons
                            if (!rules.VipGroups.Contains(personGroup) && !specialPersons.Contains(personName))
                            {
                                eligibleStaff.Add(personName);
                            }
                        }
                        else
                        {
                            eligibleStaff.Add(personName);
                        }
                    }
                }
            }

            // Shuffle eligible staff for random assignment (but maintain constraints)
            var random = new Random();
            eligibleStaff = eligibleStaff.OrderBy(x => random.Next()).ToList();

            // Assign the required count of shifts
            int assigned = 0;
            foreach (var personName in eligibleStaff)
            {
                if (assigned >= requiredCount)
                    break;

                // Double-check constraints before assignment
                if (CanAssignShift(personName, shift, date, scheduleData, shifts, rules))
                {
                    scheduleData[personName].Shifts[dateStr] = shift;
                    assigned++;

                    // Update statistics
                    string dailyKey = $"{dateStr}_{shift}";
                    _dailyShiftCounts[dailyKey] = _dailyShiftCounts.ContainsKey(dailyKey)
                        ? _dailyShiftCounts[dailyKey] + 1
                        : 1;

                    if (_staffShiftCounts.ContainsKey(personName) && _staffShiftCounts[personName].ContainsKey(shift))
                    {
                        _staffShiftCounts[personName][shift]++;
                    }
                }
            }
        }

        private void AssignShiftForDateWithStats(Dictionary<string, ScheduleDataModel> scheduleData, DateTime date,
            string shift, int requiredCount, List<StaffModel> availableStaff, List<ShiftModel> shifts,
            RulesModel rules, string dateStr, Dictionary<string, StaffStatistics> staffStats, Dictionary<string, DailyStatistics> dailyStats)
        {
            var eligibleStaff = new List<string>();

            // Find eligible staff for this shift
            foreach (var person in availableStaff)
            {
                var personName = person.Name;
                var personGroup = person.Group;

                // Skip if already assigned for this date
                if (scheduleData[personName].Shifts.ContainsKey(dateStr))
                    continue;

                // Apply constraints
                if (CanAssignShift(personName, shift, date, scheduleData, shifts, rules))
                {
                    // Check VIP/Special group restrictions
                    bool isVipExpert = rules.VipGroups.Contains(personGroup);
                    var specialPersons = rules.SpecialPersons;

                    if (isVipExpert)
                    {
                        // Get VIP special persons for this group
                        var vipSpecialPersons = new List<string>();
                        if (rules.VipSpecialPersons.ContainsKey(personGroup))
                        {
                            vipSpecialPersons = rules.VipSpecialPersons[personGroup].Persons;
                        }

                        if (vipSpecialPersons.Contains(personName))
                        {
                            // VIP special persons can only work 乙1
                            if (shift == "乙1")
                                eligibleStaff.Add(personName);
                        }
                        else if (shift == "乙1" || shift == "丙" || shift == "休息")
                        {
                            // Regular VIP experts can work 乙1, 丙, 休息
                            eligibleStaff.Add(personName);
                        }
                    }
                    else
                    {
                        if (specialPersons.Contains(personName))
                        {
                            if (shift == "乙1")
                            {
                                // Special persons can only work 乙1
                                eligibleStaff.Add(personName);
                            }
                        }
                        else if (shift == "甲2PLUS")
                        {
                            // 甲2PLUS cannot be assigned to VIP experts or special persons
                            if (!rules.VipGroups.Contains(personGroup) && !specialPersons.Contains(personName))
                            {
                                eligibleStaff.Add(personName);
                            }
                        }
                        else
                        {
                            eligibleStaff.Add(personName);
                        }
                    }
                }
            }

            // For better assignment, consider staff statistics to balance workload
            // Prioritize staff who have fewer assigned shifts overall
            var staffWithStats = new List<(string Name, int TotalShifts)>();
            foreach (var personName in eligibleStaff)
            {
                var totalShifts = staffStats[personName].ShiftCounts.Values.Sum();
                staffWithStats.Add((personName, totalShifts));
            }

            // Sort by total shifts assigned (ascending - assign to those with fewer shifts first)
            eligibleStaff = staffWithStats.OrderBy(x => x.TotalShifts).ThenBy(x => Guid.NewGuid()).Select(x => x.Name).ToList();

            // Assign the required count of shifts
            int assigned = 0;
            foreach (var personName in eligibleStaff)
            {
                if (assigned >= requiredCount)
                    break;

                // Double-check constraints before assignment
                if (CanAssignShift(personName, shift, date, scheduleData, shifts, rules))
                {
                    scheduleData[personName].Shifts[dateStr] = shift;
                    assigned++;

                    // Update statistics
                    string dailyKey = $"{dateStr}_{shift}";
                    _dailyShiftCounts[dailyKey] = _dailyShiftCounts.ContainsKey(dailyKey)
                        ? _dailyShiftCounts[dailyKey] + 1
                        : 1;

                    if (_staffShiftCounts.ContainsKey(personName) && _staffShiftCounts[personName].ContainsKey(shift))
                    {
                        _staffShiftCounts[personName][shift]++;
                    }

                    // Update statistics tracking
                    if (staffStats.ContainsKey(personName))
                    {
                        var stats = staffStats[personName];
                        if (stats.ShiftCounts.ContainsKey(shift))
                        {
                            stats.ShiftCounts[shift]++;
                        }

                        // Update totals based on shift type
                        if (shift == "休息")
                        {
                            stats.TotalRestDays++;
                            stats.TotalShiftDays += GetShiftDayValue(shift, rules);
                        }
                        else
                        {
                            stats.TotalWorkDays++;
                            stats.TotalShiftDays += GetShiftDayValue(shift, rules);
                        }
                    }

                    if (dailyStats.ContainsKey(dateStr))
                    {
                        var stats = dailyStats[dateStr];
                        if (stats.ShiftCounts.ContainsKey(shift))
                        {
                            stats.ShiftCounts[shift]++;
                        }
                        stats.TotalAssigned++;
                    }
                }
            }
        }

        private void EnsureRestDayRequirements(Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> applicableStaff, List<ShiftModel> shifts,
            RulesModel rules, Dictionary<string, StaffStatistics> staffStats, Dictionary<string, DailyStatistics> dailyStats)
        {
            // This method ensures that rest day requirements are met as much as possible
            // It runs after initial assignments to prioritize rest days

            // For each staff member, check if they're meeting rest day requirements
            foreach (var person in applicableStaff)
            {
                var personName = person.Name;
                if (!staffStats.ContainsKey(personName)) continue;

                var currentRestDays = staffStats[personName].TotalRestDays;

                // The target rest days could come from rules.TotalRestDays or be calculated based on the period
                var targetRestDays = rules.TotalRestDays; // This is the total rest days required per cycle

                // If a staff member has fewer rest days than required, try to assign more rest days
                // on dates where they are not yet assigned
                if (currentRestDays < targetRestDays)
                {
                    var additionalRestNeeded = (int)(targetRestDays - currentRestDays);

                    foreach (var date in dateRange)
                    {
                        if (additionalRestNeeded <= 0) break;

                        var dateStr = date.ToString("yyyy-MM-dd");

                        // Only consider dates where the person is not yet assigned
                        if (!scheduleData[personName].Shifts.ContainsKey(dateStr))
                        {
                            // Check if we can assign a rest day
                            if (CanAssignShift(personName, "休息", date, scheduleData, shifts, rules))
                            {
                                scheduleData[personName].Shifts[dateStr] = "休息";
                                additionalRestNeeded--;

                                // Update statistics
                                string dailyKey = $"{dateStr}_休息";
                                _dailyShiftCounts[dailyKey] = _dailyShiftCounts.ContainsKey(dailyKey)
                                    ? _dailyShiftCounts[dailyKey] + 1
                                    : 1;

                                if (_staffShiftCounts.ContainsKey(personName) && _staffShiftCounts[personName].ContainsKey("休息"))
                                {
                                    _staffShiftCounts[personName]["休息"]++;
                                }

                                // Update statistics tracking
                                if (staffStats.ContainsKey(personName))
                                {
                                    var stats = staffStats[personName];
                                    if (stats.ShiftCounts.ContainsKey("休息"))
                                    {
                                        stats.ShiftCounts["休息"]++;
                                    }
                                    stats.TotalRestDays++;
                                    stats.TotalShiftDays += GetShiftDayValue("休息", rules);
                                }

                                if (dailyStats.ContainsKey(dateStr))
                                {
                                    var stats = dailyStats[dateStr];
                                    if (stats.ShiftCounts.ContainsKey("休息"))
                                    {
                                        stats.ShiftCounts["休息"]++;
                                    }
                                    stats.TotalAssigned++;
                                }
                            }
                        }
                    }
                }
            }

            // Additionally, for half-day shifts, try to arrange them for 2 consecutive days when possible
            // This follows the requirement that half-day shifts should be arranged for 2 consecutive days
            foreach (var shiftName in rules.HalfDayShifts)
            {
                for (int i = 0; i < dateRange.Count - 1; i++) // -1 to have the next day available
                {
                    var currentDate = dateRange[i];
                    var nextDate = dateRange[i + 1];
                    var currentDateStr = currentDate.ToString("yyyy-MM-dd");
                    var nextDateStr = nextDate.ToString("yyyy-MM-dd");

                    // Find staff who have this half-day shift on the current date
                    foreach (var person in applicableStaff)
                    {
                        var personName = person.Name;

                        // Check if this person has the half-day shift on current date but not on the next
                        if (scheduleData[personName].Shifts.ContainsKey(currentDateStr) &&
                            scheduleData[personName].Shifts[currentDateStr] == shiftName &&
                            !scheduleData[personName].Shifts.ContainsKey(nextDateStr))
                        {
                            // Try to assign the same half-day shift to the next date as well
                            if (CanAssignShift(personName, shiftName, nextDate, scheduleData, shifts, rules))
                            {
                                // Get applicable shift requirements to make sure we don't exceed requirements
                                var isCurrentHoliday = IsHoliday(currentDate, rules);
                                var shiftRequirements = isCurrentHoliday ? rules.Holiday : rules.Weekday;
                                var requirement = shiftRequirements.FirstOrDefault(r => r.ShiftName == shiftName);

                                if (requirement != null)
                                {
                                    // Check if we're still within the required count for the next date
                                    var currentDateShiftCount = dailyStats.ContainsKey(currentDateStr)
                                        ? dailyStats[currentDateStr].ShiftCounts.ContainsKey(shiftName)
                                            ? dailyStats[currentDateStr].ShiftCounts[shiftName]
                                            : 0
                                        : 0;

                                    var nextDateShiftCount = dailyStats.ContainsKey(nextDateStr)
                                        ? dailyStats[nextDateStr].ShiftCounts.ContainsKey(shiftName)
                                            ? dailyStats[nextDateStr].ShiftCounts[shiftName]
                                            : 0
                                        : 0;

                                    if (nextDateShiftCount < requirement.RequiredCount)
                                    {
                                        scheduleData[personName].Shifts[nextDateStr] = shiftName;

                                        // Update statistics
                                        string dailyKey = $"{nextDateStr}_{shiftName}";
                                        _dailyShiftCounts[dailyKey] = _dailyShiftCounts.ContainsKey(dailyKey)
                                            ? _dailyShiftCounts[dailyKey] + 1
                                            : 1;

                                        if (_staffShiftCounts.ContainsKey(personName) && _staffShiftCounts[personName].ContainsKey(shiftName))
                                        {
                                            _staffShiftCounts[personName][shiftName]++;
                                        }

                                        // Update statistics tracking
                                        if (staffStats.ContainsKey(personName))
                                        {
                                            var stats = staffStats[personName];
                                            if (stats.ShiftCounts.ContainsKey(shiftName))
                                            {
                                                stats.ShiftCounts[shiftName]++;
                                            }

                                            if (shiftName == "休息")
                                            {
                                                stats.TotalRestDays++;
                                                stats.TotalShiftDays += GetShiftDayValue(shiftName, rules);
                                            }
                                            else
                                            {
                                                stats.TotalWorkDays++;
                                                stats.TotalShiftDays += GetShiftDayValue(shiftName, rules);
                                            }
                                        }

                                        if (dailyStats.ContainsKey(nextDateStr))
                                        {
                                            var stats = dailyStats[nextDateStr];
                                            if (stats.ShiftCounts.ContainsKey(shiftName))
                                            {
                                                stats.ShiftCounts[shiftName]++;
                                            }
                                            stats.TotalAssigned++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool IsHoliday(DateTime date, RulesModel rules)
        {
            // Check if in custom holidays list
            if (rules.CustomHolidays.Contains(date.ToString("yyyy-MM-dd")))
            {
                return true;
            }

            // If no custom holidays set, use default weekends
            return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
        }

        private bool CanAssignShift(string personName, string shift, DateTime date, Dictionary<string, ScheduleDataModel> scheduleData,
            List<ShiftModel> shifts, RulesModel rules)
        {
            var dateStr = date.ToString("yyyy-MM-dd");

            // Check if already assigned for this date
            if (scheduleData[personName].Shifts.ContainsKey(dateStr))
            {
                return false;
            }

            // Check shift filter constraints
            if (rules.ShiftFilters.ContainsKey(shift))
            {
                var shiftFilter = rules.ShiftFilters[shift];

                if (shiftFilter.ExcludedPersons.Contains(personName))
                {
                    return false;
                }

                var personGroup = scheduleData[personName].Group;
                if (shiftFilter.ExcludedGroups.Contains(personGroup))
                {
                    return false;
                }
            }

            // Check consecutive work days
            if (shift != "休息")
            {
                var tempScheduleData = new Dictionary<string, ScheduleDataModel>();
                foreach (var kvp in scheduleData)
                {
                    tempScheduleData[kvp.Key] = new ScheduleDataModel
                    {
                        Id = kvp.Value.Id,
                        Group = kvp.Value.Group,
                        Shifts = new Dictionary<string, string>(kvp.Value.Shifts)
                    };
                }

                tempScheduleData[personName].Shifts[dateStr] = shift;

                double totalConsecutive = CalculateConsecutiveWorkDaysWithRules(personName, date, shift, tempScheduleData, rules);

                if (totalConsecutive > rules.MaxConsecutiveDays)
                {
                    return false;
                }
            }

            // Check holiday restrictions
            if (IsHoliday(date, rules))
            {
                if (shift == "甲2PLUS")
                {
                    var personGroup = scheduleData[personName].Group;
                    if (rules.VipGroups.Contains(personGroup) || rules.SpecialPersons.Contains(personName))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private double CalculateConsecutiveWorkDaysWithRules(string personName, DateTime date, string shift, Dictionary<string, ScheduleDataModel> scheduleData, RulesModel rules)
        {
            double maxConsecutive = 0;
            double currentConsecutive = 0;
            DateTime? lastAssignedDate = null;

            // Get a wider date range for calculation
            var dateRange = new List<DateTime>();
            for (int i = -15; i <= 15; i++)
            {
                dateRange.Add(date.AddDays(i));
            }

            var sortedDates = dateRange.OrderBy(d => d).ToList();

            foreach (var checkDate in sortedDates)
            {
                var checkDateStr = checkDate.ToString("yyyy-MM-dd");
                if (scheduleData[personName].Shifts.ContainsKey(checkDateStr))
                {
                    var assignedShift = scheduleData[personName].Shifts[checkDateStr];
                    if (assignedShift != "休息")
                    {
                        if (lastAssignedDate.HasValue && checkDate.Date == lastAssignedDate.Value.AddDays(1).Date)
                        {
                            currentConsecutive += GetShiftDayValue(assignedShift, rules);
                        }
                        else
                        {
                            currentConsecutive = GetShiftDayValue(assignedShift, rules);
                        }
                        maxConsecutive = Math.Max(maxConsecutive, currentConsecutive);
                        lastAssignedDate = checkDate;
                    }
                    else
                    {
                        currentConsecutive = 0;
                        lastAssignedDate = checkDate;
                    }
                }
            }

            return maxConsecutive;
        }

        private double CalculateConsecutiveWorkDays(string personName, DateTime date, string shift, Dictionary<string, ScheduleDataModel> scheduleData)
        {
            // This method is for general use without access to rules - uses default logic
            double maxConsecutive = 0;
            double currentConsecutive = 0;
            DateTime? lastAssignedDate = null;

            // Get a wider date range for calculation
            var dateRange = new List<DateTime>();
            for (int i = -15; i <= 15; i++)
            {
                dateRange.Add(date.AddDays(i));
            }

            var sortedDates = dateRange.OrderBy(d => d).ToList();

            foreach (var checkDate in sortedDates)
            {
                var checkDateStr = checkDate.ToString("yyyy-MM-dd");
                if (scheduleData[personName].Shifts.ContainsKey(checkDateStr))
                {
                    var assignedShift = scheduleData[personName].Shifts[checkDateStr];
                    if (assignedShift != "休息")
                    {
                        if (lastAssignedDate.HasValue && checkDate.Date == lastAssignedDate.Value.AddDays(1).Date)
                        {
                            currentConsecutive += GetShiftDayValue(assignedShift, null);
                        }
                        else
                        {
                            currentConsecutive = GetShiftDayValue(assignedShift, null);
                        }
                        maxConsecutive = Math.Max(maxConsecutive, currentConsecutive);
                        lastAssignedDate = checkDate;
                    }
                    else
                    {
                        currentConsecutive = 0;
                        lastAssignedDate = checkDate;
                    }
                }
            }

            return maxConsecutive;
        }

        private double GetShiftDayValue(string shift, RulesModel? rules = null)
        {
            // Check if shift is in HalfDayShifts collection from rules
            if (rules != null && rules.HalfDayShifts.Contains(shift))
            {
                return 0.5;
            }
            // Specific check for 甲2PLUS which is also treated as a half-day shift
            else if (shift == "甲2PLUS")
            {
                return 0.5;
            }
            return 1.0;
        }

        private void FillUnassignedDates(Dictionary<string, ScheduleDataModel> scheduleData, List<DateTime> dateRange)
        {
            // For any unassigned dates, assign default shifts based on priority
            foreach (var personName in scheduleData.Keys)
            {
                foreach (var date in dateRange)
                {
                    var dateStr = date.ToString("yyyy-MM-dd");
                    if (!scheduleData[personName].Shifts.ContainsKey(dateStr))
                    {
                        // Default to assigning 乙1 if no better option is available
                        scheduleData[personName].Shifts[dateStr] = "乙1";
                    }
                }
            }
        }

        // Public method to generate schedule and return person-based data
        public Dictionary<string, ScheduleDataModel> GeneratePersonBasedSchedule(
            List<StaffModel> staff,
            List<ShiftModel> shifts,
            RulesModel rules,
            DateTime startDate,
            DateTime endDate)
        {
            return GenerateScheduleWithPriorityAndStats(staff, shifts, rules, startDate, endDate);
        }
    }
}