using FlowMy.Services.Rendering;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FlowMy.Views
{
    public partial class WorkflowEditorWindow
    {
        /// <summary>
        /// Initialize GPU settings UI (ComboBox items, CheckBox state)
        /// </summary>
        private void InitializeGpuSettingsUI()
        {
            if (GpuQualityComboBox == null || GpuEnabledCheckBox == null) return;
            
            // Populate ComboBox with quality options
            GpuQualityComboBox.Items.Clear();
            foreach (GpuRenderQuality quality in System.Enum.GetValues(typeof(GpuRenderQuality)))
            {
                GpuQualityComboBox.Items.Add(GpuRenderQualityHelper.GetDisplayName(quality));
            }
            
            // Set current selection
            GpuQualityComboBox.SelectedIndex = (int)_gpuRenderQuality;
            
            // Set CheckBox state
            GpuEnabledCheckBox.IsChecked = _gpuEnabled;
            
            // Update ComboBox enabled state based on CheckBox
            UpdateGpuQualityComboBoxEnabled();
            UpdateGpuStatusInfo();
        }
        
        /// <summary>
        /// Update ComboBox and MenuItem enabled state based on CheckBox
        /// </summary>
        private void UpdateGpuQualityComboBoxEnabled()
        {
            if (GpuQualityComboBox != null && GpuEnabledCheckBox != null && GpuQualityMenuItem != null)
            {
                bool isEnabled = GpuEnabledCheckBox.IsChecked == true;
                //GpuQualityComboBox.IsEnabled = isEnabled;
                //GpuQualityMenuItem.IsEnabled = isEnabled;
                GpuQualityComboBox.IsEnabled = true;
                GpuQualityMenuItem.IsEnabled = true;
            }
        }
        
        /// <summary>
        /// Event handler khi CheckBox GPU Enabled thay đổi
        /// </summary>
        private void GpuEnabledCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            GpuEnabled = true;
            UpdateGpuQualityComboBoxEnabled();
            UpdateGpuStatusInfo();
        }
        
        /// <summary>
        /// Event handler khi CheckBox GPU Enabled bị uncheck
        /// </summary>
        private void GpuEnabledCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            GpuEnabled = false;
            UpdateGpuQualityComboBoxEnabled();
            UpdateGpuStatusInfo();
        }
        
        /// <summary>
        /// Event handler khi ComboBox Quality thay đổi
        /// </summary>
        private void GpuQualityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GpuQualityComboBox?.SelectedIndex >= 0)
            {
                GpuRenderQuality = (GpuRenderQuality)GpuQualityComboBox.SelectedIndex;
                UpdateGpuStatusInfo();
            }
        }

        private void UpdateGpuStatusInfo()
        {
            if (GpuStatusLineText == null || GpuStatusRenderModeText == null || GpuStatusTierText == null || GpuStatusNameText == null)
                return;

            var tier = GpuDetectionHelper.RenderTier;
            var wpfTier = RenderCapability.Tier >> 16;
            var tierLabel = tier switch
            {
                0 => "Tier 0 (CPU/Software)",
                1 => "Tier 1 (Partial GPU)",
                2 => "Tier 2 (Full GPU)",
                _ => $"Tier {tier}"
            };

            var gpuDetected = GpuDetectionHelper.IsGpuAvailable;
            var gpuRequested = GpuEnabledCheckBox?.IsChecked == true;
            var modeText = gpuRequested
                ? (gpuDetected && wpfTier > 0 ? "Đang dùng GPU acceleration" : "GPU bật nhưng WPF chưa dùng hardware tier")
                : "Đang dùng chế độ CPU (GPU tắt trong setting)";
            var renderModeText = wpfTier switch
            {
                2 => "WPF Render: Hardware (Tier 2)",
                1 => "WPF Render: Partial Hardware (Tier 1)",
                _ => "WPF Render: Software (Tier 0)"
            };
            var renderBrush = wpfTier switch
            {
                2 => Brushes.LimeGreen,
                1 => Brushes.Goldenrod,
                _ => Brushes.IndianRed
            };

            GpuStatusLineText.Text = $"GPU Status: {modeText}";
            GpuStatusRenderModeText.Text = renderModeText;
            GpuStatusRenderModeText.Foreground = renderBrush;
            GpuStatusTierText.Text = $"Render Tier: {tierLabel}";
            GpuStatusNameText.Text = $"Device: {GpuDetectionHelper.GpuName}";
        }

        private void CopyGpuDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wpfTier = RenderCapability.Tier >> 16;
                var gpuRequested = GpuEnabledCheckBox?.IsChecked == true;
                var diagnostics = string.Join(System.Environment.NewLine, new[]
                {
                    "[GPU Diagnostics]",
                    $"GPU Enabled (setting): {gpuRequested}",
                    $"GPU Detected: {GpuDetectionHelper.IsGpuAvailable}",
                    $"GPU Name: {GpuDetectionHelper.GpuName}",
                    $"GpuDetectionHelper.RenderTier: {GpuDetectionHelper.RenderTier}",
                    $"WPF RenderCapability Tier: {wpfTier}",
                    $"GpuRenderQuality: {_gpuRenderQuality}",
                    $"Timestamp: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}"
                });

                Clipboard.SetText(diagnostics);
                MessageBox.Show(
                    "Đã copy GPU diagnostics vào clipboard.",
                    "GPU Diagnostics",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Không thể copy diagnostics: {ex.Message}",
                    "GPU Diagnostics",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}

