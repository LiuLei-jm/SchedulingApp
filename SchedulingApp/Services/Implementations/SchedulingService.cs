using SchedulingApp.Helpers;
using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;
using System.Windows;

namespace SchedulingApp.Services.Implementations
{
    // Statistics classes for tracking row and column data
    public class StaffStatistics
    {
        public string StaffName { get; set; } = string.Empty;
        public Dictionary<string, double> ShiftCounts { get; set; } = [];
        public double TotalAssigned { get; set; }
    }

    public class DailyStatistics
    {
        public string Date { get; set; } = string.Empty;
        public Dictionary<string, double> ShiftCounts { get; set; } = [];
        public double TotalAssigned { get; set; }
        public double TotalAvailable { get; set; }
    }

    public class SchedulingService : ISchedulingService
    {
        private readonly IDataService _dataService;

        public SchedulingService(IDataService dataService)
        {
            _dataService = dataService;
        }

        public (
            Dictionary<string, List<StaffModel>> schedule,
            string errorMessage
        ) GenerateSchedule(
            List<StaffModel> staff,
            List<ShiftModel> shifts,
            RulesModel rules,
            int month,
            int year
        )
        {
            // Convert the month and year to startDate and endDate
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Generate the schedule using the new algorithm
            var scheduleData = GenerateScheduleWithPriorityAndStats(
                staff,
                shifts,
                rules,
                startDate,
                endDate
            );

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
                            Group = staffMember.Group,
                        };

