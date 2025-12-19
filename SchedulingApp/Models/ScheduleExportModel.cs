namespace SchedulingApp.Models
{
    public class ScheduleExportModel
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string ShiftType { get; set; } = string.Empty;  // Add shift type to the export model
    }
}