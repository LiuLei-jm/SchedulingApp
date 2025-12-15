using CommunityToolkit.Mvvm.ComponentModel;
using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;
using System.Collections.ObjectModel;

namespace SchedulingApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        public MainViewModel(
            IDataService dataService,
            ScheduleViewModel scheduleViewModel,
            StaffViewModel staffViewModel,
            ShiftViewModel shiftViewModel,
            RulesViewModel rulesViewModel
        )
        {
            _dataService = dataService;

            // 初始化各个模块的ViewModel (这些 are now injected)
            RulesViewModel = rulesViewModel;
            StaffViewModel = staffViewModel;
            ShiftViewModel = shiftViewModel;
            ScheduleViewModel = scheduleViewModel;
        }

        public ScheduleViewModel ScheduleViewModel { get; }
        public StaffViewModel StaffViewModel { get; }
        public ShiftViewModel ShiftViewModel { get; }
        public RulesViewModel RulesViewModel { get; }

        public async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                // 初始化共享数据 - 现在由MainViewModel协调但异步执行
                var staffList = _dataService.LoadStaff();
                StaffViewModel.Staffs = new ObservableCollection<StaffModel>(staffList);

                var shiftList = _dataService.LoadShifts();
                ShiftViewModel.Shifts = new ObservableCollection<ShiftModel>(shiftList);

                var rules = _dataService.LoadRules();
                RulesViewModel.Rules = rules;

                // 初始化ScheduleViewModel并传递共享数据
                ScheduleViewModel.SetStaffData(StaffViewModel.Staffs);
                ScheduleViewModel.SetShiftsData(ShiftViewModel.Shifts);
                ScheduleViewModel.FillWeekendsIfEmpty(rules, ScheduleViewModel.StartDate, ScheduleViewModel.EndDate);

            });
        }
    }
}
