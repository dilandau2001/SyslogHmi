using System.Windows.Controls;
using SyslogHmi.ViewModels;

namespace SyslogHmi.Views
{
    /// <summary>
    /// Code-behind for SqlAnalysisView.
    /// Provides UI logic for the SQL Analysis view.
    /// </summary>
    public partial class SqlAnalysisView : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the SqlAnalysisView class.
        /// </summary>
        public SqlAnalysisView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Called when the view is loaded.
        /// Sets the DataContext to SqlAnalysisViewModel from the parent MainViewModel if available.
        /// </summary>
        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // If DataContext is MainViewModel, extract the SqlAnalysisViewModel property
            if (DataContext is MainViewModel mainViewModel)
            {
                DataContext = mainViewModel.SqlAnalysisViewModel;
            }
        }
    }
}
