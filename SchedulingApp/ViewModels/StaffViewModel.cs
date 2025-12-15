using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using SchedulingApp.Models;
using SchedulingApp.Services.Interfaces;
using SchedulingApp.Views.Dialogs;
using System.Collections.ObjectModel;
using System.Windows;

namespace SchedulingApp.ViewModels
{
    public partial class StaffViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IStaffExcelService _staffExcelService;

        public StaffViewModel(IDataService dataService, IStaffExcelService staffExcelService)
        {
            _dataService = dataService;
            _staffExcelService = staffExcelService;
        }

        // 数据属性
        public ObservableCollection<StaffModel> Staffs { get; set; } = [];

        // 命令
        [RelayCommand]
        private void AddStaff()
        {
            try
            {
                // Create a simple dialog to get staff information
                var dialog = new StaffEditDialog("添加人员", new StaffModel { Name = "", Id = "", Group = "" });

                if (dialog.ShowDialog() == true)
                {
                    var newStaff = dialog.Staff;

                    // Validate inputs
                    if (string.IsNullOrWhiteSpace(newStaff.Name) || string.IsNullOrWhiteSpace(newStaff.Id))
                    {
                        Growl.ErrorGlobal("姓名和工号不能为空！");
                        return;
                    }

                    // Check if ID already exists
                    if (Staffs.Any(s => s.Id == newStaff.Id))
                    {
                        Growl.ErrorGlobal("工号已存在，请使用不同的工号！");
                        return;
                    }

                    // Add to collection
                    Staffs.Add(newStaff);

                    // Save to file
                    _dataService.SaveStaff(Staffs.ToList());

                    Console.WriteLine($"成功添加员工: {newStaff.Name} (ID: {newStaff.Id})");
                    Growl.InfoGlobal($"成功添加员工: {newStaff.Name}");
                }
            }
            catch (Exception ex)
            {
                Growl.ErrorGlobal($"添加员工失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void EditStaff(object parameter)
        {
            try
            {
                var selectedStaff = parameter as StaffModel;

                // If no item is selected, we need to prompt the user to select one
                if (selectedStaff == null)
                {
                    Growl.InfoGlobal("请先选择要编辑的人员！");
                    return;
                }

                // Create a copy to edit
                var staffToEdit = new StaffModel
                {
                    Name = selectedStaff.Name,
                    Id = selectedStaff.Id,  // Keep original ID for comparison
                    Group = selectedStaff.Group
                };

                var dialog = new StaffEditDialog("编辑人员", staffToEdit);

                if (dialog.ShowDialog() == true)
                {
                    var updatedStaff = dialog.Staff;

                    // Validate inputs
                    if (string.IsNullOrWhiteSpace(updatedStaff.Name) || string.IsNullOrWhiteSpace(updatedStaff.Id))
                    {
                        Growl.ErrorGlobal("姓名和工号不能为空！");
                        return;
                    }

                    // Check if ID is being changed and if the new ID already exists
                    if (selectedStaff.Id != updatedStaff.Id && Staffs.Any(s => s.Id == updatedStaff.Id && s != selectedStaff))
                    {
                        Growl.ErrorGlobal("工号已存在，请使用不同的工号！");
                        return;
                    }

                    // Update the existing staff member
                    selectedStaff.Name = updatedStaff.Name;
                    selectedStaff.Id = updatedStaff.Id;
                    selectedStaff.Group = updatedStaff.Group;

                    // Save to file
                    _dataService.SaveStaff(Staffs.ToList());

                    Console.WriteLine($"成功编辑员工: {updatedStaff.Name} (ID: {updatedStaff.Id})");
                    Growl.InfoGlobal($"成功编辑员工: {updatedStaff.Name}");
                }
            }
            catch (Exception ex)
            {
                Growl.ErrorGlobal($"编辑员工失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void DeleteStaff(object parameter)
        {
            try
            {
                var selectedStaffList = parameter as System.Collections.IList;

                // If no item is selected, we need to prompt the user to select one
                if (selectedStaffList == null || selectedStaffList.Count == 0)
                {
                    Growl.InfoGlobal("请先选择要删除的人员！");
                    return;
                }

                // Convert to list of StaffModel
                var staffToDelete = new List<StaffModel>();
                foreach (var item in selectedStaffList)
                {
                    if (item is StaffModel staff)
                    {
                        staffToDelete.Add(staff);
                    }
                }

                if (staffToDelete.Count == 0)
                {
                    Growl.InfoGlobal("请先选择要删除的人员！");
                    return;
                }

                string message;
                if (staffToDelete.Count == 1)
                {
                    message = $"确定要删除人员 '{staffToDelete[0].Name}' 吗？";
                }
                else
                {
                    message = $"确定要删除选中的 {staffToDelete.Count} 位人员吗？";
                }

                // For confirmation, using System.Windows.MessageBox to avoid conflict with HandyControl
                var result = System.Windows.MessageBox.Show(message,
                                            "确认删除",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Remove from collection
                    foreach (var staff in staffToDelete)
                    {
                        Staffs.Remove(staff);
                    }

                    // Save to file
                    _dataService.SaveStaff(Staffs.ToList());

                    Console.WriteLine($"成功删除 {staffToDelete.Count} 名员工");
                    Growl.InfoGlobal($"成功删除 {staffToDelete.Count} 名员工");
                }
            }
            catch (Exception ex)
            {
                Growl.ErrorGlobal($"删除员工失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void DownloadStaffTemplate()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    FileName = "人员信息模板.xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    _staffExcelService.ExportStaffTemplate(saveFileDialog.FileName);
                    Console.WriteLine($"人员信息模板已下载到: {saveFileDialog.FileName}");

                    // Show message to user
                    Growl.InfoGlobal("人员信息模板下载成功！");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"下载模板失败: {ex.Message}");
                Growl.ErrorGlobal($"下载模板失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void UploadStaffFile()
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var importedStaff = _staffExcelService.ImportStaffFromExcel(openFileDialog.FileName);

                    // Confirm with user before replacing staff list
                    var result = System.Windows.MessageBox.Show($"确定要导入 {importedStaff.Count} 条人员信息？这将替换当前的人员列表。",
                                                "确认导入",
                                                MessageBoxButton.YesNo,
                                                MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Replace the current staff list with imported data
                        Staffs.Clear();
                        foreach (var staff in importedStaff)
                        {
                            Staffs.Add(staff);
                        }

                        // Save to file
                        _dataService.SaveStaff(Staffs.ToList());

                        Console.WriteLine($"成功导入 {importedStaff.Count} 条人员信息");
                        Growl.InfoGlobal($"成功导入 {importedStaff.Count} 条人员信息！");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"上传文件失败: {ex.Message}");
                Growl.ErrorGlobal($"上传文件失败: {ex.Message}");
            }
        }
    }
}