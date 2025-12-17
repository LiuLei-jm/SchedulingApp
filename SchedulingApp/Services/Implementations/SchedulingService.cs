using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;

namespace SchedulingApp.Services.Implementations
{
    // Statistics classes for tracking row and column data
    public class StaffStatistics
    {
        public string StaffName { get; set; } = string.Empty;
        public Dictionary<string, double> ShiftCounts { get; set; } = [];
        public double TotalShiftDays { get; set; }
        public double TotalWorkDays { get; set; }
        public double TotalRestDays { get; set; }
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
            List<StaffModel> staffs,
            List<ShiftModel> shifts,
            RulesModel rules,
            DateTime startDate,
            DateTime endDate)
        {
            var scheduleData = new Dictionary<string, ScheduleDataModel>();

            // Initialize schedule data for each staff member
            foreach (var person in staffs)
            {
                scheduleData[person.Name] = new ScheduleDataModel
                {
                    Id = person.Id,
                    Group = person.Group,
                    Shifts = new Dictionary<string, string>()
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
                    ProcessSchedulingRuleWithFullRequirements(scheduleData, dateRange, staffs, shifts, rules, schedulingRule, existingSchedule);
                }
            }
            else
            {
                // Fallback to old weekday/holiday rules if new rules aren't available
                ProcessOldStyleRulesWithFullRequirements(scheduleData, dateRange, staffs, shifts, rules, existingSchedule);
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
        private Dictionary<string, Dictionary<string, string>> LoadExistingScheduleBefore(DateTime startDate, List<StaffModel> staff)
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
            var itemsBeforeStart = allScheduleItems.Where(item =>
            {
                if (DateTime.TryParse(item.Date, out DateTime itemDate))
                {
                    return itemDate < startDate;
                }
                return false;
            }).ToList();

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

        private void ProcessShiftsByRulesPriority(Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> staff, List<ShiftModel> shifts, RulesModel rules,
            Dictionary<string, Dictionary<string, string>> existingSchedule)
        {
            // Process scheduling rules from the new SchedulingRules collection if available
            if (rules.SchedulingRules != null && rules.SchedulingRules.Count > 0)
            {
                // Process each scheduling rule independently
                foreach (var schedulingRule in rules.SchedulingRules)
                {
                    ProcessSchedulingRuleWithFullRequirements(scheduleData, dateRange, staff, shifts, rules, schedulingRule, existingSchedule);
                }
            }
            else
            {
                // Fallback to old weekday/holiday rules if new rules aren't available
                ProcessOldStyleRules(scheduleData, dateRange, staff, shifts, rules, existingSchedule);
            }
        }

        private void ProcessSchedulingRuleWithFullRequirements(Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> staffs, List<ShiftModel> shifts,
            RulesModel rules, SchedulingRuleModel schedulingRule,
            Dictionary<string, Dictionary<string, string>> existingSchedule)
        {
            var applicableStaff = schedulingRule.ApplicableStaff?.Any() == true
                ? staffs.Where(s => schedulingRule.ApplicableStaff.Contains(s.Name)).ToList()
                : staffs.ToList();

            // Initialize statistics tracking for this rule
            var staffStats = new Dictionary<string, StaffStatistics>();
            var dailyStats = new Dictionary<string, DailyStatistics>();

            foreach (var person in applicableStaff)
            {
                staffStats[person.Name] = new StaffStatistics
                {
                    StaffName = person.Name,
                    ShiftCounts = new Dictionary<string, double>(),
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

            foreach (var date in dateRange)
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                dailyStats[dateStr] = new DailyStatistics
                {
                    Date = dateStr,
                    ShiftCounts = new Dictionary<string, double>(),
                    TotalAssigned = 0,
                    TotalAvailable = applicableStaff.Count
                };

                // Initialize shift counts for each shift type
                foreach (var shift in shifts)
                {
                    dailyStats[dateStr].ShiftCounts[shift.ShiftName] = 0;
                }
            }

            // Process each date independently per the requirement: each scheduling rule is independent
            foreach (var date in dateRange)
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                var isHoliday = IsHoliday(date, rules);

                // Get shift requirements based on whether it's a holiday or weekday
                var shiftRequirements = isHoliday ? schedulingRule.HolidayShifts : schedulingRule.WeekdayShifts;

                // Sort shifts by priority: priority shifts first (lower numbers = higher priority), null priorities come last
                var sortedShiftRequirements = shiftRequirements
                    .OrderBy(req => req.Priority ?? int.MaxValue) // Null priorities come last
                    .ToList();

                var priorityShifts = sortedShiftRequirements.Where(req => req.Priority.HasValue).ToList();
                var nonPriorityShifts = sortedShiftRequirements.Where(req => !req.Priority.HasValue).ToList();

                // Step 1: Process priority shifts (must reach exact required count)
                // These have higher priority numbers and must be satisfied exactly
                foreach (var shiftRequirement in priorityShifts)
                {
                    var shiftType = shiftRequirement.ShiftName;
                    var requiredCount = shiftRequirement.RequiredCount;

                    // Create a list of eligible staff for this shift assignment
                    var eligibleStaff = new List<StaffModel>();
                    foreach (var person in applicableStaff)
                    {
                        var personName = person.Name;
                        if (scheduleData[personName].Shifts[dateStr] == "" &&
                            CanAssignShiftWithConsecutiveCheckAndStats(personName, shiftType, date, scheduleData, shifts, rules, existingSchedule, staffStats))
                        {
                            eligibleStaff.Add(person);
                        }
                    }

                    // Rotate staff to ensure fair distribution
                    var rotatedEligibleStaff = RotateStaffForFairDistribution(eligibleStaff, date);

                    // Assign exactly the required count of this shift
                    int assigned = 0;
                    foreach (var person in rotatedEligibleStaff)
                    {
                        if (assigned >= requiredCount)
                            break;

                        var personName = person.Name;
                        if (scheduleData[personName].Shifts[dateStr] == "" &&
                            CanAssignShiftWithConsecutiveCheckAndStats(personName, shiftType, date, scheduleData, shifts, rules, existingSchedule, staffStats))
                        {
                            // Perform assignment
                            scheduleData[personName].Shifts[dateStr] = shiftType;

                            // Update statistics
                            UpdateStatisticsAfterAssignment(personName, shiftType, dateStr, scheduleData, staffStats, dailyStats);
                            assigned++;
                        }
                    }
                }

                // Step 2: Process non-priority shifts after priority shifts
                foreach (var shiftRequirement in nonPriorityShifts)
                {
                    var shiftType = shiftRequirement.ShiftName;
                    var requiredCount = shiftRequirement.RequiredCount;

                    // Get unassigned staff who are eligible for this shift
                    var remainingUnassigned = applicableStaff.Where(person =>
                        scheduleData[person.Name].Shifts[dateStr] == "").ToList();

                    var eligibleStaff = new List<StaffModel>();
                    foreach (var person in remainingUnassigned)
                    {
                        var personName = person.Name;
                        if (CanAssignShiftWithConsecutiveCheckAndStats(personName, shiftType, date, scheduleData, shifts, rules, existingSchedule, staffStats))
                        {
                            eligibleStaff.Add(person);
                        }
                    }

                    // Rotate staff for fair distribution
                    var rotatedEligibleStaff = RotateStaffForFairDistribution(eligibleStaff, date);

                    // Assign up to the required count (flexible assignment)
                    int assigned = 0;
                    foreach (var person in rotatedEligibleStaff)
                    {
                        if (assigned >= requiredCount)
                            break;

                        var personName = person.Name;
                        if (scheduleData[personName].Shifts[dateStr] == "" &&
                            CanAssignShiftWithConsecutiveCheckAndStats(personName, shiftType, date, scheduleData, shifts, rules, existingSchedule, staffStats))
                        {
                            // Perform assignment
                            scheduleData[personName].Shifts[dateStr] = shiftType;

                            // Update statistics
                            UpdateStatisticsAfterAssignment(personName, shiftType, dateStr, scheduleData, staffStats, dailyStats);
                            assigned++;
                        }
                    }
                }

                // Step 3: Assign non-priority shifts first, then rest days only up to TotalRestDays requirement
                var stillUnassigned = applicableStaff.Where(person =>
                    scheduleData[person.Name].Shifts[dateStr] == "").ToList();

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
                        if (scheduleData.ContainsKey(personName) &&
                            scheduleData[personName].Shifts.ContainsKey(checkDateStr) &&
                            scheduleData[personName].Shifts[checkDateStr] == "休息")
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
                        UpdateStatisticsAfterAssignment(personName, "休息", dateStr, scheduleData, staffStats, dailyStats);
                    }
                    else
                    {
                        // Need to assign a non-priority shift instead of rest
                        // First, find a non-priority shift that's still needed for this date
                        bool shiftAssigned = false;

                        foreach (var shiftRequirement in nonPriorityShifts)
                        {
                            var shiftType = shiftRequirement.ShiftName;
                            var requiredCount = shiftRequirement.RequiredCount;

                            // Count how many people are already assigned this shift type for this date
                            int currentAssignments = 0;
                            foreach (var p in applicableStaff)
                            {
                                var pName = p.Name;
                                if (scheduleData.ContainsKey(pName) &&
                                    scheduleData[pName].Shifts.ContainsKey(dateStr) &&
                                    scheduleData[pName].Shifts[dateStr] == shiftType)
                                {
                                    currentAssignments++;
                                }
                            }

                            // If we need more of this shift and it's eligible for this person
                            if (currentAssignments < requiredCount &&
                                CanAssignShiftWithConsecutiveCheckAndStats(personName, shiftType, date, scheduleData, shifts, rules, existingSchedule, staffStats))
                            {
                                scheduleData[personName].Shifts[dateStr] = shiftType;
                                UpdateStatisticsAfterAssignment(personName, shiftType, dateStr, scheduleData, staffStats, dailyStats);
                                shiftAssigned = true;
                                break;
                            }
                        }

                        // If no non-priority shift could be assigned, assign rest anyway (but this should be rare)
                        if (!shiftAssigned)
                        {
                            scheduleData[personName].Shifts[dateStr] = "休息";
                            UpdateStatisticsAfterAssignment(personName, "休息", dateStr, scheduleData, staffStats, dailyStats);
                        }
                    }
                }
            }

            // After all dates are processed, check TotalRestDays constraint
            // This should be handled during assignment, but we'll make sure it's met
            EnforceTotalRestDaysRequirement(scheduleData, dateRange, applicableStaff, rules, existingSchedule);

            // Handle half-day shift consecutive arrangement for shifts like 甲2PLUS
            HandleHalfDayShiftConsecutiveArrangement(scheduleData, dateRange, applicableStaff, shifts, rules);
        }

