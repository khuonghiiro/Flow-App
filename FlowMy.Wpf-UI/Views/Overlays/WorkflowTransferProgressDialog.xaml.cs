using System.Windows;
using FlowMy.Services.Workflow;

namespace FlowMy.Views.Overlays
{
    public partial class WorkflowTransferProgressDialog : Window
    {
        public event EventHandler? CancellationRequested;

        public WorkflowTransferProgressDialog()
        {
            InitializeComponent();
        }

        public void Report(WorkflowTransferProgress p)
        {
            void apply()
            {
                StatusText.Text = p.Message;
                var v = p.Percent;
                if (v < 0) v = 0;
                if (v > 100) v = 100;
                ProgressBar.Value = v;
            }

            if (Dispatcher.CheckAccess())
                apply();
            else
                Dispatcher.Invoke(apply);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelButton.IsEnabled = false;
            CancellationRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
