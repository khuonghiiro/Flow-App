using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace FlowMy.Helpers
{
    public class LoadingDotHelper : INotifyPropertyChanged
    {
        public static LoadingDotHelper Instance { get; } = new LoadingDotHelper();

        private string _dots = "";
        private readonly DispatcherTimer _timer;

        private LoadingDotHelper()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (s, e) =>
            {
                _dots = _dots.Length >= 3 ? "" : _dots + ".";
                OnPropertyChanged(nameof(Dots));
            };
            _timer.Start();
        }

        public string Dots => _dots;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
