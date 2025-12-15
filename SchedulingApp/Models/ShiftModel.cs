using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SchedulingApp.Models
{
    public class ShiftModel : INotifyPropertyChanged
    {
        private string _shiftName = string.Empty;
        private string _startTime = string.Empty;
        private string _endTime = string.Empty;
        private string _color = "#FFFFFF";

        public string ShiftName
        {
            get => _shiftName;
            set
            {
                if (_shiftName != value)
                {
                    _shiftName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StartTime
        {
            get => _startTime;
            set
            {
                if (_startTime != value)
                {
                    _startTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public string EndTime
        {
            get => _endTime;
            set
            {
                if (_endTime != value)
                {
                    _endTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Color
        {
            get => _color;
            set
            {
                if (_color != value)
                {
                    _color = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

