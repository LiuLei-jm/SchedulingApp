using CommunityToolkit.Mvvm.ComponentModel;

namespace SchedulingApp.Models
{
    public partial class ScheduleItemModel : ObservableObject
    {
        [ObservableProperty]
        private string _date = string.Empty;

        [ObservableProperty]
        private string _shift = string.Empty;

        [ObservableProperty]
        private string _personName = string.Empty;

        [ObservableProperty]
        private string _shiftColor = "#FFFFFF";
    }
}