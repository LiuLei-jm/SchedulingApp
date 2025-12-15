using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchedulingApp.Models;

namespace SchedulingApp.ViewModels
{
    public partial class StaffEditDialogViewModel : ObservableObject
    {
        private StaffModel _staff;
        [ObservableProperty]
        private string _title;

        public StaffEditDialogViewModel(string title, StaffModel staff)
        {
            Title = title;
            _staff = staff;

            // Initialize individual properties to ensure proper binding
            Name = staff.Name;
            Id = staff.Id;
            Group = staff.Group;
        }

        public StaffModel Staff
        {
            get => _staff;
            set => SetProperty(ref _staff, value);
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
        private string _name;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
        private string _id;

        [ObservableProperty]
        private string _group;

        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private void Confirm()
        {
            // Update the staff model with values from the dialog
            _staff.Name = Name;
            _staff.Id = Id;
            _staff.Group = Group;

            RequestClose?.Invoke(true);
        }

        private bool CanConfirm()
        {
            return !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Id);
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(false);
        }

        // 事件用于通知视图关闭对话框
        public event Action<bool>? RequestClose;
    }
}