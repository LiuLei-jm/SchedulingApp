namespace SchedulingApp.Models
{
    public class ScheduleDataModel
    {
        public string Id { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public Dictionary<string, string> Shifts { get; set; } = new Dictionary<string, string>();
    }
}