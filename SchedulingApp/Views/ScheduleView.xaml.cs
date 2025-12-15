using SchedulingApp.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SchedulingApp.Views
{
    public partial class ScheduleView : UserControl
    {
        public ScheduleView()
        {
            InitializeComponent();
            // Subscribe to the data context changed event to update columns when schedule is generated
            this.Loaded += ScheduleView_Loaded;
        }

        private void ScheduleView_Loaded(object sender, RoutedEventArgs e)
        {
            // Subscribe to when the data context changes to update the columns
            if (DataContext is ScheduleViewModel viewModel)
            {
                // Listen for when the schedule is updated to regenerate columns
                viewModel.PropertyChanged += ViewModel_PropertyChanged;

                // Also listen for changes to the collections themselves to update columns when content changes
                if (viewModel.StaffStatistics is System.Collections.Specialized.INotifyCollectionChanged staffStatsCollection)
                {
                    staffStatsCollection.CollectionChanged += (s, e) => UpdateStaffStatisticsColumns();
                }
                if (viewModel.DailyStatistics is System.Collections.Specialized.INotifyCollectionChanged dailyStatsCollection)
                {
                    dailyStatsCollection.CollectionChanged += (s, e) => UpdateDailyStatisticsColumns();
                }
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e?.PropertyName == nameof(ScheduleViewModel.StaffWithSchedules) ||
                e?.PropertyName == nameof(ScheduleViewModel.DateHeaders))
            {
                UpdateDataGridColumns();
            }

            // Update statistics grids when statistics data changes
            if (e?.PropertyName == nameof(ScheduleViewModel.DailyStatistics))
            {
                UpdateDailyStatisticsColumns();
            }

            if (e?.PropertyName == nameof(ScheduleViewModel.StaffStatistics))
            {
                UpdateStaffStatisticsColumns();
            }
        }

        private void UpdateDataGridColumns()
        {
            if (DataContext is ScheduleViewModel viewModel)
            {
                // Clear existing date columns (keep the first 3 columns: Name, ID, Group)
                var baseColumnsCount = 3; // Name, ID, Group
                while (ScheduleDataGrid.Columns.Count > baseColumnsCount)
                {
                    ScheduleDataGrid.Columns.RemoveAt(baseColumnsCount);
                }

                // Add date columns based on DateHeaders
                foreach (var dateHeader in viewModel.DateHeaders)
                {
                    var templateColumn = new DataGridTemplateColumn
                    {
                        Header = dateHeader,
                        Width = 60 // Increased width for better visibility
                    };

                    // Create cell template to display shift with color as background
                    var cellTemplate = new DataTemplate();

                    // Use a factory method to create the visual tree - Border with color background
                    var factory = new FrameworkElementFactory(typeof(Border));
                    factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(1));
                    // Set the background color dynamically based on the shift color
                    var backgroundBinding = new Binding($"DateShifts[{dateHeader}].ShiftColor")
                    {
                        Converter = new SchedulingApp.Converters.ColorConverter()
                    };
                    factory.SetBinding(Border.BackgroundProperty, backgroundBinding);
                    factory.SetValue(Border.BorderBrushProperty, System.Windows.Media.Brushes.Black);
                    factory.SetValue(Border.BorderThicknessProperty, new Thickness(0.5));
                    factory.SetValue(FrameworkElement.HeightProperty, 32.0);
                    factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
                    factory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);

                    // TextBlock for the shift name (with white or contrasting text color)
                    var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
                    textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding($"DateShifts[{dateHeader}].ShiftName"));
                    textBlockFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
                    textBlockFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                    textBlockFactory.SetValue(TextBlock.FontSizeProperty, 14.0); // Increased font size for better readability
                    textBlockFactory.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Black); // Use black text for visibility

                    factory.AppendChild(textBlockFactory);
                    cellTemplate.VisualTree = factory;
                    templateColumn.CellTemplate = cellTemplate;

                    ScheduleDataGrid.Columns.Add(templateColumn);
                }
            }
        }

        private void UpdateDailyStatisticsColumns()
        {
            if (DataContext is ScheduleViewModel viewModel)
            {
                // Clear existing date columns (keep the first column: Shift Type)
                var baseColumnsCount = 1; // Shift Type
                while (DailyStatisticsGrid.Columns.Count > baseColumnsCount)
                {
                    DailyStatisticsGrid.Columns.RemoveAt(baseColumnsCount);
                }

                // Add date columns based on DateHeaders
                foreach (var dateHeader in viewModel.DateHeaders)
                {
                    var column = new DataGridTextColumn
                    {
                        Header = dateHeader,
                        Width = 60,
                        Binding = new Binding($"DateCounts[{dateHeader}]")
                    };
                    DailyStatisticsGrid.Columns.Add(column);
                }
            }
        }

        private void UpdateStaffStatisticsColumns()
        {
            if (DataContext is ScheduleViewModel viewModel)
            {
                // Clear existing shift columns (keep the first column: Staff Name)
                var baseColumnsCount = 1; // Staff Name
                while (StaffStatisticsGrid.Columns.Count > baseColumnsCount)
                {
                    StaffStatisticsGrid.Columns.RemoveAt(baseColumnsCount);
                }

                // Get all possible shift types from system shift definitions first
                var allShiftTypes = new HashSet<string>();

                // Add all shift types from the system shift definitions
                foreach (var shift in viewModel.ShiftsData)
                {
                    allShiftTypes.Add(shift.ShiftName);
                }

                // Then add any additional shift types that might exist in the current statistics
                // (in case there are shifts in the schedule that aren't in the main shift definitions)
                foreach (var staffStat in viewModel.StaffStatistics)
                {
                    foreach (var shiftType in staffStat.ShiftCounts.Keys)
                    {
                        allShiftTypes.Add(shiftType);
                    }
                }

                // Create columns for all possible shift types
                foreach (var shiftType in allShiftTypes)
                {
                    var column = new DataGridTextColumn
                    {
                        Header = shiftType,
                        Width = 60,
                        Binding = new Binding($"ShiftCounts[{shiftType}]")
                    };
                    StaffStatisticsGrid.Columns.Add(column);
                }

                // Force the DataGrid to refresh to show the new columns
                StaffStatisticsGrid.Items.Refresh();
            }
        }

        private void ScheduleDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}