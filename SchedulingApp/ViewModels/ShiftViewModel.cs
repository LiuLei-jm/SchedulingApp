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
    public partial class ShiftViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IShiftExcelService _shiftExcelService;

        public ShiftViewModel(IDataService dataService, IShiftExcelService shiftExcelService)
        {
            _dataService = dataService;
            _shiftExcelService = shiftExcelService;
        }

        // 数据属性
        public ObservableCollection<ShiftModel> Shifts { get; set; } =
            new ObservableCollection<ShiftModel>();

        [ObservableProperty]
        private ShiftModel? _selectedShift;

        // 命令
        [RelayCommand]
        private void AddShift()
        {
            try
            {
                var dialog = new ShiftEditDialog("添加班次", new ShiftModel());
                if (dialog.ShowDialog() == true)
                {
                    // Add the new shift to the collection if OK was clicked
                    var newShift = dialog.Shift;

                    if (string.IsNullOrWhiteSpace(newShift.ShiftName))
                    {
                        Growl.ErrorGlobal("班次名称不能为空");
                        return;
                    }

                    var rules = _dataService.LoadRules();
                    if (newShift.ShiftName == rules.RestShiftName)
                    {
                        Growl.ErrorGlobal($"无法添加\"{rules.RestShiftName}\"班次，该班次为默认程序值，无法新增");
                        return;
                    }

                    Shifts.Add(newShift);
                    _dataService.SaveShifts(Shifts.ToList());
                    Growl.InfoGlobal($"成功添加班次: {newShift.ShiftName}");
                }
            }
            catch (Exception ex)
            {
                Growl.ErrorGlobal($"添加班次失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void EditShift(object parameter)
        {
            try
            {
                var selectedShift = parameter as ShiftModel;
                if (selectedShift == null)
                {
                    Growl.ErrorGlobal("请选择要编辑的班次");
                    return;
                }

                var rules = _dataService.LoadRules();
                if (selectedShift.ShiftName == rules.RestShiftName)
                {
                    Growl.ErrorGlobal($"无法编辑\"{rules.RestShiftName}\"班次，该班次为默认程序值，无法编辑");
                    return;
                }

                var shiftToEdit = new ShiftModel
                {
                    ShiftName = selectedShift.ShiftName,
                    StartTime = selectedShift.StartTime,
                    EndTime = selectedShift.EndTime,
                    Color = selectedShift.Color,
                };

                var dialog = new ShiftEditDialog("编辑班次", shiftToEdit);
                if (dialog.ShowDialog() == true)
                {
                    var updatedShift = dialog.Shift;

                    if (string.IsNullOrWhiteSpace(updatedShift.ShiftName))
                    {
                        Growl.ErrorGlobal("班次名称不能为空");
                        return;
                    }

                    if (
                        Shifts.Any(s => s.ShiftName == updatedShift.ShiftName && s != selectedShift)
                    )
                    {
                        Growl.ErrorGlobal("班次名称已存在");
                        return;
                    }

                    selectedShift.ShiftName = updatedShift.ShiftName;
                    selectedShift.StartTime = updatedShift.StartTime;
                    selectedShift.EndTime = updatedShift.EndTime;
                    selectedShift.Color = updatedShift.Color;

                    _dataService.SaveShifts(Shifts.ToList());
                    Growl.InfoGlobal($"成功编辑班次: {shiftToEdit.ShiftName}");
                }
            }
            catch (Exception ex)
            {
                Growl.ErrorGlobal($"编辑班次失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void DeleteShift(object parameter)
        {
            try
            {
                var selectedShiftList = parameter as System.Collections.IList;
                if (selectedShiftList == null || selectedShiftList.Count == 0)
                {
                    Growl.ErrorGlobal("请选择要删除的班次!");
                    return;
                }

                var shiftToDelete = new List<ShiftModel>();
                foreach (var item in selectedShiftList)
                {
                    if (item is ShiftModel shift)
                    {
                        var rules = _dataService.LoadRules();
                        // Check if any of the selected shifts is rest shift, if so, prevent deletion
                        if (shift.ShiftName == rules.RestShiftName)
                        {
                            Growl.ErrorGlobal($"无法删除\"{rules.RestShiftName}\"班次，该班次为默认程序值，无法删除");
                            return;
                        }
                        shiftToDelete.Add(shift);
                    }
                }

                if (shiftToDelete.Count == 0)
                {
                    Growl.InfoGlobal("请选择要删除的班次!");
                    return;
                }

                string message;
                if (shiftToDelete.Count == 1)
                {
                    message = $"确定要删除班次: {shiftToDelete[0].ShiftName} 吗？";
                }
                else
                {
                    message = $"确定要删除选中的 {shiftToDelete.Count} 个班次吗？";
                }

                var result = HandyControl.Controls.MessageBox.Ask(
                    "确定要删除该班次吗？",
                    "确认删除"
                );
                if (result == MessageBoxResult.OK)
                {
                    foreach (var shift in shiftToDelete)
                    {
                        Shifts.Remove(shift);
                    }
                    _dataService.SaveShifts(Shifts.ToList());
                    Growl.InfoGlobal("成功删除班次");
                }
            }
            catch (Exception ex)
            {
                Growl.ErrorGlobal($"删除班次失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void DownloadShiftTemplate()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx|ALL files (*.*)|*.*",
                    FileName = "班次模板.xlsx",
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    _shiftExcelService.ExportShiftToTemplate(saveFileDialog.FileName);
                    Growl.InfoGlobal("班次模板下载成功!");
                }
            }
            catch (Exception ex)
            {
                Growl.ErrorGlobal($"下载班次模板失败: {ex.Message}");
            }
        }

        [RelayCommand]
        private void UploadShiftFile()
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx|ALL files (*.*)|*.*",
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var importedShifts = _shiftExcelService.ImportShiftFromExcel(
                        openFileDialog.FileName
                    );
                    var result = System.Windows.MessageBox.Show(
                        $"确定要导入 {importedShifts.Count} 个班次吗？",
                        "确认导入",
                        MessageBoxButton.OKCancel
                    );
                    if (result != MessageBoxResult.OK)
                    {
                        Shifts.Clear();
                        foreach (var shift in importedShifts)
                        {
                            Shifts.Add(shift);
                        }
                        _dataService.SaveShifts(Shifts.ToList());

                        Growl.InfoGlobal($"成功导入 {importedShifts.Count} 个班次!");
                    }
                }
            }
            catch (Exception ex)
            {
                Growl.ErrorGlobal($"上传班次文件失败: {ex.Message}");
            }
        }
    }
}