        private void ProcessOldStyleRules(Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> staff, List<ShiftModel> shifts, RulesModel rules,
            Dictionary<string, Dictionary<string, string>> existingSchedule)
        {
            // Initialize statistics tracking for old-style rules
            var staffStats = new Dictionary<string, StaffStatistics>();
            var dailyStats = new Dictionary<string, DailyStatistics>();

            foreach (var person in staff)
            {
                staffStats[person.Name] = new StaffStatistics
                {
                    StaffName = person.Name,
                    ShiftCounts = new Dictionary<string, double>(),
                    TotalShiftDays = 0,
                    TotalWorkDays = 0,
                    TotalRestDays = 0
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
                    TotalAvailable = staff.Count
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

                // Sort shifts by priority (lower numbers = higher priority, null priorities come last and have lowest priority)
                var sortedShiftRequirements = shiftRequirements
                    .OrderBy(req => req.Priority ?? int.MaxValue) // Null priorities come last
                    .ToList();

                var priorityShifts = sortedShiftRequirements.Where(req => req.Priority.HasValue).ToList();
                var nonPriorityShifts = sortedShiftRequirements.Where(req => !req.Priority.HasValue).ToList();

                // Process priority shifts first (must reach exact required count)
                foreach (var shiftRequirement in priorityShifts)
                {
                    var shiftType = shiftRequirement.ShiftName;
                    var requiredCount = shiftRequirement.RequiredCount;

                    // Create a list of eligible staff for this shift assignment
                    var eligibleStaff = new List<StaffModel>();
                    foreach (var person in staff)
                    {
                        var personName = person.Name;
                        if (scheduleData[personName].Shifts[dateStr] == "" &&
                            CanAssignShiftWithConsecutiveCheckAndStats(personName, shiftType, date, scheduleData, shifts, rules, existingSchedule, staffStats))
                        {
                            eligibleStaff.Add(person);
                        }
                    }

                    // Rotate staff to ensure fair distribution
                    var rotatedEligibleStaff = RotateStaffForFairDistribution(eligibleStaff, date);

                    // Assign exactly the required count of this shift
                    int assigned = 0;
                    foreach (var person in rotatedEligibleStaff)
                    {
                        if (assigned >= requiredCount)
                            break;

                        var personName = person.Name;
                        if (scheduleData[personName].Shifts[dateStr] == "" &&
                            CanAssignShiftWithConsecutiveCheckAndStats(personName, shiftType, date, scheduleData, shifts, rules, existingSchedule, staffStats))
                        {
                            // Perform assignment
                            scheduleData[personName].Shifts[dateStr] = shiftType;

                            // Update statistics
                            UpdateStatisticsAfterAssignment(personName, shiftType, dateStr, scheduleData, staffStats, dailyStats);
                            assigned++;
                        }
                    }
                }

                // Process non-priority shifts after priority shifts
                foreach (var shiftRequirement in nonPriorityShifts)
                {
                    var shiftType = shiftRequirement.ShiftName;
                    var requiredCount = shiftRequirement.RequiredCount;

                    // Get unassigned staff who are eligible for this shift
                    var remainingUnassigned = staff.Where(person =>
                        scheduleData[person.Name].Shifts[dateStr] == "").ToList();

                    var eligibleStaff = new List<StaffModel>();
                    foreach (var person in remainingUnassigned)
                    {
                        var personName = person.Name;
                        if (CanAssignShiftWithConsecutiveCheckAndStats(personName, shiftType, date, scheduleData, shifts, rules, existingSchedule, staffStats))
                        {
                            eligibleStaff.Add(person);
                        }
                    }

                    // Rotate staff for fair distribution
                    var rotatedEligibleStaff = RotateStaffForFairDistribution(eligibleStaff, date);

                    // Assign up to the required count (flexible assignment)
                    int assigned = 0;
                    foreach (var person in rotatedEligibleStaff)
                    {
                        if (assigned >= requiredCount)
                            break;

                        var personName = person.Name;
                        if (scheduleData[personName].Shifts[dateStr] == "" &&
                            CanAssignShiftWithConsecutiveCheckAndStats(personName, shiftType, date, scheduleData, shifts, rules, existingSchedule, staffStats))
                        {
                            // Perform assignment
                            scheduleData[personName].Shifts[dateStr] = shiftType;

                            // Update statistics
                            UpdateStatisticsAfterAssignment(personName, shiftType, dateStr, scheduleData, staffStats, dailyStats);
                            assigned++;
                        }
                    }
                }

                // Assign rest days to any remaining unassigned staff
                var stillUnassigned = staff.Where(person =>
                    scheduleData[person.Name].Shifts[dateStr] == "").ToList();

                foreach (var person in stillUnassigned)
                {
                    var personName = person.Name;
                    scheduleData[personName].Shifts[dateStr] = "休息";
                    UpdateStatisticsAfterAssignment(personName, "休息", dateStr, scheduleData, staffStats, dailyStats);
                }
            }

            // After all dates are processed, check TotalRestDays constraint
            EnforceTotalRestDaysRequirement(scheduleData, dateRange, staff, rules, existingSchedule);

            // Handle half-day shift consecutive arrangement for shifts like 甲2PLUS
            HandleHalfDayShiftConsecutiveArrangement(scheduleData, dateRange, staff, shifts, rules);
        }

        private void AssignExactShiftCount(Dictionary<string, ScheduleDataModel> scheduleData, DateTime date,
            string shift, int requiredCount, List<StaffModel> availableStaff, List<ShiftModel> shifts, RulesModel rules)
        {
            var dateStr = date.ToString("yyyy-MM-dd");
            var eligibleStaff = new List<StaffModel>();

            // Find eligible staff for this shift who are not yet assigned for this date
            foreach (var person in availableStaff)
            {
                var personName = person.Name;

                // Check if this person is still unassigned for this date
                if (scheduleData[personName].Shifts[dateStr] != "")
                    continue;

                // Apply constraints
                if (CanAssignShiftWithConsecutiveCheck(personName, shift, date, scheduleData, shifts, rules))
                {
                }
            }

            // Rotate staff to ensure fair distribution over time
            // Use a simple rotation based on date to ensure different people get the priority shifts on different days
            var rotatedEligibleStaff = RotateStaffForFairDistribution(eligibleStaff, date);

            // Assign the required count of shifts
            int assigned = 0;
            foreach (var person in rotatedEligibleStaff)
            {
                if (assigned >= requiredCount)
                    break;

                // Double-check constraints before assignment
                if (CanAssignShiftWithConsecutiveCheck(person.Name, shift, date, scheduleData, shifts, rules))
                {
                    scheduleData[person.Name].Shifts[dateStr] = shift;
                    assigned++;
                }
            }
        }

        // Method to rotate staff for fair distribution
        private List<StaffModel> RotateStaffForFairDistribution(List<StaffModel> eligibleStaff, DateTime date)
        {
            if (eligibleStaff.Count <= 1)
                return eligibleStaff;

            // Use day of year as a rotation seed to rotate assignment
            var seed = date.DayOfYear % eligibleStaff.Count;
            var rotated = new List<StaffModel>();

            // Add people starting from the rotation index
            for (int i = seed; i < eligibleStaff.Count; i++)
            {
                rotated.Add(eligibleStaff[i]);
            }
            for (int i = 0; i < seed; i++)
            {
                rotated.Add(eligibleStaff[i]);
            }

            return rotated;
        }

        private void AssignShiftCountFlexible(Dictionary<string, ScheduleDataModel> scheduleData, DateTime date,
            string shift, int requiredCount, List<StaffModel> availableStaff, List<ShiftModel> shifts, RulesModel rules)
        {
            var dateStr = date.ToString("yyyy-MM-dd");
            var eligibleStaff = new List<StaffModel>();

            // Find eligible staff for this shift who are not yet assigned for this date
            foreach (var person in availableStaff)
            {
                var personName = person.Name;

                // Check if this person is still unassigned for this date
                if (scheduleData[personName].Shifts[dateStr] != "")
                    continue;

                // Apply constraints
                if (CanAssignShiftWithConsecutiveCheck(personName, shift, date, scheduleData, shifts, rules))
                {
                }
            }

            // Rotate staff to ensure fair distribution
            var rotatedEligibleStaff = RotateStaffForFairDistribution(eligibleStaff, date);

            // For non-priority shifts, assign up to the required count, but allow flexibility
            int assigned = 0;
            foreach (var person in rotatedEligibleStaff)
            {
                if (assigned >= requiredCount)
                    break;

                // Double-check constraints before assignment
                if (CanAssignShiftWithConsecutiveCheck(person.Name, shift, date, scheduleData, shifts, rules))
                {
                    scheduleData[person.Name].Shifts[dateStr] = shift;
                    assigned++;
                }
            }
        }

        // Enhanced constraint check that includes consecutive day checking
        private bool CanAssignShiftWithConsecutiveCheck(string personName, string shift, DateTime date,
            Dictionary<string, ScheduleDataModel> scheduleData, List<ShiftModel> shifts, RulesModel rules)
        {
            var dateStr = date.ToString("yyyy-MM-dd");

            // Check if already assigned for this date
            if (scheduleData[personName].Shifts[dateStr] != "")
            {
                return false;
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


            return true;
        }

        // Method to enforce consecutive day limit by inserting rest days when needed
        private void EnforceConsecutiveDayLimit(Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> applicableStaff, RulesModel rules)
        {
            foreach (var person in applicableStaff)
            {
                var personName = person.Name;
                int consecutiveCount = 0;
                DateTime? lastWorkDay = null;

                for (int i = 0; i < dateRange.Count; i++)
                {
                    var date = dateRange[i];
                    var dateStr = date.ToString("yyyy-MM-dd");
                    var shift = scheduleData[personName].Shifts[dateStr];

                    if (shift != "休息" && !string.IsNullOrEmpty(shift))
                    {
                        // This is a work day
                        if (lastWorkDay.HasValue && date.Date == lastWorkDay.Value.AddDays(1).Date)
                        {
                            // Consecutive work day
                            consecutiveCount += (int)GetShiftDayValue(shift, rules);
                        }
                        else
                        {
                            // New work sequence
                            consecutiveCount = (int)GetShiftDayValue(shift, rules);
                        }

                        // If we've reached max consecutive days, force a rest day tomorrow if possible
                        if (consecutiveCount >= rules.MaxConsecutiveDays && i < dateRange.Count - 1)
                        {
                            var nextDate = dateRange[i + 1];
                            var nextDateStr = nextDate.ToString("yyyy-MM-dd");
                            var nextShift = scheduleData[personName].Shifts[nextDateStr];

                            // Only replace if it's not already a rest day and it's a work shift
                            if (nextShift != "休息" && !string.IsNullOrEmpty(nextShift))
                            {
                                // Try to shift this to a future day if there's another staff member available
                                // For now, just force the rest day if possible
                                if (CanAssignShiftWithConsecutiveCheck(personName, "休息", nextDate, scheduleData, new List<ShiftModel>(), rules))
                                {
                                    scheduleData[personName].Shifts[nextDateStr] = "休息";
                                }
                            }
                        }

                        lastWorkDay = date;
                    }
                    else
                    {
                        // This is a rest day or unassigned
                        consecutiveCount = 0;
                        lastWorkDay = date;
                    }
                }
            }
        }

        // Method to enforce rest day requirements for each employee
        private void EnforceRestDayRequirements(Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> applicableStaff, RulesModel rules)
        {
            foreach (var person in applicableStaff)
            {
                var personName = person.Name;

                // Count current rest days for this person
                int currentRestDays = 0;
                foreach (var date in dateRange)
                {
                    var dateStr = date.ToString("yyyy-MM-dd");
                    if (scheduleData[personName].Shifts[dateStr] == "休息")
                    {
                        currentRestDays++;
                    }
                }

                // Calculate how many more rest days are needed
                int restDaysNeeded = rules.TotalRestDays - currentRestDays;

                if (restDaysNeeded > 0)
                {
                    // Need to assign more rest days
                    // Look for work days that can be converted to rest days
                    var workDays = new List<DateTime>();
                    foreach (var date in dateRange)
                    {
                        var dateStr = date.ToString("yyyy-MM-dd");
                        var shift = scheduleData[personName].Shifts[dateStr];
                        if (shift != "休息" && !string.IsNullOrEmpty(shift))
                        {
                            workDays.Add(date);
                        }
                    }

                    // Choose work days to convert to rest days, prioritizing days that don't break consecutive work limits
                    int converted = 0;
                    foreach (var date in workDays)
                    {
                        if (converted >= restDaysNeeded) break;

                        var dateStr = date.ToString("yyyy-MM-dd");

                        // Check if converting this day to rest would still allow the person to meet other requirements
                        if (CanConvertToRestDay(personName, date, scheduleData, dateRange, applicableStaff, rules))
                        {
                            scheduleData[personName].Shifts[dateStr] = "休息";
                            converted++;
                        }
                    }
                }
            }
        }

        // Helper to check if a work day can be converted to a rest day without violating other constraints
        private bool CanConvertToRestDay(string personName, DateTime date, Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> applicableStaff, RulesModel rules)
        {
            var dateStr = date.ToString("yyyy-MM-dd");
            var originalShift = scheduleData[personName].Shifts[dateStr];

            // Temporarily change to rest
            scheduleData[personName].Shifts[dateStr] = "休息";

            // Check if this change violates any constraints
            bool isValid = true;

            // Check consecutive day constraint
            double totalConsecutive = CalculateConsecutiveWorkDaysWithRules(personName, date, "休息", scheduleData, rules);
            if (totalConsecutive > rules.MaxConsecutiveDays)
            {
                isValid = false;
            }

            // Restore original value for now
            scheduleData[personName].Shifts[dateStr] = originalShift;

            return isValid;
        }

        // Method to handle half-day shift consecutive day arrangements
        private void HandleHalfDayShiftConsecutiveSchedule(Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> applicableStaff, List<ShiftModel> shifts, RulesModel rules)
        {
            // Process each half-day shift type
            foreach (var halfDayShift in rules.HalfDayShifts)
            {
                for (int i = 0; i < dateRange.Count - 1; i++) // -1 to have the next day available
                {
                    var currentDate = dateRange[i];
                    var nextDate = dateRange[i + 1];
                    var currentDateStr = currentDate.ToString("yyyy-MM-dd");
                    var nextDateStr = nextDate.ToString("yyyy-MM-dd");

                    // Look for people who have this half-day shift on current date but not on the next date
                    foreach (var person in applicableStaff)
                    {
                        var personName = person.Name;

                        // Check if this person has the half-day shift on current date but not on the next
                        if (scheduleData[personName].Shifts.ContainsKey(currentDateStr) &&
                            scheduleData[personName].Shifts[currentDateStr] == halfDayShift &&
                            scheduleData[personName].Shifts.ContainsKey(nextDateStr) &&
                            scheduleData[personName].Shifts[nextDateStr] != halfDayShift &&
                            scheduleData[personName].Shifts[nextDateStr] != "休息")
                        {
                            // Check if we can assign the same half-day shift to the next date
                            if (CanAssignShiftWithConsecutiveCheck(personName, halfDayShift, nextDate, scheduleData, shifts, rules))
                            {
                                // Check if this would still meet the shift requirements for the next date
                                // For this, we need to count existing assignments for the next date
                                int currentAssignmentCount = 0;
                                foreach (var p in applicableStaff)
                                {
                                    if (scheduleData[p.Name].Shifts[nextDateStr] == halfDayShift)
                                    {
                                        currentAssignmentCount++;
                                    }
                                }

                                // Get the required count for this shift on the next date
                                var isNextHoliday = IsHoliday(nextDate, rules);
                                var shiftRequirements = isNextHoliday ? rules.Holiday : rules.Weekday;
                                var requirement = shiftRequirements.FirstOrDefault(r => r.ShiftName == halfDayShift);

                                if (requirement != null && currentAssignmentCount < requirement.RequiredCount)
                                {
                                    // Assign the half-day shift to consecutive days
                                    scheduleData[personName].Shifts[nextDateStr] = halfDayShift;
                                }
                            }
                        }
                    }
                }
            }
        }

        // Old EnsureRestDayRequirements method will now be replaced since we have the new implementation
        // Keep the method name but update the content to be empty or mark as deprecated
        private void EnsureRestDayRequirements(Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> applicableStaff, List<ShiftModel> shifts,
            RulesModel rules, Dictionary<string, StaffStatistics> staffStats, Dictionary<string, DailyStatistics> dailyStats)
        {
            // This method is replaced by the new EnforceRestDayRequirements implementation
            // The new method is called separately in ProcessSchedulingRuleWithFullRequirements
        }

        private void ProcessOldStyleRulesWithFullRequirements(Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> staff, List<ShiftModel> shifts, RulesModel rules,
            Dictionary<string, Dictionary<string, string>> existingSchedule)
        {
            // Initialize statistics tracking
            var staffStats = new Dictionary<string, StaffStatistics>();
            var dailyStats = new Dictionary<string, DailyStatistics>();

            foreach (var person in staff)
            {
                staffStats[person.Name] = new StaffStatistics
                {
                    StaffName = person.Name,
                    ShiftCounts = new Dictionary<string, double>(),
                    TotalShiftDays = 0,
                    TotalWorkDays = 0,
                    TotalRestDays = 0
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
                    TotalAvailable = staff.Count
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

                var priorityShifts = sortedShiftRequirements.Where(req => req.Priority.HasValue).ToList();
                var nonPriorityShifts = sortedShiftRequirements.Where(req => !req.Priority.HasValue).ToList();

                // Step 1: Process priority shifts (must reach exact required count)
                foreach (var shiftRequirement in priorityShifts)
                {
                    var shiftType = shiftRequirement.ShiftName;
                    var requiredCount = shiftRequirement.RequiredCount;

                    // Create a list of eligible staff for this shift assignment
                    var eligibleStaff = new List<StaffModel>();
                    foreach (var person in staff)
                    {
                        var personName = person.Name;
                        if (scheduleData[personName].Shifts[dateStr] == "" &&
                            CanAssignShiftWithConsecutiveCheckAndStats(personName, shiftType, date, scheduleData, shifts, rules, existingSchedule, staffStats))
                        {
                            eligibleStaff.Add(person);
                        }
                    }

                    // Rotate staff to ensure fair distribution
                    var rotatedEligibleStaff = RotateStaffForFairDistribution(eligibleStaff, date);

                    // Assign exactly the required count of this shift
                    int assigned = 0;
                    foreach (var person in rotatedEligibleStaff)
                    {
                        if (assigned >= requiredCount)
                            break;

                        var personName = person.Name;
                        if (scheduleData[personName].Shifts[dateStr] == "" &&
                            CanAssignShiftWithConsecutiveCheckAndStats(personName, shiftType, date, scheduleData, shifts, rules, existingSchedule, staffStats))
                        {
                            // Perform assignment
                            scheduleData[personName].Shifts[dateStr] = shiftType;

                            // Update statistics
                            UpdateStatisticsAfterAssignment(personName, shiftType, dateStr, scheduleData, staffStats, dailyStats);
                            assigned++;
                        }
                    }
                }

                // Step 2: Process non-priority shifts after priority shifts
                foreach (var shiftRequirement in nonPriorityShifts)
                {
                    var shiftType = shiftRequirement.ShiftName;
                    var requiredCount = shiftRequirement.RequiredCount;

                    // Get unassigned staff who are eligible for this shift
                    var remainingUnassigned = staff.Where(person =>
                        scheduleData[person.Name].Shifts[dateStr] == "").ToList();

                    var eligibleStaff = new List<StaffModel>();
                    foreach (var person in remainingUnassigned)
                    {
                        var personName = person.Name;
                        if (CanAssignShiftWithConsecutiveCheckAndStats(personName, shiftType, date, scheduleData, shifts, rules, existingSchedule, staffStats))
                        {
                            eligibleStaff.Add(person);
                        }
                    }

                    // Rotate staff for fair distribution
                    var rotatedEligibleStaff = RotateStaffForFairDistribution(eligibleStaff, date);

                    // Assign up to the required count (flexible assignment)
                    int assigned = 0;
                    foreach (var person in rotatedEligibleStaff)
                    {
                        if (assigned >= requiredCount)
                            break;

                        var personName = person.Name;
                        if (scheduleData[personName].Shifts[dateStr] == "" &&
                            CanAssignShiftWithConsecutiveCheckAndStats(personName, shiftType, date, scheduleData, shifts, rules, existingSchedule, staffStats))
                        {
                            // Perform assignment
                            scheduleData[personName].Shifts[dateStr] = shiftType;

                            // Update statistics
                            UpdateStatisticsAfterAssignment(personName, shiftType, dateStr, scheduleData, staffStats, dailyStats);
                            assigned++;
                        }
                    }
                }

                // Step 3: Assign non-priority shifts first, then rest days only up to TotalRestDays requirement
                var stillUnassigned = staff.Where(person =>
                    scheduleData[person.Name].Shifts[dateStr] == "").ToList();

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
                        if (scheduleData.ContainsKey(personName) &&
                            scheduleData[personName].Shifts.ContainsKey(checkDateStr) &&
                            scheduleData[personName].Shifts[checkDateStr] == "休息")
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
                        UpdateStatisticsAfterAssignment(personName, "休息", dateStr, scheduleData, staffStats, dailyStats);
                    }
                    else
                    {
                        // Need to assign a non-priority shift instead of rest
                        // First, find a non-priority shift that's still needed for this date
                        bool shiftAssigned = false;

                        foreach (var shiftRequirement in nonPriorityShifts)
                        {
                            var shiftType = shiftRequirement.ShiftName;
                            var requiredCount = shiftRequirement.RequiredCount;

                            // Count how many people are already assigned this shift type for this date
                            int currentAssignments = 0;
                            foreach (var p in staff)
                            {
                                var pName = p.Name;
                                if (scheduleData.ContainsKey(pName) &&
                                    scheduleData[pName].Shifts.ContainsKey(dateStr) &&
                                    scheduleData[pName].Shifts[dateStr] == shiftType)
                                {
                                    currentAssignments++;
                                }
                            }

                            // If we need more of this shift and it's eligible for this person
                            if (currentAssignments < requiredCount &&
                                CanAssignShiftWithConsecutiveCheckAndStats(personName, shiftType, date, scheduleData, shifts, rules, existingSchedule, staffStats))
                            {
                                scheduleData[personName].Shifts[dateStr] = shiftType;
                                UpdateStatisticsAfterAssignment(personName, shiftType, dateStr, scheduleData, staffStats, dailyStats);
                                shiftAssigned = true;
                                break;
                            }
                        }

                        // If no non-priority shift could be assigned, assign rest anyway (but this should be rare)
                        if (!shiftAssigned)
                        {
                            scheduleData[personName].Shifts[dateStr] = "休息";
                            UpdateStatisticsAfterAssignment(personName, "休息", dateStr, scheduleData, staffStats, dailyStats);
                        }
                    }
                }
            }

            // After all dates are processed, check TotalRestDays constraint
            EnforceTotalRestDaysRequirement(scheduleData, dateRange, staff, rules, existingSchedule);

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

        private bool CanAssignShift(string personName, string shift, DateTime date, Dictionary<string, ScheduleDataModel> scheduleData,
            List<ShiftModel> shifts, RulesModel rules)
        {
            var dateStr = date.ToString("yyyy-MM-dd");

            // Check if already assigned for this date
            if (scheduleData[personName].Shifts.ContainsKey(dateStr))
            {
                return false;
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

        // Enhanced constraint check that includes both consecutive day checking and statistics tracking
        private bool CanAssignShiftWithConsecutiveCheckAndStats(string personName, string shift, DateTime date,
            Dictionary<string, ScheduleDataModel> scheduleData, List<ShiftModel> shifts, RulesModel rules,
            Dictionary<string, Dictionary<string, string>> existingSchedule, Dictionary<string, StaffStatistics> staffStats)
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
                        Shifts = new Dictionary<string, string>(kvp.Value.Shifts)
                    };
                }
                tempScheduleData[personName].Shifts[dateStr] = shift;

                // Calculate consecutive work days considering both existing and new schedule
                double totalConsecutive = CalculateConsecutiveWorkDaysWithExistingSchedule(personName, date, shift, tempScheduleData, existingSchedule, rules);

                if (totalConsecutive > rules.MaxConsecutiveDays)
                {
                    return false;
                }
            }

            return true;
        }

