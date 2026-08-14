using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Models
{
    public class WorkflowItemInfo : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private DateTime _lastModified;
        private bool _isSelected;

        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        public DateTime LastModified
        {
            get => _lastModified;
            set
            {
                if (_lastModified != value)
                {
                    _lastModified = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LastModifiedText));
                }
            }
        }

        public string LastModifiedText => LastModified != DateTime.MinValue ? LastModified.ToString("dd/MM/yyyy HH:mm") : "—";

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
