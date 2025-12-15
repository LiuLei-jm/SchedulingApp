using Microsoft.Extensions.DependencyInjection;
using SchedulingApp.Services.Implementations;
using SchedulingApp.Services.Interfaces;
using SchedulingApp.ViewModels;
using System.Windows;

namespace SchedulingApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();

        // 注册服务
        services.AddSingleton<IDataService, DataService>();
        services.AddSingleton<DataService>(provider => (DataService)provider.GetRequiredService<IDataService>());
        services.AddSingleton<ISchedulingService, SchedulingService>();
        services.AddSingleton<IStaffExcelService, StaffExcelService>();
        services.AddSingleton<IShiftExcelService, ShiftExcelService>();
        services.AddSingleton<IExcelExportService, ExcelExportService>();

        // 注册ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ScheduleViewModel>();
        services.AddSingleton<StaffViewModel>();
        services.AddSingleton<ShiftViewModel>();
        services.AddSingleton<RulesViewModel>();

        _serviceProvider = services.BuildServiceProvider();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 获取MainViewModel并设置为MainWindow的DataContext
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        var mainWindow = new MainWindow();
        mainWindow.DataContext = mainViewModel;

        // 初始化共享数据
        await mainViewModel.InitializeAsync();

        mainWindow.Show();
    }
}

