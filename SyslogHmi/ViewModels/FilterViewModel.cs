using System;
using SyslogHmi.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace SyslogHmi.ViewModels
{
    public class FilterViewModel : ViewModelBase
    {
        public string HostnameFilter
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        public string AppNameFilter
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        public string MessageFilter
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        public int? SelectedSeverity
        {
            get;
            set => SetProperty(ref field, value);
        }

        public DateTime? StartTime
        {
            get;
            set
            {
                SetProperty(ref field, value);
                ValidateTimeRange();
            }
        } = null;

        public DateTime? EndTime
        {
            get;
            set
            {
                SetProperty(ref field, value);
                ValidateTimeRange();
            }
        } = null;

        public int StartHour
        {
            get;
            set => SetProperty(ref field, value);
        }
        public int StartMinute
        {
            get;
            set => SetProperty(ref field, value);
        }

        public int EndHour
        {
            get;
            set => SetProperty(ref field, value);
        }
        public int EndMinute
        {
            get;
            set => SetProperty(ref field, value);
        }

        public ObservableCollection<int> Hours { get; } = new ObservableCollection<int>(Enumerable.Range(0, 24));
        public ObservableCollection<int> Minutes { get; } = new ObservableCollection<int>(Enumerable.Range(0, 60));

        public ObservableCollection<SeverityOptionViewModel> SeverityOptions { get; set; }

        public int? SelectedFacility
        {
            get;
            set => SetProperty(ref field, value);
        }

        public bool AutoScroll
        {
            get;
            set => SetProperty(ref field, value);
        } = true;

        public ICommand ClearFiltersCommand { get; }

        public FilterViewModel()
        {
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

            // Inicializamos la lista con todas las opciones marcadas por defecto
            SeverityOptions =
            [
                new SeverityOptionViewModel() { Name = "Emergency", Tag = 0, IsChecked = true },
                new SeverityOptionViewModel() { Name = "Alert", Tag = 1, IsChecked = true },
                new SeverityOptionViewModel() { Name = "Critical", Tag = 2, IsChecked = true },
                new SeverityOptionViewModel() { Name = "Error", Tag = 3, IsChecked = true },
                new SeverityOptionViewModel() { Name = "Warning", Tag = 4, IsChecked = true },
                new SeverityOptionViewModel() { Name = "Notice", Tag = 5, IsChecked = true },
                new SeverityOptionViewModel() { Name = "Info", Tag = 6, IsChecked = true },
                new SeverityOptionViewModel() { Name = "Debug", Tag = 7, IsChecked = true }
            ];

            // Suscribirse a los cambios de los checkboxes para refrescar la lista de mensajes automáticamente
            foreach (var option in SeverityOptions)
            {
                option.PropertyChanged += (s, e) => {
                    if (e.PropertyName == nameof(SeverityOptionViewModel.IsChecked))
                    {
                        FiltersChanged?.Invoke(this, EventArgs.Empty); 
                    }
                };
            }

            PropertyChanged += (sender, args) => FiltersChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler FiltersChanged;  

        private void ClearFilters()
        {
            HostnameFilter = string.Empty;
            AppNameFilter = string.Empty;
            MessageFilter = string.Empty;
            SelectedSeverity = null;
            SelectedFacility = null;
            AutoScroll = true;
            StartTime = null;
            EndTime = null;

            foreach (var severityOptionViewModel in SeverityOptions)
            {
                severityOptionViewModel.IsChecked = true;
            }
        }

        public bool MatchesFilters(SyslogMessage message)
        {
            if (!string.IsNullOrWhiteSpace(HostnameFilter))
            {
                var hostLines = HostnameFilter
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .ToList();

                if (hostLines.Count > 0)
                {
                    var matchesAnyHost = hostLines.Any(line =>
                        message.Hostname != null &&
                        message.Hostname.Contains(line, StringComparison.OrdinalIgnoreCase));

                    if (!matchesAnyHost) return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(AppNameFilter))
            {
                var appnames = AppNameFilter
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .ToList();

                if (appnames.Count > 0)
                {
                    var matchesAnyHost = appnames.Any(line =>
                        message.AppName != null &&
                        message.AppName.Contains(line, StringComparison.OrdinalIgnoreCase));

                    if (!matchesAnyHost) return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(MessageFilter))
            {
                var messages = MessageFilter
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())    
                    .ToList();

                if (messages.Count > 0)
                {
                    var matchesAnyHost = messages.Any(line =>
                        message.Message != null &&
                        message.Message.Contains(line, StringComparison.OrdinalIgnoreCase));

                    if (!matchesAnyHost) return false;
                }
            }

            var option = SeverityOptions.FirstOrDefault(o => o.Tag == (int)message.Severity);
            if (option != null && !option.IsChecked)
            {
                return false; // Si existe la opción pero no está marcada, ocultamos el mensaje
            }

            if (SelectedFacility.HasValue && (int)message.Facility != SelectedFacility.Value)
                return false;

            if (StartTime.HasValue)
            {
                var fullStart = GetCombinedDate(StartTime, StartHour, StartMinute);
                if (message.Timestamp < fullStart) return false;
            }

            if (EndTime.HasValue)
            {
                var fullEnd = GetCombinedDate(EndTime, EndHour, EndMinute);
                if (message.Timestamp > fullEnd) return false;
            }

            return true;
        }

        private void ValidateTimeRange()
        {
            if (StartTime.HasValue && EndTime.HasValue && EndTime.Value < StartTime.Value)
            {
                // Si el usuario pone un fin anterior al inicio, igualamos el fin al inicio
                EndTime = StartTime;
                OnPropertyChanged(nameof(EndTime));
            }
        }

        private DateTime GetCombinedDate(DateTime? date, int hour, int minute)
        {
            if (!date.HasValue) return DateTime.MinValue;
            return new DateTime(date.Value.Year, date.Value.Month, date.Value.Day, hour, minute, 0);
        }
    }
}
