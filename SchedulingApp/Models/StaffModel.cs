using CommunityToolkit.Mvvm.ComponentModel;

namespace SchedulingApp.Models
{
    public partial class StaffModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _group = string.Empty;

        [ObservableProperty]
        private bool _isSelected;
    }
}