        // Calculate consecutive work days considering both existing and new schedule
        private double CalculateConsecutiveWorkDaysWithExistingSchedule(string personName, DateTime date, string shift,
            Dictionary<string, ScheduleDataModel> newScheduleData,
            Dictionary<string, Dictionary<string, string>> existingSchedule, RulesModel rules)
        {
            double maxConsecutive = 0;
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
            var startDate = date.AddDays(-14);
            var endDate = date.AddDays(14);

            var currentDate = startDate;
            while (currentDate <= endDate)
            {
                var dateStr = currentDate.ToString("yyyy-MM-dd");

                string assignedShift = "";
                if (combinedSchedule.ContainsKey(dateStr))
                {
                    assignedShift = combinedSchedule[dateStr];
                }
                else
                {
                    // If not in either schedule, consider as rest day
                    assignedShift = "休息";
                }

                if (assignedShift != "休息" && !string.IsNullOrEmpty(assignedShift))
                {
                    if (lastAssignedDate.HasValue && currentDate.Date == lastAssignedDate.Value.AddDays(1).Date)
                    {
                        currentConsecutive += GetShiftDayValue(assignedShift, rules);
                    }
                    else
                    {
                        currentConsecutive = GetShiftDayValue(assignedShift, rules);
                    }
                    maxConsecutive = Math.Max(maxConsecutive, currentConsecutive);
                    lastAssignedDate = currentDate;
                }
                else
                {
                    currentConsecutive = 0;
                    lastAssignedDate = currentDate;
                }

                currentDate = currentDate.AddDays(1);
            }

            return maxConsecutive;
        }

