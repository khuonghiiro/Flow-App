using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace FlowMy.Models.Nodes
{
    public sealed class ScreenCaptureNode : WorkflowNode, INotifyPropertyChanged
    {
        private int _captureX;
        private int _captureY;
        private int _captureWidth;
        private int _captureHeight;
        private BitmapImage? _capturedImage;  // THÊM

        public ScreenCaptureNode()
        {
            Type = NodeType.ScreenCapture;
            Title = "Screen Capture";
        }

        public int CaptureX
        {
            get => _captureX;
            set
            {
                if (_captureX != value)
                {
                    _captureX = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CaptureY
        {
            get => _captureY;
            set
            {
                if (_captureY != value)
                {
                    _captureY = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CaptureWidth
        {
            get => _captureWidth;
            set
            {
                if (_captureWidth != value)
                {
                    _captureWidth = value;
                    OnPropertyChanged();
                }
            }
        }

        public int CaptureHeight
        {
            get => _captureHeight;
            set
            {
                if (_captureHeight != value)
                {
                    _captureHeight = value;
                    OnPropertyChanged();
                }
            }
        }

        // THÊM: Property lưu ảnh
        public BitmapImage? CapturedImage
        {
            get => _capturedImage;
            set
            {
                if (_capturedImage != value)
                {
                    _capturedImage = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasCaptureRegion => CaptureWidth > 0 && CaptureHeight > 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
