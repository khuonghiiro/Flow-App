using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlowMy.Models.Nodes
{
    public sealed class OverlayItem : INotifyPropertyChanged
    {
        private string _type = "text";
        private string _source = string.Empty;
        private double _x = 0.1;
        private double _y = 0.1;
        private double _width = 0.2;
        private double _height = 0.2;
        private double _rotation;
        private double _opacity = 1.0;
        private string _fontFamily = "Arial";
        private string _fontColor = "white";
        private int _fontSize = 32;
        private string _textAlignment = "Left";
        private bool _isSelected;
        private bool _isVisible = true;
        private bool _isLocked;

        public string Type
        {
            get => _type;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "text" : value.Trim().ToLowerInvariant();
                if (_type != next) { _type = next; OnPropertyChanged(); }
            }
        }

        public string Source
        {
            get => _source;
            set
            {
                var next = value ?? string.Empty;
                if (_source != next) { _source = next; OnPropertyChanged(); }
            }
        }

        public double X
        {
            get => _x;
            set
            {
                var next = Math.Clamp(value, 0, 1);
                if (Math.Abs(_x - next) > 0.0001) { _x = next; OnPropertyChanged(); }
            }
        }

        public double Y
        {
            get => _y;
            set
            {
                var next = Math.Clamp(value, 0, 1);
                if (Math.Abs(_y - next) > 0.0001) { _y = next; OnPropertyChanged(); }
            }
        }

        public double Width
        {
            get => _width;
            set
            {
                var next = Math.Clamp(value, 0.01, 1);
                if (Math.Abs(_width - next) > 0.0001) { _width = next; OnPropertyChanged(); }
            }
        }

        public double Height
        {
            get => _height;
            set
            {
                var next = Math.Clamp(value, 0.01, 1);
                if (Math.Abs(_height - next) > 0.0001) { _height = next; OnPropertyChanged(); }
            }
        }

        public double Rotation
        {
            get => _rotation;
            set
            {
                if (Math.Abs(_rotation - value) > 0.0001) { _rotation = value; OnPropertyChanged(); }
            }
        }

        public double Opacity
        {
            get => _opacity;
            set
            {
                var next = Math.Clamp(value, 0, 1);
                if (Math.Abs(_opacity - next) > 0.0001) { _opacity = next; OnPropertyChanged(); }
            }
        }

        public string FontFamily
        {
            get => _fontFamily;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "Arial" : value.Trim();
                if (_fontFamily != next) { _fontFamily = next; OnPropertyChanged(); }
            }
        }

        public string FontColor
        {
            get => _fontColor;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "white" : value.Trim();
                if (_fontColor != next) { _fontColor = next; OnPropertyChanged(); }
            }
        }

        public int FontSize
        {
            get => _fontSize;
            set
            {
                var next = value < 8 ? 8 : (value > 400 ? 400 : value);
                if (_fontSize != next) { _fontSize = next; OnPropertyChanged(); }
            }
        }

        /// <summary>Only used when <see cref="Type"/> is "text". Values: Left, Center, Right.</summary>
        public string TextAlignment
        {
            get => _textAlignment;
            set
            {
                var next = string.IsNullOrWhiteSpace(value) ? "Left" : value.Trim();
                next = next.Equals("center", StringComparison.OrdinalIgnoreCase) ? "Center"
                    : next.Equals("right", StringComparison.OrdinalIgnoreCase) ? "Right"
                    : "Left";
                if (_textAlignment != next) { _textAlignment = next; OnPropertyChanged(); }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set { if (_isVisible != value) { _isVisible = value; OnPropertyChanged(); } }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set { if (_isLocked != value) { _isLocked = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
