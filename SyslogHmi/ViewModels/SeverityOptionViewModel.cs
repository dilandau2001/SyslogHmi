namespace SyslogHmi.ViewModels
{
    public class SeverityOptionViewModel : ViewModelBase // O la clase base que uses para notificar cambios
    {
        public string Name { get; set; }
        public int Tag { get; set; } // El ID numérico de la severidad (0 al 7)

        public bool IsChecked
        {
            get;
            set
            {
                field = value;
                OnPropertyChanged();
            } // Cambia OnPropertyChanged por tu método de notificación
        }
    }
}
