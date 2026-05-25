using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace SyslogHmi.Views
{
    public partial class MessagesView : UserControl
    {
        public MessagesView()
        {
            InitializeComponent();

            // Force default column order by Timestamp descending
            TimestampColumn.SortDirection = ListSortDirection.Descending;
        }

        // Este manejador controlará el clic de cualquier MenuItem dentro del ContextMenu
        private void ColumnToggle_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is MenuItem menuItem && menuItem.Tag != null)
            {
                if (int.TryParse(menuItem.Tag.ToString(), out int columnIndex))
                {
                    var contextMenu = sender as ContextMenu;
                    if (contextMenu?.PlacementTarget is DataGrid dataGrid && columnIndex < dataGrid.Columns.Count)
                    {
                        // Sincronizamos la visibilidad de la columna con el estado del Checkmark
                        dataGrid.Columns[columnIndex].Visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }
    }
}
