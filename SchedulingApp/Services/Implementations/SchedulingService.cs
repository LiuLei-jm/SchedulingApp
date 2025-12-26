using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;

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
        private readonly DataService _dataService;

        public SchedulingService(DataService dataService)
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
                // Fallback to old weekday/holiday rules if new rules aren't available
                ProcessOldStyleRulesWithFullRequirements(
                    scheduleData,
                    dateRange,
                    staffs,
                    shifts,
                    rules,
                    existingSchedule
                );
            }

            // Handle any completely unassigned dates - assign default shifts based on rules
            foreach (var personName in scheduleData.Keys)
            {
                foreach (var date in dateRange)
                {
                    var dateStr = date.ToString("yyyy-MM-dd");
                    if (scheduleData[personName].Shifts[dateStr] == "")
                    {
                        // Default to rest if no assignment was made
                        scheduleData[personName].Shifts[dateStr] = "休息";
                    }
                }
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
            var dateBeforeStart = startDate.AddDays(-1); // Include the day before start date
            var itemsBeforeStart = allScheduleItems
                .Where(item =>
                {
                    if (DateTime.TryParse(item.Date, out DateTime itemDate))
                    {
                        return itemDate < startDate;
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

                        // Rotate staff to ensure fair distribution
                        var rotatedEligibleStaff = RotateStaffForFairDistribution(
                            unassignedEligibleStaff,
                            date,
                            staffStats
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
                                scheduleData[personName].Shifts[dateStr] = "休息";
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
                            && CanAssignShiftWithConsecutiveCheckAndStats(
                                personName,
                                shiftType,
                                nextDate,
                                scheduleData,
                                shifts,
                                rules,
                                existionSchedule
                            )
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
            var weekDayRestDays = averageRestDays - 1;
            var holidayRestDays = averageRestDays + 2;

            // For each employee, ensure their total "rest day equivalents" equal Rules.TotalRestDays
            // Where "休息" counts as 1.0 day and shifts in Rules.HalfDayShifts count as 0.5 days
            foreach (var person in applicableStaff)
            {
                var personName = person.Name;

                // Calculate the current rest day equivalents for this employee during the schedule period
                double currentRestDayEquivalents = 0.0;

                foreach (var date in dateRange)
                {
                    var dateStr = date.ToString("yyyy-MM-dd");
                    var shiftType = scheduleData[personName].Shifts[dateStr];

                    if (shiftType == "休息")
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

                // Calculate the difference needed to reach the target
                double difference = targetRestDays - currentRestDayEquivalents;

                if (difference > 0)
                {
                    // Need more rest day equivalents (can be achieved by adding rest days)
                    // We'll focus on converting only non-priority work days to rest days to preserve priority requirements
                    int restDaysNeeded = (int)Math.Ceiling(difference); // Round up to ensure we meet the requirement
                    var canAssignedRestDates = new Dictionary<string, int>();
                    var dateCanAssignedRest = new List<string>();
                    // Convert some non-priority work days to rest days ("休息")
                    foreach (var date in dateRange)
                    {
                        if (staffStats[personName].ShiftCounts["休息"] >= targetRestDays)
                            break;

                        var dateStr = date.ToString("yyyy-MM-dd");
                        var currentShift = scheduleData[personName].Shifts[dateStr];

                        // Skip if already a rest day
                        if (currentShift != "")
                            continue;

                        // Check if it's a priority shift to avoid disrupting priority requirements
                        var isHoliday = IsHoliday(date, rules);
                        var shiftRequirements = isHoliday
                            ? schedulingRule.HolidayShifts
                            : schedulingRule.WeekdayShifts;
                        var shiftRequirement = shiftRequirements.FirstOrDefault(sr =>
                            sr.ShiftName == currentShift
                        );
                        //var dailyMaxRestDays = isHoliday ? holidayRestDays : weekDayRestDays;
                        //if (dailyStats[dateStr].ShiftCounts["休息"] >= dailyMaxRestDays)
                        //    break;

                        canAssignedRestDates[dateStr] = CanAssignRestWithConsecutiveCheckAndStats(
                            personName,
                            date,
                            scheduleData,
                            rules,
                            existingSchedule,
                            staffStats
                        );
                    }

                    dateCanAssignedRest = canAssignedRestDates
                        .OrderByDescending(kv => kv.Value)
                        .Select(kv => kv.Key)
                        .ToList();

                    for (var i = 0; i < difference; i++)
                    {
                        var dateStr = dateCanAssignedRest[i];

                        if (!staffStats[personName].ShiftCounts.ContainsKey("休息"))
                        {
                            staffStats[personName].ShiftCounts["休息"] = 0;
                        }
                        // Assign rest
                        scheduleData[personName].Shifts[dateStr] = "休息";
                        UpdateStatisticsAfterAssignment(
                            personName,
                            "休息",
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

        // Method to rotate staff for fair distribution
        private List<StaffModel> RotateStaffForFairDistribution(
            List<StaffModel> eligibleStaff,
            DateTime date,
            Dictionary<string, StaffStatistics> staffStats
        )
        {
            if (eligibleStaff.Count <= 1)
                return eligibleStaff;

            // Group staff by their total assigned shift count to maintain fairness
            var staffGroups = eligibleStaff
                .GroupBy(s => staffStats.ContainsKey(s.Name) ? staffStats[s.Name].TotalAssigned : 0)
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

        private void ProcessOldStyleRulesWithFullRequirements(
            Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange,
            List<StaffModel> staff,
            List<ShiftModel> shifts,
            RulesModel rules,
            Dictionary<string, Dictionary<string, string>> existingSchedule
        )
        {
            Dictionary<string, StaffStatistics> staffStats = [];
            Dictionary<string, DailyStatistics> dailyStats = [];

            foreach (var person in staff)
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
                staffStats[person.Name].ShiftCounts["休息"] = 0;
            }

            foreach (var date in dateRange)
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                dailyStats[dateStr] = new DailyStatistics
                {
                    Date = dateStr,
                    ShiftCounts = new Dictionary<string, double>(),
                    TotalAssigned = 0,
                    TotalAvailable = staff.Count,
                };

                // Initialize shift counts for each shift type
                foreach (var shift in shifts)
                {
                    dailyStats[dateStr].ShiftCounts[shift.ShiftName] = 0;
                }
                dailyStats[dateStr].ShiftCounts["休息"] = 0;
            }

            // Process each date independently
            foreach (var date in dateRange)
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                var isHoliday = IsHoliday(date, rules);

                // Get shift requirements based on whether it's a holiday or weekday
                var shiftRequirements = isHoliday ? rules.Holiday : rules.Weekday;

                // Sort shifts by priority: priority shifts first (lower numbers = higher priority), null priorities come last
                var sortedShiftRequirements = shiftRequirements
                    .OrderBy(req => req.Priority ?? int.MaxValue) // Null priorities come last
                    .ToList();

                var priorityShifts = sortedShiftRequirements
                    .Where(req => req.Priority.HasValue)
                    .ToList();
                var nonPriorityShifts = sortedShiftRequirements
                    .Where(req => !req.Priority.HasValue)
                    .ToList();

                // Step 1: Process priority shifts (must reach exact required count)
                foreach (var shiftRequirement in priorityShifts)
                {
                    var shiftType = shiftRequirement.ShiftName;
                    var requiredCount = shiftRequirement.RequiredCount ?? 0;

                    // Create a list of eligible staff for this shift assignment
                    var eligibleStaff = new List<StaffModel>();
                    foreach (var person in staff)
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
                            eligibleStaff.Add(person);
                        }
                    }

                    // Rotate staff to ensure fair distribution
                    var rotatedEligibleStaff = RotateStaffForFairDistribution(
                        eligibleStaff,
                        date,
                        staffStats
                    );

                    // Assign exactly the required count of this shift
                    int assigned = 0;
                    foreach (var person in rotatedEligibleStaff)
                    {
                        if (assigned >= requiredCount)
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
                            assigned++;
                        }
                    }
                }

                // Step 2: Process non-priority shifts after priority shifts
                foreach (var shiftRequirement in nonPriorityShifts)
                {
                    var shiftType = shiftRequirement.ShiftName;
                    var requiredCount = shiftRequirement.RequiredCount ?? 0;

                    // Get unassigned staff who are eligible for this shift
                    var remainingUnassigned = staff
                        .Where(person => scheduleData[person.Name].Shifts[dateStr] == "")
                        .ToList();

                    var eligibleStaff = new List<StaffModel>();
                    foreach (var person in remainingUnassigned)
                    {
                        var personName = person.Name;
                        if (
                            CanAssignShiftWithConsecutiveCheckAndStats(
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
                            eligibleStaff.Add(person);
                        }
                    }

                    // Rotate staff for fair distribution
                    var rotatedEligibleStaff = RotateStaffForFairDistribution(
                        eligibleStaff,
                        date,
                        staffStats
                    );

                    // Assign up to the required count (flexible assignment)
                    int assigned = 0;
                    foreach (var person in rotatedEligibleStaff)
                    {
                        if (assigned >= requiredCount)
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
                            assigned++;
                        }
                    }
                }

                // Step 3: Assign non-priority shifts first, then rest days only up to TotalRestDays requirement
                var stillUnassigned = staff
                    .Where(person => scheduleData[person.Name].Shifts[dateStr] == "")
                    .ToList();

                // After priority assignments, only assign rest days up to TotalRestDays requirement
                // and fill remaining slots with non-priority shifts
                foreach (var person in stillUnassigned)
                {
                    var personName = person.Name;

                    // Check if this person has reached their TotalRestDays requirement
                    int currentRestDays = 0;
                    foreach (var checkDate in dateRange)
                    {
                        var checkDateStr = checkDate.ToString("yyyy-MM-dd");
                        if (
                            scheduleData.ContainsKey(personName)
                            && scheduleData[personName].Shifts.ContainsKey(checkDateStr)
                            && scheduleData[personName].Shifts[checkDateStr] == "休息"
                        )
                        {
                            currentRestDays++;
                        }
                    }

                    // Check if this person has already reached their TotalRestDays including existing schedule
                    int existingRestDays = 0;
                    if (existingSchedule.ContainsKey(personName))
                    {
                        foreach (var kvp in existingSchedule[personName])
                        {
                            if (kvp.Value == "休息")
                            {
                                existingRestDays++;
                            }
                        }
                    }

                    // Check if we should assign rest or a non-priority shift based on TotalRestDays
                    if (currentRestDays + existingRestDays < rules.TotalRestDays)
                    {
                        // Can assign a rest day
                        scheduleData[personName].Shifts[dateStr] = "休息";
                        UpdateStatisticsAfterAssignment(
                            personName,
                            "休息",
                            dateStr,
                            rules,
                            scheduleData,
                            staffStats,
                            dailyStats
                        );
                    }
                    else
                    {
                        // Need to assign a non-priority shift instead of rest
                        // First, find a non-priority shift that's still needed for this date
                        bool shiftAssigned = false;

                        foreach (var shiftRequirement in nonPriorityShifts)
                        {
                            var shiftType = shiftRequirement.ShiftName;
                            var requiredCount = shiftRequirement.RequiredCount ?? 0;

                            // Count how many people are already assigned this shift type for this date
                            int currentAssignments = 0;
                            foreach (var p in staff)
                            {
                                var pName = p.Name;
                                if (
                                    scheduleData.ContainsKey(pName)
                                    && scheduleData[pName].Shifts.ContainsKey(dateStr)
                                    && scheduleData[pName].Shifts[dateStr] == shiftType
                                )
                                {
                                    currentAssignments++;
                                }
                            }

                            // If we need more of this shift and it's eligible for this person
                            if (
                                currentAssignments < requiredCount
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
                                shiftAssigned = true;
                                break;
                            }
                        }

                        // If no non-priority shift could be assigned, assign rest anyway (but this should be rare)
                        if (!shiftAssigned)
                        {
                            scheduleData[personName].Shifts[dateStr] = "休息";
                            UpdateStatisticsAfterAssignment(
                                personName,
                                "休息",
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

            // After all dates are processed, check TotalRestDays constraint
            EnforceTotalRestDaysRequirement(
                scheduleData,
                dateRange,
                staff,
                rules,
                existingSchedule
            );

            // Handle half-day shift consecutive arrangement for shifts like 甲2PLUS
            HandleHalfDayShiftConsecutiveArrangement(scheduleData, dateRange, staff, shifts, rules);
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
            if (shift != "休息")
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

        private bool CheckDateForRange(DateTime date, Dictionary<string, string> schedule)
        {
            var dateStr = date.ToString("yyyy-MM-dd");
            return schedule.ContainsKey(dateStr);
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
            return shift == "休息" || shift == "休";
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
                else if (assignedShift != "休息" && assignedShift != "休")
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
                    staffStats[personName].ShiftCounts["休息"] += shiftDayValue;
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

        // Method to enforce TotalRestDays requirement for each employee
        private void EnforceTotalRestDaysRequirement(
            Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange,
            List<StaffModel> applicableStaff,
            RulesModel rules,
            Dictionary<string, Dictionary<string, string>> existingSchedule
        )
        {
            foreach (var person in applicableStaff)
            {
                var personName = person.Name;

                // Count current rest days for this person in the new schedule period only
                int currentRestDays = 0;
                foreach (var date in dateRange)
                {
                    var dateStr = date.ToString("yyyy-MM-dd");
                    if (
                        scheduleData.ContainsKey(personName)
                        && scheduleData[personName].Shifts.ContainsKey(dateStr)
                        && scheduleData[personName].Shifts[dateStr] == "休息"
                    )
                    {
                        currentRestDays++;
                    }
                }

                // Calculate how many rest days are still needed to reach the target
                int restDaysNeeded = rules.TotalRestDays - currentRestDays;

                // If more rest days are needed, convert some work days to rest days
                if (restDaysNeeded > 0)
                {
                    // Find work days for this person that we can convert to rest days
                    var workDays = new List<DateTime>();
                    foreach (var date in dateRange)
                    {
                        var dateStr = date.ToString("yyyy-MM-dd");
                        if (
                            scheduleData.ContainsKey(personName)
                            && scheduleData[personName].Shifts.ContainsKey(dateStr)
                            && scheduleData[personName].Shifts[dateStr] != "休息"
                            && !string.IsNullOrEmpty(scheduleData[personName].Shifts[dateStr])
                        )
                        {
                            workDays.Add(date);
                        }
                    }

                    // Convert some work days to rest days, considering MaxConsecutiveDays constraint
                    int converted = 0;
                    foreach (var date in workDays)
                    {
                        if (converted >= restDaysNeeded)
                            break;

                        var dateStr = date.ToString("yyyy-MM-dd");
                        var originalShift = scheduleData[personName].Shifts[dateStr];

                        // Temporarily change to rest day to test the constraint
                        scheduleData[personName].Shifts[dateStr] = "休息";

                        // Check if this change violates MaxConsecutiveDays constraint
                        if (
                            CheckMaxConsecutiveDaysConstraint(
                                scheduleData[personName].Shifts,
                                rules.MaxConsecutiveDays,
                                dateRange,
                                personName,
                                existingSchedule
                            )
                        )
                        {
                            // Constraint is satisfied, keep the rest day
                            converted++;
                        }
                        else
                        {
                            // Constraint violated, revert the change
                            scheduleData[personName].Shifts[dateStr] = originalShift;
                        }
                    }
                }
                // If too many rest days have been assigned and need to be reduced, it's more complex
                // For now, let's focus on meeting the minimum requirement
            }
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
                    assignedShift = "休息";
                }

                if (assignedShift != "休息" && !string.IsNullOrEmpty(assignedShift))
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

        // Method to handle half-day shift consecutive arrangement for shifts like 甲2PLUS
        private void HandleHalfDayShiftConsecutiveArrangement(
            Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange,
            List<StaffModel> applicableStaff,
            List<ShiftModel> shifts,
            RulesModel rules
        )
        {
            // Process each half-day shift type from the rules
            foreach (var halfDayShift in rules.HalfDayShifts)
            {
                // For each day, try to arrange consecutive half-day shifts for the same person when beneficial
                for (int i = 0; i < dateRange.Count - 1; i++) // -1 to have the next day available
                {
                    var currentDate = dateRange[i];
                    var nextDate = dateRange[i + 1];
                    var currentDateStr = currentDate.ToString("yyyy-MM-dd");
                    var nextDateStr = nextDate.ToString("yyyy-MM-dd");

                    // Look for opportunities to swap assignments to create consecutive half-day shifts for the same person
                    foreach (var person in applicableStaff)
                    {
                        var personName = person.Name;

                        // Case 1: Person has half-day shift on current day and rest on next day,
                        // and someone else has a work shift on the next day they could swap with
                        if (
                            scheduleData.ContainsKey(personName)
                            && scheduleData[personName].Shifts.ContainsKey(currentDateStr)
                            && scheduleData[personName].Shifts[currentDateStr] == halfDayShift
                            && scheduleData[personName].Shifts.ContainsKey(nextDateStr)
                            && scheduleData[personName].Shifts[nextDateStr] == "休息"
                        )
                        {
                            // Look for another person who has a work shift on the next day
                            foreach (var otherPerson in applicableStaff)
                            {
                                var otherPersonName = otherPerson.Name;
                                if (
                                    otherPersonName != personName
                                    && scheduleData.ContainsKey(otherPersonName)
                                    && scheduleData[otherPersonName].Shifts.ContainsKey(nextDateStr)
                                    && scheduleData[otherPersonName].Shifts[nextDateStr] != "休息"
                                    && scheduleData[otherPersonName].Shifts[nextDateStr]
                                        != halfDayShift
                                )
                                {
                                    // Check if we can swap: person gets half-day shift on next day,
                                    // and other person gets original person's assignment (rest)
                                    var originalOtherShift = scheduleData[otherPersonName].Shifts[
                                        nextDateStr
                                    ];

                                    // Create temporary schedules to test constraints
                                    var tempSchedulePerson = new Dictionary<string, string>(
                                        scheduleData[personName].Shifts
                                    );
                                    var tempScheduleOther = new Dictionary<string, string>(
                                        scheduleData[otherPersonName].Shifts
                                    );

                                    // Apply potential swap
                                    tempSchedulePerson[nextDateStr] = halfDayShift; // Person gets consecutive half-day
                                    tempScheduleOther[nextDateStr] = "休息"; // Other person gets rest day

                                    // Check constraints for both people
                                    bool personValid = CheckMaxConsecutiveDaysConstraint(
                                        tempSchedulePerson,
                                        rules.MaxConsecutiveDays,
                                        dateRange,
                                        personName
                                    );
                                    bool otherValid = CheckMaxConsecutiveDaysConstraint(
                                        tempScheduleOther,
                                        rules.MaxConsecutiveDays,
                                        dateRange,
                                        otherPersonName
                                    );

                                    if (personValid && otherValid)
                                    {
                                        // Perform the swap if it's beneficial (especially for 甲2PLUS)
                                        if (halfDayShift == "甲2PLUS")
                                        {
                                            scheduleData[personName].Shifts[nextDateStr] =
                                                halfDayShift;
                                            scheduleData[otherPersonName].Shifts[nextDateStr] =
                                                "休息";
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
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
