using SchedulingApp.Models;
using SchedulingApp.ViewModels;
using System.Windows;

namespace SchedulingApp.Views.Dialogs
{
    public partial class StaffEditDialog : Window
    {
        private StaffEditDialogViewModel _viewModel;

        public StaffModel Staff { get; private set; }

        public StaffEditDialog(string title, StaffModel staff)
        {
            Staff = staff;

            InitializeComponent();

            // 创建并设置ViewModel
            _viewModel = new StaffEditDialogViewModel(title, staff);
            _viewModel.RequestClose += OnRequestClose;
            DataContext = _viewModel;
        }

        private void OnRequestClose(bool dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }
    }
}

