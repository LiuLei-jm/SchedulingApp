using SchedulingApp.Models;

namespace SchedulingApp.Helpers
{
    public static class RulesHelper
    {
        public static RulesModel CurrentRules { get; set; } = new RulesModel();

        public static string GetRestShiftName()
        {
            return CurrentRules?.RestShiftName ?? "休息";
        }
    }
}