        // Method to update statistics after each assignment
        private void UpdateStatisticsAfterAssignment(string personName, string shiftType, string dateStr,
            Dictionary<string, ScheduleDataModel> scheduleData,
            Dictionary<string, StaffStatistics> staffStats,
            Dictionary<string, DailyStatistics> dailyStats)
        {
            // Update staff statistics
            if (staffStats.ContainsKey(personName))
            {
                if (staffStats[personName].ShiftCounts.ContainsKey(shiftType))
                {
                    staffStats[personName].ShiftCounts[shiftType]++;
                }
                else
                {
                    staffStats[personName].ShiftCounts[shiftType] = 1;
                }

                // Update total counts
                if (shiftType == "休息")
                {
                    staffStats[personName].TotalRestDays++;
                }
                else if (!string.IsNullOrEmpty(shiftType))
                {
                    staffStats[personName].TotalWorkDays++;
                }
                staffStats[personName].TotalShiftDays++;
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
        private void EnforceTotalRestDaysRequirement(Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> applicableStaff, RulesModel rules,
            Dictionary<string, Dictionary<string, string>> existingSchedule)
        {
            foreach (var person in applicableStaff)
            {
                var personName = person.Name;

                // Count current rest days for this person in the new schedule period only
                int currentRestDays = 0;
                foreach (var date in dateRange)
                {
                    var dateStr = date.ToString("yyyy-MM-dd");
                    if (scheduleData.ContainsKey(personName) &&
                        scheduleData[personName].Shifts.ContainsKey(dateStr) &&
                        scheduleData[personName].Shifts[dateStr] == "休息")
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
                        if (scheduleData.ContainsKey(personName) &&
                            scheduleData[personName].Shifts.ContainsKey(dateStr) &&
                            scheduleData[personName].Shifts[dateStr] != "休息" &&
                            !string.IsNullOrEmpty(scheduleData[personName].Shifts[dateStr]))
                        {
                            workDays.Add(date);
                        }
                    }

                    // Convert some work days to rest days, considering MaxConsecutiveDays constraint
                    int converted = 0;
                    foreach (var date in workDays)
                    {
                        if (converted >= restDaysNeeded) break;

                        var dateStr = date.ToString("yyyy-MM-dd");
                        var originalShift = scheduleData[personName].Shifts[dateStr];

                        // Temporarily change to rest day to test the constraint
                        scheduleData[personName].Shifts[dateStr] = "休息";

                        // Check if this change violates MaxConsecutiveDays constraint
                        if (CheckMaxConsecutiveDaysConstraint(scheduleData[personName].Shifts, rules.MaxConsecutiveDays, dateRange, personName, existingSchedule))
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
        private bool CheckMaxConsecutiveDaysConstraint(Dictionary<string, string> newPersonSchedule,
            int maxConsecutiveDays, List<DateTime> dateRange, string personName,
            Dictionary<string, Dictionary<string, string>> existingSchedule = null)
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
        private void HandleHalfDayShiftConsecutiveArrangement(Dictionary<string, ScheduleDataModel> scheduleData,
            List<DateTime> dateRange, List<StaffModel> applicableStaff, List<ShiftModel> shifts, RulesModel rules)
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
                        if (scheduleData.ContainsKey(personName) &&
                            scheduleData[personName].Shifts.ContainsKey(currentDateStr) &&
                            scheduleData[personName].Shifts[currentDateStr] == halfDayShift &&
                            scheduleData[personName].Shifts.ContainsKey(nextDateStr) &&
                            scheduleData[personName].Shifts[nextDateStr] == "休息")
                        {
                            // Look for another person who has a work shift on the next day
                            foreach (var otherPerson in applicableStaff)
                            {
                                var otherPersonName = otherPerson.Name;
                                if (otherPersonName != personName &&
                                    scheduleData.ContainsKey(otherPersonName) &&
                                    scheduleData[otherPersonName].Shifts.ContainsKey(nextDateStr) &&
                                    scheduleData[otherPersonName].Shifts[nextDateStr] != "休息" &&
                                    scheduleData[otherPersonName].Shifts[nextDateStr] != halfDayShift)
                                {
                                    // Check if we can swap: person gets half-day shift on next day,
                                    // and other person gets original person's assignment (rest)
                                    var originalOtherShift = scheduleData[otherPersonName].Shifts[nextDateStr];

                                    // Create temporary schedules to test constraints
                                    var tempSchedulePerson = new Dictionary<string, string>(scheduleData[personName].Shifts);
                                    var tempScheduleOther = new Dictionary<string, string>(scheduleData[otherPersonName].Shifts);

                                    // Apply potential swap
                                    tempSchedulePerson[nextDateStr] = halfDayShift;  // Person gets consecutive half-day
                                    tempScheduleOther[nextDateStr] = "休息";  // Other person gets rest day

                                    // Check constraints for both people
                                    bool personValid = CheckMaxConsecutiveDaysConstraint(tempSchedulePerson, rules.MaxConsecutiveDays, dateRange, personName);
                                    bool otherValid = CheckMaxConsecutiveDaysConstraint(tempScheduleOther, rules.MaxConsecutiveDays, dateRange, otherPersonName);

                                    if (personValid && otherValid)
                                    {
                                        // Perform the swap if it's beneficial (especially for 甲2PLUS)
                                        if (halfDayShift == "甲2PLUS")
                                        {
                                            scheduleData[personName].Shifts[nextDateStr] = halfDayShift;
                                            scheduleData[otherPersonName].Shifts[nextDateStr] = "休息";
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
            DateTime endDate)
        {
            return GenerateScheduleWithPriorityAndStats(staffs, shifts, rules, startDate, endDate);
        }
    }
}