                        result[dateStr].Add(staffForDate);
                    }
                }
            }

            return (result, errorMessage);
        }

        private Dictionary<string, ScheduleDataModel> GenerateScheduleWithPriorityAndStats(
            List<StaffModel> staffs,
            List<ShiftModel> shifts,
            RulesModel rules,
            DateTime startDate,
            DateTime endDate
        )
        {
            var scheduleData = new Dictionary<string, ScheduleDataModel>();

            // Initialize schedule data for each staff member
            foreach (var person in staffs)
            {
                scheduleData[person.Name] = new ScheduleDataModel
                {
                    Id = person.Id,
                    Group = person.Group,
                    Shifts = new Dictionary<string, string>(),
                };
            }

            // Generate date range
            var dateRange = new List<DateTime>();
            var currentDate = startDate;
            while (currentDate <= endDate)
            {
                // Initialize all dates as empty shifts for all staff
                foreach (var person in staffs)
                {
                    scheduleData[person.Name].Shifts[currentDate.ToString("yyyy-MM-dd")] = "";
                }
                dateRange.Add(currentDate);
                currentDate = currentDate.AddDays(1);
            }

            // Load existing schedule data before startDate for MaxConsecutiveDays constraint
            var existingSchedule = LoadExistingScheduleBefore(startDate, staffs);

            // Process scheduling rules from the new SchedulingRules collection if available
            if (rules.SchedulingRules != null && rules.SchedulingRules.Count > 0)
            {
                // Process each scheduling rule independently
                foreach (var schedulingRule in rules.SchedulingRules)
                {
                    ProcessSchedulingRuleWithFullRequirements(
                        scheduleData,
                        dateRange,
                        staffs,
                        shifts,
                        rules,
                        schedulingRule,
                        existingSchedule
                    );
                }
            }
            else
            {
                MessageBox.Show("没有定义排班规则，请先设置规则。");
                // Fallback to old weekday/holiday rules if new rules aren't available
                //ProcessOldStyleRulesWithFullRequirements(
                //    scheduleData,
                //    dateRange,
                //    staffs,
                //    shifts,
                //    rules,
                //    existingSchedule
                //);
            }

            return scheduleData;
        }

        // Helper method to load existing schedule before the start date for MaxConsecutiveDays constraint
        private Dictionary<string, Dictionary<string, string>> LoadExistingScheduleBefore(
            DateTime startDate,
            List<StaffModel> staff
        )
        {
            var existingSchedule = new Dictionary<string, Dictionary<string, string>>();

            // Initialize for all staff
            foreach (var person in staff)
            {
                existingSchedule[person.Name] = new Dictionary<string, string>();
            }

            // Load schedule data from JSON or other sources
            var allScheduleItems = _dataService.LoadSchedule();

            // Get schedule items that are before the start date
            var dateBeforeStart = startDate.AddDays(-7); // Include the day before start date
            var itemsBeforeStart = allScheduleItems
                .Where(item =>
                {
                    if (DateTime.TryParse(item.Date, out DateTime itemDate))
                    {
                        return dateBeforeStart <= itemDate && itemDate < startDate;
                    }
                    return false;
                })
                .ToList();

            // Populate the existing schedule
            foreach (var item in itemsBeforeStart)
            {
                if (existingSchedule.ContainsKey(item.PersonName))
                {
                    existingSchedule[item.PersonName][item.Date] = item.Shift;
                }
            }

            return existingSchedule;
        }

        private void ProcessSchedulingRuleWithFullRequirements(
            Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange,
            List<StaffModel> staffs,
            List<ShiftModel> shifts,
            RulesModel rules,
            SchedulingRuleModel schedulingRule,
            Dictionary<string, Dictionary<string, string>> existingSchedule
        )
        {
            Dictionary<string, StaffStatistics> staffStats = [];
            Dictionary<string, DailyStatistics> dailyStats = [];

            var applicableStaff =
                schedulingRule.ApplicableStaff?.Any() == true
                    ? staffs.Where(s => schedulingRule.ApplicableStaff.Contains(s.Name)).ToList()
                    : staffs.ToList();

            foreach (var person in applicableStaff)
            {
                staffStats[person.Name] = new StaffStatistics
                {
                    StaffName = person.Name,
                    ShiftCounts = new Dictionary<string, double>(),
                };

                // Initialize shift counts for each shift type
                foreach (var shift in shifts)
                {
                    staffStats[person.Name].ShiftCounts[shift.ShiftName] = 0;
                }
            }

            foreach (var date in dateRange)
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                dailyStats[dateStr] = new DailyStatistics
                {
                    Date = dateStr,
                    ShiftCounts = new Dictionary<string, double>(),
                    TotalAssigned = 0,
                    TotalAvailable = applicableStaff.Count,
                };

                // Initialize shift counts for each shift type
                foreach (var shift in shifts)
                {
                    dailyStats[dateStr].ShiftCounts[shift.ShiftName] = 0;
                }
            }

            // FIRST LOOP: Process priority shifts to ensure exact required count (must achieve required count exactly)
            ProcessPriorityShifts(
                scheduleData,
                dateRange,
                applicableStaff,
                shifts,
                rules,
                schedulingRule,
                existingSchedule,
                staffStats,
                dailyStats
            );

            // SECOND LOOP: Ensure each employee's rest days equal Rules.TotalRestDays
            ProcessRestDayAdjustments(
                scheduleData,
                dateRange,
                applicableStaff,
                shifts,
                rules,
                schedulingRule,
                existingSchedule,
                staffStats,
                dailyStats
            );



            // THIRD LOOP: Fill remaining unassigned slots with non-priority shifts
            ProcessNonPriorityShifts(
                scheduleData,
                dateRange,
                applicableStaff,
                shifts,
                rules,
                schedulingRule,
                existingSchedule,
                staffStats,
                dailyStats
            );

            // Handle half-day shift consecutive arrangement for shifts like 甲2PLUS
            //HandleHalfDayShiftConsecutiveArrangement(
            //    scheduleData,
            //    dateRange,
            //    applicableStaff,
            //    shifts,
            //    rules
            //);
        }

        private void ProcessPriorityShifts(
            Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange,
            List<StaffModel> applicableStaff,
            List<ShiftModel> shifts,
            RulesModel rules,
            SchedulingRuleModel schedulingRule,
            Dictionary<string, Dictionary<string, string>> existingSchedule,
            Dictionary<string, StaffStatistics> staffStats,
            Dictionary<string, DailyStatistics> dailyStats
        )
        {
            foreach (var date in dateRange)
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                var isHoliday = IsHoliday(date, rules);

                // Get shift requirements based on whether it's a holiday or weekday
                var shiftRequirements = isHoliday
                    ? schedulingRule.HolidayShifts
                    : schedulingRule.WeekdayShifts;

                var sortedShiftRequirements = shiftRequirements
                    .OrderBy(req => req.Priority ?? int.MaxValue) // Null priorities come last
                    .ToList();

                var priorityShifts = sortedShiftRequirements
                    .Where(req => req.Priority.HasValue)
                    .ToList();
                var nonPriorityShifts = sortedShiftRequirements
                    .Where(req => !req.Priority.HasValue)
                    .ToList();

                priorityShifts.AddRange(nonPriorityShifts);

                // Process each priority shift to ensure exact required count
                foreach (var shiftRequirement in priorityShifts)
                {
                    var shiftType = shiftRequirement.ShiftName;
                    var requiredCount = shiftRequirement.RequiredCount ?? 0;

                    // If we need more assignments, assign them
                    if (dailyStats[dateStr].ShiftCounts[shiftType] < requiredCount)
                    {
                        // Find unassigned staff who are eligible for this shift
                        var unassignedEligibleStaff = new List<StaffModel>();
                        foreach (var person in applicableStaff)
                        {
                            var personName = person.Name;
                            if (
                                scheduleData[personName].Shifts[dateStr] == ""
                                && CanAssignShiftWithConsecutiveCheckAndStats(
                                    personName,
                                    shiftType,
                                    date,
                                    scheduleData,
                                    shifts,
                                    rules,
                                    existingSchedule
                                )
                            )
                            {
                                unassignedEligibleStaff.Add(person);
                            }
                        }

                        // Rotate staff to ensure fair distribution by target shift type
                        var rotatedEligibleStaff = RotateStaffForFairDistribution(
                            unassignedEligibleStaff,
                            date,
                            staffStats,
                            shiftType
                        );

                        // Assign the needed number of people to this shift
                        foreach (var person in rotatedEligibleStaff)
                        {
                            if (dailyStats[dateStr].ShiftCounts[shiftType] >= requiredCount)
                                break;

                            var personName = person.Name;

                            if (
                                scheduleData[personName].Shifts[dateStr] == ""
                                && CanAssignShiftWithConsecutiveCheckAndStats(
                                    personName,
                                    shiftType,
                                    date,
                                    scheduleData,
                                    shifts,
                                    rules,
                                    existingSchedule
                                )
                            )
                            {
                                // Perform assignment
                                scheduleData[personName].Shifts[dateStr] = shiftType;

                                if (rules.HalfDayShifts.Contains(shiftType))
                                {
                                    HalfdayShiftsAreArrangedContinuously(
                                        scheduleData,
                                        personName,
                                        date,
                                        dateRange[^1],
                                        shiftType,
                                        shifts,
                                        rules,
                                        schedulingRule,
                                        existingSchedule,
                                        staffStats,
                                        dailyStats
                                    );
                                }
                            }
                            else
                            {
                                scheduleData[personName].Shifts[dateStr] = rules.RestShiftName;
                            }
                            // Update statistics
                            UpdateStatisticsAfterAssignment(
                                personName,
                                shiftType,
                                dateStr,
                                rules,
                                scheduleData,
                                staffStats,
                                dailyStats
                            );
                        }
                    }
                    // If we have MORE than required, we need to potentially remove some assignments
                    else if (dailyStats[dateStr].ShiftCounts[shiftType] > requiredCount)
                    {
                        // This situation occurs when there are already more people assigned than needed
                        // We need to find excess assignments and potentially remove them
                        var excess = dailyStats[dateStr].ShiftCounts[shiftType] - requiredCount;

                        // Identify people currently assigned to this priority shift
                        var assignedPeople = new List<StaffModel>();
                        foreach (var person in applicableStaff)
                        {
                            var personName = person.Name;
                            if (scheduleData[personName].Shifts[dateStr] == shiftType)
                            {
                                assignedPeople.Add(person);
                            }
                        }
                        foreach (var assigned in assignedPeople)
                        {
                            if (excess <= 0)
                                break;
                            scheduleData[assigned.Name].Shifts[dateStr] = "";
                            excess--;
                        }
                    }
                }
            }
        }

        private void HalfdayShiftsAreArrangedContinuously(
            Dictionary<string, ScheduleDataModel> scheduleData,
            string personName,
            DateTime date,
            DateTime endDate,
            string shiftType,
            List<ShiftModel> shifts,
            RulesModel rules,
            SchedulingRuleModel schedulingRule,
            Dictionary<string, Dictionary<string, string>> existionSchedule,
            Dictionary<string, StaffStatistics>? staffStats,
            Dictionary<string, DailyStatistics>? dailyStats
        )
        {
            var nextDate = date.AddDays(1);
            while (nextDate <= endDate)
            {
                var dateStr = nextDate.ToString("yyyy-MM-dd");
                var isHoliday = IsHoliday(nextDate, rules);

                // Get shift requirements based on whether it's a holiday or weekday
                var shiftRequirements = isHoliday
                    ? schedulingRule.HolidayShifts
                    : schedulingRule.WeekdayShifts;
                foreach (var shiftRequirement in shiftRequirements)
                {
                    if (shiftRequirement.ShiftName == shiftType)
                    {
                        // Check if the next date is unassigned and can be assigned the same half-day shift
                        if (
                            scheduleData[personName].Shifts[dateStr] == ""
                                                    )
                        {
                            // Assign the same half-day shift to the next date
                            scheduleData[personName].Shifts[dateStr] = shiftType;
                            UpdateStatisticsAfterAssignment(
                                personName,
                                shiftType,
                                dateStr,
                                rules,
                                scheduleData,
                                staffStats,
                                dailyStats
                            );
                            return;
                        }
                        else
                        {
                            // Stop if we encounter a date that cannot be assigned the same half-day shift
                            break;
                        }
                    }
                }
                nextDate = nextDate.AddDays(1);
            }
        }

        private void ProcessNonPriorityShifts(
            Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange,
            List<StaffModel> applicableStaff,
            List<ShiftModel> shifts,
            RulesModel rules,
            SchedulingRuleModel schedulingRule,
            Dictionary<string, Dictionary<string, string>> existingSchedule,
            Dictionary<string, StaffStatistics> staffStats,
            Dictionary<string, DailyStatistics> dailyStats
        )
        {
            // THIRD LOOP: Fill remaining unassigned slots with non-priority shifts
            foreach (var date in dateRange)
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                var isHoliday = IsHoliday(date, rules);

                // Get shift requirements based on whether it's a holiday or weekday
                var shiftRequirements = isHoliday
                    ? schedulingRule.HolidayShifts
                    : schedulingRule.WeekdayShifts;

                // Get only non-priority shifts (those without priority values)
                var nonPriorityShifts = shiftRequirements
                    .Where(req => !req.Priority.HasValue)
                    .ToList();

                // Process each non-priority shift to fill remaining unassigned slots first
                foreach (var shiftRequirement in nonPriorityShifts)
                {
                    var shiftType = shiftRequirement.ShiftName;
                    var requiredCount = shiftRequirement.RequiredCount ?? 0;

                    // Count how many are already assigned to this shift on this date
                    foreach (var person in applicableStaff)
                    {
                        var personName = person.Name;
                        if (scheduleData[personName].Shifts[dateStr] == "")
                        {
                            scheduleData[personName].Shifts[dateStr] = shiftType;
                        }
                    }
                }
            }
        }

        private void ProcessRestDayAdjustments(
            Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange,
            List<StaffModel> applicableStaff,
            List<ShiftModel> shifts,
            RulesModel rules,
            SchedulingRuleModel schedulingRule,
            Dictionary<string, Dictionary<string, string>> existingSchedule,
            Dictionary<string, StaffStatistics> staffStats,
            Dictionary<string, DailyStatistics> dailyStats
        )
        {
            var averageRestDays = (applicableStaff.Count * rules.TotalRestDays) / dateRange.Count;
            var weekDayRestDays = Math.Max(1, averageRestDays - 1); // 至少1人休息
            var holidayRestDays = averageRestDays + 2; // 节假日休息人数更多

            // 首先计算每个员工目前的休息日数量
            var personRestDays = new Dictionary<string, int>();
            foreach (var person in applicableStaff)
            {
                var personName = person.Name;
                personRestDays[personName] = 0;

                foreach (var date in dateRange)
                {
                    var dateStr = date.ToString("yyyy-MM-dd");
                    var shiftType = scheduleData[personName].Shifts[dateStr];

                    if (shiftType == rules.RestShiftName)
                    {
                        personRestDays[personName]++;
                    }
                    else if (string.IsNullOrEmpty(shiftType))
                    {
                        // 未分配的日期可以考虑设为休息
                        personRestDays[personName]++; // 先预设为可转换的休息日
                    }
                }
            }

            // 创建一个需要分配的休息日候选池，用于均匀分布
            var restDayNeeds = new List<(string PersonName, double RequiredRestDays, double CurrentRestDays)>();
            foreach (var person in applicableStaff)
            {
                var personName = person.Name;
                double currentRestDayEquivalents = 0.0;

                foreach (var date in dateRange)
                {
                    var dateStr = date.ToString("yyyy-MM-dd");
                    var shiftType = scheduleData[personName].Shifts[dateStr];

                    if (shiftType == rules.RestShiftName)
                    {
                        currentRestDayEquivalents += 1.0; // Full rest day
                    }
                    else if (rules.HalfDayShifts.Contains(shiftType))
                    {
                        currentRestDayEquivalents += 0.5; // Half-day shift counts as 0.5 toward rest
                    }
                    // Regular work shifts don't contribute to rest day requirement
                }

                int targetRestDays = rules.TotalRestDays;
                int currentRestDays = (int)currentRestDayEquivalents;

                if (targetRestDays > currentRestDays)
                {
                    restDayNeeds.Add((personName, targetRestDays, currentRestDays));
                }
            }

            // 按当前休息日数量升序排列，优先为休息日少的员工分配
            restDayNeeds = restDayNeeds.OrderBy(x => x.CurrentRestDays).ToList();

            // 按日期逐一处理，以实现均匀分布
            foreach (var date in dateRange)
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                var isHoliday = IsHoliday(date, rules);
                var dailyMaxRestDays = isHoliday ? holidayRestDays : weekDayRestDays;

                // 计算当日已有的休息人数
                int currentDailyRestCount = 0;
                foreach (var person in applicableStaff)
                {
                    if (scheduleData[person.Name].Shifts[dateStr] == rules.RestShiftName)
                    {
                        currentDailyRestCount++;
                    }
                }

                // 如果当日休息人数已达到上限，跳过
                if (currentDailyRestCount >= dailyMaxRestDays)
                {
                    continue;
                }

                // 确定当日还可以安排多少休息日
                int remainingRestSlots = dailyMaxRestDays - currentDailyRestCount;

                // 对于当前需要休息的员工，按顺序尝试在该日期分配休息
                var availablePeople = new List<(string PersonName, double RequiredRestDays, double CurrentRestDays)>();
                foreach (var (personName, required, current) in restDayNeeds)
                {
                    // 如果该日期该员工已有非休息班次，则不能安排休息
                    if (scheduleData[personName].Shifts[dateStr] == "")
                    {
                        // 确保为该员工安排休息后不会超过最大连续工作日限制
                        if (CanAssignRestWithConsecutiveCheckAndStats(
                            personName,
                            date,
                            scheduleData,
                            rules,
                            existingSchedule,
                            staffStats) > 0)
                        {
                            availablePeople.Add((personName, required, current));
                        }
                    }
                }

                // 优先安排当前休息日较少的员工休息
                var sortedAvailable = availablePeople.OrderBy(x =>
                {
                    // 计算该员工当前的总休息天数
                    double totalCurrentRest = 0;
                    foreach (var d in dateRange)
                    {
                        var dStr = d.ToString("yyyy-MM-dd");
                        var shift = scheduleData[x.PersonName].Shifts[dStr];
                        if (shift == rules.RestShiftName)
                            totalCurrentRest++;
                        if(rules.HalfDayShifts.Contains(shift))
                            totalCurrentRest += 0.5;
                    }
                    return totalCurrentRest; // 休息天数少的优先
                }).ToList();

                // 分配剩余休息名额，直到达到当日最大值或没有更多需要休息的员工
                int assignedToday = 0;
                foreach (var (personName, required, current) in sortedAvailable)
                {
                    if (assignedToday >= remainingRestSlots)
                        break;

                    // 再次检查该员工的休息总数，确保不超过要求
                    double currentPersonRestCount = 0;
                    foreach (var d in dateRange)
                    {
                        var dStr = d.ToString("yyyy-MM-dd");
                        var shift = scheduleData[personName].Shifts[dStr];
                        if (shift == rules.RestShiftName)
                            currentPersonRestCount++;
                        else if(rules.HalfDayShifts.Contains(shift))
                            currentPersonRestCount += 0.5;
                    }

                    if (currentPersonRestCount < required)
                    {
                        // 分配休息
                        scheduleData[personName].Shifts[dateStr] = rules.RestShiftName;
                        UpdateStatisticsAfterAssignment(
                            personName,
                            rules.RestShiftName,
                            dateStr,
                            rules,
                            scheduleData,
                            staffStats,
                            dailyStats
                        );
                        assignedToday++;
                    }
                }
            }

            // 最后检查是否所有员工都达到了最低休息天数要求，并优化连续工作天数
            foreach (var person in applicableStaff)
            {
                var personName = person.Name;

                // First, ensure each employee has at least the required number of rest days
                // Calculate current rest days for this person (full rest days + half days as 0.5)
                double currentRestDays = 0;
                foreach (var date in dateRange)
                {
                    var dateStr = date.ToString("yyyy-MM-dd");
                    var shift = scheduleData[personName].Shifts[dateStr];
                    if (shift == rules.RestShiftName)
                    {
                        currentRestDays++; // Full rest day
                    }
                    else if (rules.HalfDayShifts.Contains(shift))
                    {
                        currentRestDays += 0.5; // Half-day shift counts as 0.5 toward rest
                    }
                }

                // Check if the person already meets the required rest days
                double neededRestDays = Math.Max(0, rules.TotalRestDays - currentRestDays);

                // First, try to assign additional rest days if needed
                if (neededRestDays > 0)
                {
                    // Find candidate dates to convert to rest days
                    // These are dates that are currently empty or have non-priority shifts
                    var candidateDates = new List<(DateTime date, double currentRestCount)>();

                    foreach (var date in dateRange)
                    {
                        var dateStr = date.ToString("yyyy-MM-dd");
                        var currentShift = scheduleData[personName].Shifts[dateStr];

                        // Empty shifts ("") and shifts that have no priority (not in schedulingRule priority shifts)
                        // can be converted to rest
                        bool canConvertToRest = string.IsNullOrEmpty(currentShift) || currentShift == "";

                        if (!canConvertToRest)
                        {
                            // Check all scheduling rules to see if any of them define this shift as non-priority
                            foreach (var rule in rules.SchedulingRules)
                            {
                                var dateStrForCheck = date.ToString("yyyy-MM-dd");
                                var isHoliday = IsHoliday(date, rules);

                                // Get the appropriate shifts based on whether it's a holiday or not
                                var applicableShifts = isHoliday ? rule.HolidayShifts : rule.WeekdayShifts;

                                var shiftRequirement = applicableShifts.FirstOrDefault(s => s.ShiftName == currentShift);

                                if (shiftRequirement != null && !shiftRequirement.Priority.HasValue)
                                {
                                    canConvertToRest = true;
                                    break;
                                }
                            }
                        }

                        if (canConvertToRest)
                        {
                            // Count how many people are already resting on this day (for optimization)
                            double dailyRestCount = 0;
                            foreach (var p in applicableStaff)
                            {
                                var pName = p.Name;
                                var pShift = scheduleData[pName].Shifts[dateStr];
                                if (pShift == rules.RestShiftName)
                                {
                                    dailyRestCount++;
                                }
                                else if (rules.HalfDayShifts.Contains(pShift))
                                {
                                    dailyRestCount += 0.5;
                                }
                            }

                            candidateDates.Add((date, dailyRestCount));
                        }
                    }

                    // Sort candidate dates by rest count (ascending) - prioritize days with fewer rest assignments
                    candidateDates = candidateDates.OrderBy(cd => cd.currentRestCount).ToList();

                    // Convert candidate dates to rest, considering the MaxConsecutiveDays constraint
                    foreach (var (date, _) in candidateDates)
                    {
                        if (neededRestDays <= 0) break; // Requirement met

                        var dateStr = date.ToString("yyyy-MM-dd");
                        var currentShift = scheduleData[personName].Shifts[dateStr]; // Get current shift value for this date

                        // Create a temporary schedule to test the assignment
                        var tempSchedule = new Dictionary<string, string>(scheduleData[personName].Shifts);
                        tempSchedule[dateStr] = rules.RestShiftName;

                        // Check if this assignment violates the MaxConsecutiveDays constraint
                        if (CheckMaxConsecutiveDaysConstraint(
                            tempSchedule,
                            rules.MaxConsecutiveDays,
                            dateRange,
                            personName,
                            existingSchedule))
                        {
                            // Assign this day as rest
                            scheduleData[personName].Shifts[dateStr] = rules.RestShiftName;

                            // Update statistics
                            UpdateStatisticsAfterAssignment(
                                personName,
                                rules.RestShiftName,
                                dateStr,
                                rules,
                                scheduleData,
                                staffStats,
                                dailyStats
                            );

                            // Update needed rest days based on the type of day we're converting
                            if (rules.HalfDayShifts.Contains(currentShift))
                            {
                                neededRestDays -= 0.5; // If it was a half day shift, we're adding 0.5 to rest
                            }
                            else
                            {
                                neededRestDays -= 1.0; // If it was empty, we're adding 1 day
                            }
                        }
                    }
                }

                // Second, check if MaxConsecutiveDays constraint is still violated and fix by moving rest days if necessary
                // Check if the current schedule violates MaxConsecutiveDays constraint for this person
                var currentSchedule = scheduleData[personName].Shifts;
                if (!CheckMaxConsecutiveDaysConstraint(
                    currentSchedule,
                    rules.MaxConsecutiveDays,
                    dateRange,
                    personName,
                    existingSchedule))
                {
                    // The constraint is violated, so we need to fix it by moving some work days to rest days
                    // Find the problematic consecutive work day sequences and try to insert rest days
                    var problematicDates = FindConsecutiveWorkDayIssues(
                        currentSchedule,
                        dateRange,
                        personName,
                        existingSchedule,
                        rules
                    );

                    // Try to resolve the consecutive work day issues by converting some work days to rest days
                    foreach (var date in problematicDates)
                    {
                        var dateStr = date.ToString("yyyy-MM-dd");
                        var currentShift = currentSchedule[dateStr];

                        // Check if this shift can be converted (is empty or non-priority)
                        bool canConvert = string.IsNullOrEmpty(currentShift) || currentShift == "";

                        if (!canConvert && !string.IsNullOrEmpty(currentShift) && currentShift != rules.RestShiftName)
                        {
                            foreach (var rule in rules.SchedulingRules)
                            {
                                var isHoliday = IsHoliday(date, rules);
                                var applicableShifts = isHoliday ? rule.HolidayShifts : rule.WeekdayShifts;
                                var shiftRequirement = applicableShifts.FirstOrDefault(s => s.ShiftName == currentShift);

                                if (shiftRequirement != null && !shiftRequirement.Priority.HasValue)
                                {
                                    canConvert = true;
                                    break;
                                }
                            }
                        }

                        // Check if converting this day to rest would fix the constraint
                        if (canConvert)
                        {
                            var tempSchedule = new Dictionary<string, string>(currentSchedule);
                            tempSchedule[dateStr] = rules.RestShiftName;

                            if (CheckMaxConsecutiveDaysConstraint(
                                tempSchedule,
                                rules.MaxConsecutiveDays,
                                dateRange,
                                personName,
                                existingSchedule))
                            {
                                // Convert this day to rest
                                scheduleData[personName].Shifts[dateStr] = rules.RestShiftName;

                                // Update statistics
                                UpdateStatisticsAfterAssignment(
                                    personName,
                                    rules.RestShiftName,
                                    dateStr,
                                    rules,
                                    scheduleData,
                                    staffStats,
                                    dailyStats
                                );
                            }
                        }
                    }
                }
            }
        }

        // Helper method to find dates that are part of problematic consecutive work day sequences
        private List<DateTime> FindConsecutiveWorkDayIssues(
            Dictionary<string, string> personSchedule,
            List<DateTime> dateRange,
            string personName,
            Dictionary<string, Dictionary<string, string>> existingSchedule,
            RulesModel rules)
        {
            var problematicDates = new List<DateTime>();

            // Create a temporary combined view of the schedule including existing schedule
            var combinedSchedule = new Dictionary<string, string>();

            // Add existing schedule if provided
            if (existingSchedule != null && existingSchedule.ContainsKey(personName))
            {
                foreach (var kvp in existingSchedule[personName])
                {
                    combinedSchedule[kvp.Key] = kvp.Value;
                }
            }

            // Add new schedule data
            foreach (var kvp in personSchedule)
            {
                combinedSchedule[kvp.Key] = kvp.Value;
            }

            // Get a broader date range to check across the boundary
            var allDates = new List<DateTime>();
            // Add dates from existing schedule
            foreach (var kvp in combinedSchedule)
            {
                if (DateTime.TryParse(kvp.Key, out DateTime date))
                {
                    allDates.Add(date);
                }
            }
            // Add dates from the new date range
            allDates.AddRange(dateRange);

            // Sort all dates
            var sortedDates = allDates.OrderBy(d => d).Distinct().ToList();

            var currentConsecutive = 0;
            var consecutiveStartDates = new List<(DateTime date, double count)>();
            var consecutiveWorkDays = 0.0;

            for (int i = 0; i < sortedDates.Count; i++)
            {
                var date = sortedDates[i];
                var dateStr = date.ToString("yyyy-MM-dd");

                string assignedShift = "";
                if (combinedSchedule.ContainsKey(dateStr))
                {
                    assignedShift = combinedSchedule[dateStr];
                }
                else
                {
                    // If not in schedule, consider as rest day
                    assignedShift = RulesHelper.GetRestShiftName();
                }

                // Check consecutive work days (not rest days) - the rule is that consecutive work days
                // should not exceed MaxConsecutiveDays
                if (assignedShift != RulesHelper.GetRestShiftName() && !string.IsNullOrEmpty(assignedShift))
                {
                    // Check if this date is consecutive to the previous
                    if (i > 0 && date.Date == sortedDates[i - 1].AddDays(1).Date)
                    {
                        consecutiveWorkDays += GetShiftDayValue(assignedShift, rules);
                    }
                    else
                    {
                        // If the previous sequence was problematic, mark the dates for potential adjustment
                        if (consecutiveWorkDays > rules.MaxConsecutiveDays)
                        {
                            // Add dates from the problematic sequence (starting from where we can potentially insert a rest day)
                            var startIndex = i - (int)consecutiveWorkDays;
                            if (startIndex < 0) startIndex = 0;

                            // Select middle dates to convert to rest days to break the sequence
                            var daysToConvert = (int)(consecutiveWorkDays - rules.MaxConsecutiveDays) + 1;
                            for (int j = startIndex + (int)(consecutiveWorkDays / 2) - daysToConvert / 2;
                                 j < startIndex + (int)(consecutiveWorkDays / 2) + daysToConvert / 2 && j < i; j++)
                            {
                                if (j >= 0 && j < sortedDates.Count)
                                {
                                    var checkDate = sortedDates[j];
                                    var checkDateStr = checkDate.ToString("yyyy-MM-dd");

                                    // Only add if it's in our main date range and isn't already a rest day
                                    if (dateRange.Contains(checkDate) &&
                                        personSchedule.ContainsKey(checkDateStr) &&
                                        personSchedule[checkDateStr] != rules.RestShiftName &&
                                        !string.IsNullOrEmpty(personSchedule[checkDateStr]))
                                    {
                                        problematicDates.Add(checkDate);
                                    }
                                }
                            }
                        }

                        consecutiveWorkDays = GetShiftDayValue(assignedShift, rules);
                    }
                }
                else
                {
                    // If the previous sequence was problematic, mark the dates for potential adjustment
                    if (consecutiveWorkDays > rules.MaxConsecutiveDays)
                    {
                        // Add dates from the problematic sequence (starting from where we can potentially insert a rest day)
                        var endIndex = i - 1;
                        var startIndex = endIndex - (int)consecutiveWorkDays + 1;
                        if (startIndex < 0) startIndex = 0;

                        // Select middle dates to convert to rest days to break the sequence
                        var daysToConvert = (int)(consecutiveWorkDays - rules.MaxConsecutiveDays) + 1;
                        for (int j = startIndex + (int)(consecutiveWorkDays / 2) - daysToConvert / 2;
                             j < startIndex + (int)(consecutiveWorkDays / 2) + daysToConvert / 2 && j <= endIndex; j++)
                        {
                            if (j >= 0 && j < sortedDates.Count)
                            {
                                var checkDate = sortedDates[j];
                                var checkDateStr = checkDate.ToString("yyyy-MM-dd");

                                // Only add if it's in our main date range and isn't already a rest day
                                if (dateRange.Contains(checkDate) &&
                                    personSchedule.ContainsKey(checkDateStr) &&
                                    personSchedule[checkDateStr] != rules.RestShiftName &&
                                    !string.IsNullOrEmpty(personSchedule[checkDateStr]))
                                {
                                    problematicDates.Add(checkDate);
                                }
                            }
                        }
                    }

                    consecutiveWorkDays = 0;
                }
            }

            // Handle the last sequence if it was problematic
            if (consecutiveWorkDays > rules.MaxConsecutiveDays)
            {
                var endIndex = sortedDates.Count - 1;
                var startIndex = endIndex - (int)consecutiveWorkDays + 1;
                if (startIndex < 0) startIndex = 0;

                // Select middle dates to convert to rest days to break the sequence
                var daysToConvert = (int)(consecutiveWorkDays - rules.MaxConsecutiveDays) + 1;
                for (int j = startIndex + (int)(consecutiveWorkDays / 2) - daysToConvert / 2;
                     j < startIndex + (int)(consecutiveWorkDays / 2) + daysToConvert / 2 && j <= endIndex; j++)
                {
                    if (j >= 0 && j < sortedDates.Count)
                    {
                        var checkDate = sortedDates[j];
                        var checkDateStr = checkDate.ToString("yyyy-MM-dd");

                        // Only add if it's in our main date range and isn't already a rest day
                        if (dateRange.Contains(checkDate) &&
                            personSchedule.ContainsKey(checkDateStr) &&
                            personSchedule[checkDateStr] != rules.RestShiftName &&
                            !string.IsNullOrEmpty(personSchedule[checkDateStr]))
                        {
                            problematicDates.Add(checkDate);
                        }
                    }
                }
            }

            return problematicDates.Distinct().ToList();
        }

        // Method to rotate staff for fair distribution
        private List<StaffModel> RotateStaffForFairDistribution(
            List<StaffModel> eligibleStaff,
            DateTime date,
            Dictionary<string, StaffStatistics> staffStats,
            string? targetShiftType = null
        )
        {
            if (eligibleStaff.Count <= 1)
                return eligibleStaff;

            // Group staff by their count of the specific target shift type if provided,
            // otherwise by their total assigned shift count for general fairness
            var staffGroups = eligibleStaff
                .GroupBy(s =>
                {
                    if (targetShiftType != null && staffStats.ContainsKey(s.Name) && staffStats[s.Name].ShiftCounts.ContainsKey(targetShiftType))
                    {
                        return staffStats[s.Name].ShiftCounts[targetShiftType];
                    }
                    else
                    {
                        return staffStats.ContainsKey(s.Name) ? staffStats[s.Name].TotalAssigned : 0;
                    }
                })
                .OrderBy(g => g.Key); // Order groups by shift count (ascending - fewer shifts first)

            var result = new List<StaffModel>();

            // Process each group in order of increasing shift count
            foreach (var group in staffGroups)
            {
                var groupList = group.ToList();

                // Apply rotation within each group to maintain fairness for staff with same shift count
                if (groupList.Count > 1)
                {
                    var groupSeed = date.DayOfYear % groupList.Count;
                    var rotatedGroup = new List<StaffModel>();

                    // Add people starting from the rotation index within the group
                    for (int i = groupSeed; i < groupList.Count; i++)
                    {
                        rotatedGroup.Add(groupList[i]);
                    }
                    for (int i = 0; i < groupSeed; i++)
                    {
                        rotatedGroup.Add(groupList[i]);
                    }

                    result.AddRange(rotatedGroup);
                }
                else
                {
                    // If only one staff in the group, just add them
                    result.Add(groupList[0]);
                }
            }

            return result;
        }


        private bool IsHoliday(DateTime date, RulesModel rules)
        {
            // Check if in custom holidays list
            if (rules.CustomHolidays.Contains(date.ToString("yyyy-MM-dd")))
            {
                return true;
            }
            return false;
        }

        private double GetShiftDayValue(string shift, RulesModel? rules = null)
        {
            // Check if shift is in HalfDayShifts collection from rules
            if (rules != null && rules.HalfDayShifts.Contains(shift))
            {
                return 0.5;
            }
            return 1.0;
        }

        // Enhanced constraint check that includes both consecutive day checking and statistics tracking
        private bool CanAssignShiftWithConsecutiveCheckAndStats(
            string personName,
            string shift,
            DateTime date,
            Dictionary<string, ScheduleDataModel> scheduleData,
            List<ShiftModel> shifts,
            RulesModel rules,
            Dictionary<string, Dictionary<string, string>> existingSchedule
        )
        {
            var dateStr = date.ToString("yyyy-MM-dd");

            // Check if already assigned for this date
            if (scheduleData[personName].Shifts[dateStr] != "")
            {
                return false;
            }

            // Check consecutive work days including existing schedule before start date
            if (shift != rules.RestShiftName)
            {
                // Create a temporary schedule to test the assignment
                var tempScheduleData = new Dictionary<string, ScheduleDataModel>();
                foreach (var kvp in scheduleData)
                {
                    tempScheduleData[kvp.Key] = new ScheduleDataModel
                    {
                        Id = kvp.Value.Id,
                        Group = kvp.Value.Group,
                        Shifts = new Dictionary<string, string>(kvp.Value.Shifts),
                    };
                }
                tempScheduleData[personName].Shifts[dateStr] = shift;

                // Calculate consecutive work days considering both existing and new schedule
                return CalculateConsecutiveWorkDaysWithExistingSchedule(
                    personName,
                    date,
                    shift,
                    tempScheduleData,
                    existingSchedule,
                    rules
                );
            }

            return true;
        }

        // Enhanced rest assignment constraint check that follows specific consecutive day rules
        private int CanAssignRestWithConsecutiveCheckAndStats(
            string personName,
            DateTime date,
            Dictionary<string, ScheduleDataModel> scheduleData,
            RulesModel rules,
            Dictionary<string, Dictionary<string, string>> existingSchedule,
            Dictionary<string, StaffStatistics> staffStats
        )
        {
            int result = 0;
            var dateStr = date.ToString("yyyy-MM-dd");

            // Basic check: person must not be already assigned
            if (scheduleData[personName].Shifts[dateStr] != "")
            {
                return result;
            }
            else
            {
                result++;
            }

            // Create combined schedule view with existing and current schedules
            var combinedSchedule = new Dictionary<string, string>();

            // Add existing schedule data
            if (existingSchedule.ContainsKey(personName))
            {
                foreach (var kvp in existingSchedule[personName])
                {
                    combinedSchedule[kvp.Key] = kvp.Value;
                }
            }

            // Add new schedule data
            foreach (var kvp in scheduleData[personName].Shifts)
            {
                combinedSchedule[kvp.Key] = kvp.Value;
            }

            // Get date range for checking patterns
            var dateList = new List<DateTime>();
            foreach (var kvp in combinedSchedule)
            {
                if (DateTime.TryParse(kvp.Key, out DateTime parsedDate))
                {
                    dateList.Add(parsedDate);
                }
            }
            dateList.Add(date); // Add the target date we're checking
            dateList = dateList.OrderBy(d => d).ToList();

            // Get previous day assignment
            var previousDay = date.AddDays(-1);
            var previousDateStr = previousDay.ToString("yyyy-MM-dd");
            string previousShift = GetShiftForDate(previousDay, combinedSchedule);

            // Rule 1: If previous day was also rest, return false to prevent consecutive rest days
            var dayBeforePrevious = date.AddDays(-2);
            var dayBeforePreviousStr = dayBeforePrevious.ToString("yyyy-MM-dd");
            string dayBeforePreviousShift = GetShiftForDate(dayBeforePrevious, combinedSchedule);

            if (IsRestShift(previousShift))
            {
                result += PreFiveDaysWorkConsecutive(
                    personName,
                    previousDay,
                    combinedSchedule,
                    dateList
                );
                result += NextFiveDaysWorkConsecutive(personName, date, combinedSchedule, dateList);
                if (IsRestShift(dayBeforePreviousShift))
                    result -= rules.MaxConsecutiveDays;
            }
            else
            {
                result += PreFiveDaysWorkConsecutive(personName, date, combinedSchedule, dateList);
                result += NextFiveDaysWorkConsecutive(personName, date, combinedSchedule, dateList);
            }
            // Default case: return true if no specific rule blocks it
            return result;
        }

        private int PreFiveDaysWorkConsecutive(
            string personName,
            DateTime date,
            Dictionary<string, string> combinedSchedule,
            List<DateTime> dateList
        )
        {
            var previousDay = date.AddDays(-1);
            int consecutiveWorkDays = 0;
            for (var i = 0; i < 5; i++)
            {
                var checkDate = previousDay;
                var checkDateStr = checkDate.ToString("yyyy-MM-dd");
                string shift = GetShiftForDate(checkDate, combinedSchedule);
                if (IsRestShift(shift) || !string.IsNullOrEmpty(shift))
                    break;
                consecutiveWorkDays++;
                previousDay = previousDay.AddDays(-1);
            }
            return consecutiveWorkDays;
        }


        // Helper method to get shift for a specific date
        private string GetShiftForDate(DateTime date, Dictionary<string, string> schedule)
        {
            var dateStr = date.ToString("yyyy-MM-dd");
            if (schedule.ContainsKey(dateStr))
            {
                return schedule[dateStr];
            }
            return ""; // Empty if not assigned
        }

        // Helper method to check if a shift is a rest day
        private bool IsRestShift(string shift)
        {
            return shift == RulesHelper.GetRestShiftName() || shift == "休";
        }

        private int NextFiveDaysWorkConsecutive(
            string personName,
            DateTime date,
            Dictionary<string, string> combinedSchedule,
            List<DateTime> dateList
        )
        {
            int consecutiveWorkDays = 0;
            var nextDay = date.AddDays(1);
            for (var i = 0; i < 5; i++)
            {
                var checkDate = nextDay;
                var checkDateStr = checkDate.ToString("yyyy-MM-dd");
                string shift = GetShiftForDate(checkDate, combinedSchedule);
                if (IsRestShift(shift) || string.IsNullOrEmpty(shift))
                    break;
                consecutiveWorkDays++;

                nextDay = nextDay.AddDays(1);
            }

            return consecutiveWorkDays;
        }

        // Calculate consecutive work days considering both existing and new schedule
        private bool CalculateConsecutiveWorkDaysWithExistingSchedule(
            string personName,
            DateTime date,
            string shift,
            Dictionary<string, ScheduleDataModel> newScheduleData,
            Dictionary<string, Dictionary<string, string>> existingSchedule,
            RulesModel rules
        )
        {
            double maxConsecutive = 0;
            double currentEmptyConsecutive = 0;
            double currentConsecutive = 0;
            DateTime? lastAssignedDate = null;

            // Get a wider date range for calculation including existing schedule
            var dateRange = new List<DateTime>();

            // Create a combined schedule view that includes both existing and new schedules
            var combinedSchedule = new Dictionary<string, string>();

            // Add existing schedule data
            if (existingSchedule.ContainsKey(personName))
            {
                foreach (var kvp in existingSchedule[personName])
                {
                    combinedSchedule[kvp.Key] = kvp.Value;
                }
            }

            // Add new schedule data
            foreach (var kvp in newScheduleData[personName].Shifts)
            {
                combinedSchedule[kvp.Key] = kvp.Value;
            }

            // Get date range from a week before the current date to cover potential consecutive work days
            var startDate = date.AddDays(-1);
            var endDate = date.AddDays(-6);

            var currentDate = startDate;
            while (currentDate > endDate)
            {
                var dateStr = currentDate.ToString("yyyy-MM-dd");

                string assignedShift = "";
                if (combinedSchedule.ContainsKey(dateStr))
                {
                    assignedShift = combinedSchedule[dateStr];
                }

                if (string.IsNullOrEmpty(assignedShift))
                {
                    currentEmptyConsecutive += 1;
                    lastAssignedDate = currentDate;
                }
                else if (assignedShift != rules.RestShiftName && assignedShift != "休")
                {
                    if (
                        lastAssignedDate.HasValue
                        && currentDate.Date == lastAssignedDate.Value.AddDays(-1).Date
                    )
                    {
                        currentConsecutive += 1;
                    }
                    else
                    {
                        currentConsecutive = 1;
                    }
                    maxConsecutive = Math.Max(maxConsecutive, currentConsecutive);
                    lastAssignedDate = currentDate;
                }
                else
                {
                    currentEmptyConsecutive = 0;
                    currentConsecutive = 0;
                    lastAssignedDate = currentDate;
                    return true;
                }

                currentDate = currentDate.AddDays(-1);
            }
            if (
                rules.HalfDayShifts.Contains(shift)
                && (maxConsecutive >= (rules.MaxConsecutiveDays - 1))
            )
            {
                return false;
            }

            return !(maxConsecutive >= rules.MaxConsecutiveDays);
        }

        // Method to update statistics after each assignment
        private void UpdateStatisticsAfterAssignment(
            string personName,
            string shiftType,
            string dateStr,
            RulesModel rules,
            Dictionary<string, ScheduleDataModel> scheduleData,
            Dictionary<string, StaffStatistics> staffStats,
            Dictionary<string, DailyStatistics> dailyStats
        )
        {
            // Update staff statistics
            if (staffStats.ContainsKey(personName))
            {
                if (staffStats[personName].ShiftCounts.ContainsKey(shiftType))
                {
                    staffStats[personName].ShiftCounts[shiftType]++;
                    staffStats[personName].TotalAssigned++;
                }
                else
                {
                    staffStats[personName].ShiftCounts[shiftType] = 1;
                    staffStats[personName].TotalAssigned++;
                }

                // Update total counts
                if (rules.HalfDayShifts.Contains(shiftType))
                {
                    // For half-day shifts, count as 0.5 for work day calculations
                    double shiftDayValue = GetShiftDayValue(shiftType, rules);
                    staffStats[personName].ShiftCounts[rules.RestShiftName] += shiftDayValue;
                }
            }

            // Update daily statistics
            if (dailyStats.ContainsKey(dateStr))
            {
                if (dailyStats[dateStr].ShiftCounts.ContainsKey(shiftType))
                {
                    dailyStats[dateStr].ShiftCounts[shiftType]++;
                }
                else
                {
                    dailyStats[dateStr].ShiftCounts[shiftType] = 1;
                }

                dailyStats[dateStr].TotalAssigned++;
            }
        }


        // Helper method to check if rest days exceed the maximum interval
        private bool WouldExceedRestInterval(
            Dictionary<string, string> newPersonSchedule,
            List<DateTime> dateRange,
            string personName,
            Dictionary<string, Dictionary<string, string>> existingSchedule,
            RulesModel rules)
        {
            // Create a temporary combined view of the schedule including existing schedule
            var combinedSchedule = new Dictionary<string, string>();

            // Add existing schedule if provided
            if (existingSchedule != null && existingSchedule.ContainsKey(personName))
            {
                foreach (var kvp in existingSchedule[personName])
                {
                    combinedSchedule[kvp.Key] = kvp.Value;
                }
            }

            // Add new schedule data
            foreach (var kvp in newPersonSchedule)
            {
                combinedSchedule[kvp.Key] = kvp.Value;
            }

            // Get a broader date range to check across the boundary
            var allDates = new List<DateTime>();
            // Add dates from existing schedule
            foreach (var kvp in combinedSchedule)
            {
                if (DateTime.TryParse(kvp.Key, out DateTime date))
                {
                    allDates.Add(date);
                }
            }
            // Add dates from the new date range
            allDates.AddRange(dateRange);

            // Sort all dates
            var sortedDates = allDates.OrderBy(d => d).Distinct().ToList();

            double maxConsecutive = 0;
            double currentConsecutive = 0;

            for (int i = 0; i < sortedDates.Count; i++)
            {
                var date = sortedDates[i];
                var dateStr = date.ToString("yyyy-MM-dd");

                string assignedShift = "";
                if (combinedSchedule.ContainsKey(dateStr))
                {
                    assignedShift = combinedSchedule[dateStr];
                }
                else
                {
                    // If not in schedule, consider as rest day
                    assignedShift = RulesHelper.GetRestShiftName();
                }

                // Check consecutive work days (not rest days) - the rule is that consecutive work days
                // should not exceed MaxConsecutiveDays
                if (assignedShift != RulesHelper.GetRestShiftName() && !string.IsNullOrEmpty(assignedShift))
                {
                    // Check if this date is consecutive to the previous
                    if (i > 0 && date.Date == sortedDates[i - 1].AddDays(1).Date)
                    {
                        currentConsecutive += GetShiftDayValue(assignedShift, rules);
                    }
                    else
                    {
                        currentConsecutive = GetShiftDayValue(assignedShift, rules);
                    }
                    maxConsecutive = Math.Max(maxConsecutive, currentConsecutive);
                }
                else
                {
                    currentConsecutive = 0;
                }
            }

            return maxConsecutive > rules.MaxConsecutiveDays;
        }

        // Helper method to check MaxConsecutiveDays constraint after a change
        private bool CheckMaxConsecutiveDaysConstraint(
            Dictionary<string, string> newPersonSchedule,
            int maxConsecutiveDays,
            List<DateTime> dateRange,
            string personName,
            Dictionary<string, Dictionary<string, string>> existingSchedule = null!
        )
        {
            // Create a temporary combined view of the schedule including existing schedule
            var combinedSchedule = new Dictionary<string, string>();

            // Add existing schedule if provided
            if (existingSchedule != null && existingSchedule.ContainsKey(personName))
            {
                foreach (var kvp in existingSchedule[personName])
                {
                    combinedSchedule[kvp.Key] = kvp.Value;
                }
            }

            // Add new schedule data
            foreach (var kvp in newPersonSchedule)
            {
                combinedSchedule[kvp.Key] = kvp.Value;
            }

            // Get a broader date range to check across the boundary
            var allDates = new List<DateTime>();
            // Add dates from existing schedule
            foreach (var kvp in combinedSchedule)
            {
                if (DateTime.TryParse(kvp.Key, out DateTime date))
                {
                    allDates.Add(date);
                }
            }
            // Add dates from the new date range
            allDates.AddRange(dateRange);

            // Sort all dates
            var sortedDates = allDates.OrderBy(d => d).Distinct().ToList();

            double maxConsecutive = 0;
            double currentConsecutive = 0;

            for (int i = 0; i < sortedDates.Count; i++)
            {
                var date = sortedDates[i];
                var dateStr = date.ToString("yyyy-MM-dd");

                string assignedShift = "";
                if (combinedSchedule.ContainsKey(dateStr))
                {
                    assignedShift = combinedSchedule[dateStr];
                }
                else
                {
                    // If not in schedule, consider as rest day
                    assignedShift = RulesHelper.GetRestShiftName();
                }

                if (assignedShift != RulesHelper.GetRestShiftName() && assignedShift != "休")
                {
                    // Check if this date is consecutive to the previous
                    if (i > 0 && date.Date == sortedDates[i - 1].AddDays(1).Date)
                    {
                        currentConsecutive += GetShiftDayValue(assignedShift, null);
                    }
                    else
                    {
                        currentConsecutive = GetShiftDayValue(assignedShift, null);
                    }
                    maxConsecutive = Math.Max(maxConsecutive, currentConsecutive);
                }
                else
                {
                    currentConsecutive = 0;
                }
            }

            return maxConsecutive <= maxConsecutiveDays;
        }


        // Public method to generate schedule and return person-based data
        public Dictionary<string, ScheduleDataModel> GeneratePersonBasedSchedule(
            List<StaffModel> staffs,
            List<ShiftModel> shifts,
            RulesModel rules,
            DateTime startDate,
            DateTime endDate
        )
        {
            return GenerateScheduleWithPriorityAndStats(staffs, shifts, rules, startDate, endDate);
        }
    }
}
