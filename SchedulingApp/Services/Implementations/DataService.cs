using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;
using System.IO;
using System.Text.Json;

namespace SchedulingApp.Services.Implementations
{
    public class DataService : IDataService
    {
        private readonly string _baseDirectory = AppContext.BaseDirectory;
        private readonly string _dataDirectory;
        private readonly string _staffFile;
        private readonly string _shiftsFile;
        private readonly string _rulesFile;
        private readonly string _scheduleFile;

        public DataService()
        {
            _dataDirectory = Path.Combine(_baseDirectory, "data");
            _staffFile = Path.Combine(_dataDirectory, "staffs.json");
            _shiftsFile = Path.Combine(_dataDirectory, "shifts.json");
            _rulesFile = Path.Combine(_dataDirectory, "rules.json");
            _scheduleFile = Path.Combine(_dataDirectory, "schedule.json");
            // 确保数据目录存在
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }
        }

        public List<StaffModel> LoadStaff()
        {
            if (File.Exists(_staffFile))
            {
                var json = File.ReadAllText(_staffFile);
                var staff = JsonSerializer.Deserialize<List<StaffModel>>(json);
                return staff ?? new List<StaffModel>();
            }

            // 默认员工数据
            return new List<StaffModel>();
        }

        public void SaveStaff(List<StaffModel> staff)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            var json = JsonSerializer.Serialize(staff, options);
            File.WriteAllText(_staffFile, json);
        }

        public List<ShiftModel> LoadShifts()
        {
            if (File.Exists(_shiftsFile))
            {
                var json = File.ReadAllText(_shiftsFile);
                var shifts = JsonSerializer.Deserialize<List<ShiftModel>>(json);
                return shifts ?? new List<ShiftModel>();
            }

            // 默认班次数据
            return new List<ShiftModel>
            {
                {
                    new ShiftModel
                    {
                        ShiftName = "甲2PLUS",
                        StartTime = "08:00",
                        EndTime = "12:00",
                        Color = "#FFD700",
                    }
                },
                {
                    new ShiftModel
                    {
                        ShiftName =  "甲2",
                        StartTime = "08:00",
                        EndTime = "17:00",
                        Color = "#FFA07A",
                    }
                },
                {

                    new ShiftModel
                    {
                        ShiftName = "乙1",
                        StartTime = "08:30",
                        EndTime = "17:30",
                        Color = "#87CEFA",
                    }
                },
                {

                    new ShiftModel
                    {
                        ShiftName = "乙2",
                        StartTime = "09:30",
                        EndTime = "18:30",
                        Color = "#98FB98",
                    }
                },
                {

                    new ShiftModel
                    {
                        ShiftName = "丙",
                        StartTime = "12:00",
                        EndTime = "21:00",
                        Color = "#DDA0DD",
                    }
                },
                {

                    new ShiftModel
                    {
                        ShiftName = "休息",
                        StartTime = "",
                        EndTime = "",
                        Color = "#D3D3D3",
                    }
                },
            };
        }

        public void SaveShifts(List<ShiftModel> shifts)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            var json = JsonSerializer.Serialize(shifts, options);
            File.WriteAllText(_shiftsFile, json);
        }

        public RulesModel LoadRules()
        {
            if (File.Exists(_rulesFile))
            {
                var json = File.ReadAllText(_rulesFile);
                var rules = JsonSerializer.Deserialize<RulesModel>(json);
                return rules ?? new RulesModel();
            }

            // 默认规则数据
            return new RulesModel
            {
                MaxConsecutiveDays = 5,
                TotalRestDays = 4,
            };
        }

        public void SaveRules(RulesModel rules)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            var json = JsonSerializer.Serialize(rules, options);
            File.WriteAllText(_rulesFile, json);
        }

        public string ScheduleFile => _scheduleFile;

        public List<ScheduleItemModel> LoadSchedule()
        {
            if (File.Exists(_scheduleFile))
            {
                var json = File.ReadAllText(_scheduleFile);
                var schedule = JsonSerializer.Deserialize<List<ScheduleItemModel>>(json);
                return schedule ?? new List<ScheduleItemModel>();
            }

            // 默认排班数据
            return new List<ScheduleItemModel>();
        }

        public void SaveSchedule(List<ScheduleItemModel> schedule)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            var json = JsonSerializer.Serialize(schedule, options);
            File.WriteAllText(_scheduleFile, json);
        }
    }
}

