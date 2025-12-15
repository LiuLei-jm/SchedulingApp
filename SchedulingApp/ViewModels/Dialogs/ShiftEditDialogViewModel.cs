using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchedulingApp.Models;

namespace SchedulingApp.ViewModels
{
    public partial class ShiftEditDialogViewModel : ObservableObject
    {
        private ShiftModel _shift;

        [ObservableProperty]
        private string _title;

        public ShiftEditDialogViewModel(string title, ShiftModel shift)
        {
            _title = title;
            _shift = shift;

            // Initialize individual properties to ensure proper binding
            ShiftName = shift.ShiftName;
            StartTime = shift.StartTime;
            EndTime = shift.EndTime;
            Color = shift.Color;
        }

        public ShiftModel Shift
        {
            get => _shift;
            set => SetProperty(ref _shift, value);
        }

        public string ShiftName
        {
            get => _shift.ShiftName;
            set
            {
                if (_shift.ShiftName != value)
                {
                    _shift.ShiftName = value;
                    OnPropertyChanged();
                    ConfirmCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string StartTime
        {
            get => _shift.StartTime;
            set
            {
                if (_shift.StartTime != value)
                {
                    _shift.StartTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public TimeSpan? SelectedStartTime
        {
            get
            {
                if (TimeSpan.TryParse(StartTime, out TimeSpan time))
                    return time;
                return null;
            }
            set
            {
                if (value.HasValue)
                {
                    StartTime = value.Value.ToString(@"hh\:mm");
                    OnPropertyChanged(nameof(StartTime));
                }
                OnPropertyChanged();
            }
        }

        public TimeSpan? SelectedEndTime
        {
            get
            {
                if (TimeSpan.TryParse(EndTime, out TimeSpan time))
                    return time;
                return null;
            }
            set
            {
                if (value.HasValue)
                {
                    EndTime = value.Value.ToString(@"hh\:mm");
                    OnPropertyChanged(nameof(EndTime));
                }
                OnPropertyChanged();
            }
        }

        public string EndTime
        {
            get => _shift.EndTime;
            set
            {
                if (_shift.EndTime != value)
                {
                    _shift.EndTime = value;
                    OnPropertyChanged();
                }
            }
        }


        public string Color
        {
            get => _shift.Color;
            set
            {
                if (_shift.Color != value)
                {
                    _shift.Color = value;
                    OnPropertyChanged();
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private void Confirm()
        {
            RequestClose?.Invoke(true);
        }

        private bool CanConfirm()
        {
            return !string.IsNullOrWhiteSpace(ShiftName);
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
