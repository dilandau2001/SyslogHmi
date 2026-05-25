namespace SyslogHmi.ViewModels
{
    public class FacilityCheckItem : ViewModelBase
    {
        public int Level
        {
            get;
            set => SetProperty(ref field, value);
        }

        public string Name
        {
            get;
            set => SetProperty(ref field, value);
        } = string.Empty;

        public bool IsChecked
        {
            get;
            set => SetProperty(ref field, value);
        }
    }
}
