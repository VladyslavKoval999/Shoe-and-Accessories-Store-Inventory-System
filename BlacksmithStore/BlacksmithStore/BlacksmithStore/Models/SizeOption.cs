using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BlacksmithStore.Models
{
    public class SizeOption : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Value { get; set; }
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}