using SchedulingApp.Models;
using SchedulingApp.ViewModels;
using System.Windows;
using System.Windows.Media;

namespace SchedulingApp.Views.Dialogs
{
    public partial class ShiftEditDialog : Window
    {
        private ShiftEditDialogViewModel _viewModel;

        public ShiftModel Shift { get; private set; }

        public ShiftEditDialog(string title, ShiftModel shift)
        {
            Shift = shift;

            InitializeComponent();

            // 创建并设置ViewModel
            _viewModel = new ShiftEditDialogViewModel(title, shift);
            _viewModel.RequestClose += OnRequestClose;
            DataContext = _viewModel;
        }

        private void ColorPicker_SelectedColorChanged(object sender, HandyControl.Data.FunctionEventArgs<Color> e)
        {
            if (DataContext is ShiftEditDialogViewModel viewModel)
            {
                // 将新选择的颜色转换为字符串并更新ViewModel的Color属性
                var newColor = e.Info;
                viewModel.Color = newColor.ToString();
            }
        }

        private void OnRequestClose(bool dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }
    }
}
