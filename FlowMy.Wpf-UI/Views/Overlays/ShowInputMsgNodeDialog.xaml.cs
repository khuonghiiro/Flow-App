// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Models.Nodes;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Utils;
using FlowMy.ViewModels;
using FlowMy.Controls;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace FlowMy.Views.Overlays
{
    public partial class ShowInputMsgNodeDialog : BaseNodeDialog, INodeDialogForceClose
    {
        private const string AiGuidePromptDetailed = @"Bạn đang viết code cho ShowInputMsgNode của FlowMy.
Mục tiêu: AI mới tinh, không biết source code, vẫn làm đúng ngay.

OUTPUT CONTRACT (BẮT BUỘC):
- Trả đúng 4 phần: [HTML], [CSS], [JS], [PARAM].
- Không thêm giải thích ngoài 4 phần.
- [PARAM] là plain text, mỗi dòng: outputKey: cssSelector (hoặc outputKey = cssSelector).

INPUT CONTRACT (KHI SỬA CODE CŨ):
- Code hiện tại sẽ được gửi cho AI theo dạng:
  #region HTML ... #endregion
  #region CSS ... #endregion
  #region JS ... #endregion
  #region PARAM ... #endregion
- Đây chỉ là format để AI đọc context.
- AI trả về kết quả cuối cùng dưới 4 phần [HTML]/[CSS]/[JS]/[PARAM] bình thường.

========================
1) CÁCH HỆ THỐNG HOẠT ĐỘNG
========================
- Input mapping từ node khác sẽ được push vào runtime JS qua window.hostLive.
- Dùng realtime callback:
  window.hostLive.on('price', function(price) { ... });
  window.hostLive.on('price', 'quantity', function(price, quantity) { ... });
  window.hostLive.on(function(live) { ... }); // nhận object live tổng quát
- Có thể đọc trực tiếp:
  window.hostLive.values['price']
- Placeholder {variableName} trong HTML/CSS/JS sẽ được C# replace trước khi render.
  Ví dụ: {price}, {userId}, {apiResponse}
- hostSubmit(): gửi tín hiệu để host đọc DOM theo [PARAM] và ghi outputs.
- Với dữ liệu local file (ví dụ datas[guid] = C:\path\video.mp4):
  KHÔNG gán file:/// trực tiếp vào video.src.
  Phải gọi hostResolvePath(localPath, requestId) rồi dùng d.localUrl từ event hostPathResolved.

========================
2) FORMAT [PARAM] BẮT BUỘC
========================
- [PARAM] là plain text, mỗi dòng:
  outputKey: cssSelector
  hoặc
  outputKey = cssSelector
- KHÔNG dùng JSON cho [PARAM].
- Host sẽ querySelector(selector):
  - nếu element có value -> lấy value
  - ngược lại lấy textContent

Ví dụ [PARAM]:
resultText: #result
emailValue: #emailInput
statusText: .status-label

========================
3) YÊU CẦU CHẤT LƯỢNG CODE
========================
- [HTML]: có id/class rõ ràng, có nút action, có vùng status.
- [CSS]: tự chứa, không cần bundler/framework.
- [JS]:
  - vanilla JS, không import module.
  - có realtime handler (window.hostLive.on).
  - có xử lý null/undefined.
  - có try/catch ở action chính.
  - có gọi hostSubmit(), có thể gọi hostStart().
- Code phải copy/paste chạy được ngay trong 4 tab HTML/CSS/JS/Params.

========================
4) VÍ DỤ MẪU THAM KHẢO
========================
[HTML]
<div class='panel'>
  <h3>Dashboard realtime</h3>
  <div class='row'>Giá: <span id='priceValue'>—</span></div>
  <div class='row'>Số lượng: <span id='qtyValue'>—</span></div>
  <div class='row'>Tổng: <span id='totalValue'>—</span></div>

  <input id='noteInput' placeholder='Nhập ghi chú...' />
  <button id='btnRun'>Gửi dữ liệu</button>

  <div id='statusText' class='status'>Sẵn sàng</div>
  <div id='resultBox'>—</div>
</div>

[CSS]
* { box-sizing: border-box; }
body {
  margin: 0;
  padding: 16px;
  font-family: 'Segoe UI', sans-serif;
  background: #0f172a;
  color: #e2e8f0;
}
.panel {
  max-width: 420px;
  margin: 0 auto;
  border: 1px solid #334155;
  border-radius: 10px;
  background: #111827;
  padding: 14px;
}
.row { margin: 6px 0; }
#priceValue, #qtyValue, #totalValue {
  font-weight: 700;
  color: #38bdf8;
}
#btnRun {
  margin-top: 10px;
  padding: 8px 12px;
  border: 0;
  border-radius: 8px;
  background: #2563eb;
  color: white;
  cursor: pointer;
}
#btnRun[disabled] { opacity: .6; cursor: not-allowed; }
.status { margin-top: 10px; font-size: 12px; color: #94a3b8; }
#resultBox {
  margin-top: 8px;
  border: 1px dashed #334155;
  border-radius: 8px;
  padding: 8px;
  min-height: 26px;
}

[JS]
(function () {
  var priceEl = document.getElementById('priceValue');
  var qtyEl = document.getElementById('qtyValue');
  var totalEl = document.getElementById('totalValue');
  var statusEl = document.getElementById('statusText');
  var resultEl = document.getElementById('resultBox');
  var noteInput = document.getElementById('noteInput');
  var btnRun = document.getElementById('btnRun');

  function setStatus(msg, isOk) {
    if (!statusEl) return;
    statusEl.textContent = msg;
    statusEl.style.color = isOk ? '#4ade80' : '#f87171';
  }

  function toNumber(v) {
    var n = parseFloat(v);
    return isNaN(n) ? 0 : n;
  }

  if (window.hostLive && typeof window.hostLive.on === 'function') {
    window.hostLive.on('price', 'quantity', function (price, quantity) {
      var p = (price == null || price === '') ? '—' : String(price);
      var q = (quantity == null || quantity === '') ? '—' : String(quantity);
      if (priceEl) priceEl.textContent = p;
      if (qtyEl) qtyEl.textContent = q;

      var total = toNumber(price) * toNumber(quantity);
      if (totalEl) totalEl.textContent = String(total);
    });
  }

  setInterval(function () {
    try {
      var live = (window.hostLive && window.hostLive.values) ? window.hostLive.values : {};
      if (live.price != null && priceEl) priceEl.textContent = String(live.price);
    } catch (_) { }
  }, 500);

  if (btnRun) {
    btnRun.addEventListener('click', function () {
      try {
        btnRun.disabled = true;
        setStatus('Đang gửi...', true);

        if (resultEl && noteInput) {
          resultEl.textContent = noteInput.value || '';
        }

        if (typeof hostSubmit === 'function') hostSubmit();

        setStatus('Đã gửi submit', true);
      } catch (err) {
        setStatus('Lỗi: ' + (err && err.message ? err.message : err), false);
      } finally {
        btnRun.disabled = false;
      }
    });
  }
})();

[PARAM]
resultText: #resultBox
noteValue: #noteInput
statusText: #statusText";

        private const string ApiDocAllText = @"LUỒNG TỔNG QUAN (JS ↔ C# Host):
1) Realtime input: host push vào window.hostLive
2) JS render UI / xử lý business
3) Nếu cần local file: resolve path -> localUrl qua hostResolvePath
4) Khi user bấm submit: hostSubmit() để host đọc DOM theo PARAM

window.hostLive.on('key', cb)
- Mục đích: nhận realtime value từ InputMappings.
- Ví dụ: window.hostLive.on('datas', function(v){ /* parse datas */ });

window.hostLive.on(function(live){ ... })
- Mục đích: nhận toàn bộ snapshot live object.
- Ví dụ: var v = live['price'];

window.hostLive.values
- Mục đích: đọc state hiện tại (polling/fallback).
- Ví dụ: setInterval(function(){ out.textContent = window.hostLive.values.price || ''; }, 500);

hostResolvePath(localPath, requestId)
- Mục đích: đổi local path -> localUrl dạng https://localfiles-*.local.
- Ví dụ: hostResolvePath('C:\\path\\video.mp4', 'req1');
- Lưu ý: không gán file:/// trực tiếp vào img/video trong HTML UI.

hostPathResolved (event)
- Mục đích: nhận kết quả resolve path.
- Ví dụ:
  window.addEventListener('hostPathResolved', function(ev){
    var d = ev.detail || {};
    if (d.requestId === 'req1' && d.ok && d.localUrl) {
      video.src = d.localUrl; video.load();
    }
  });

hostCurl(rawCurl, fileName, key)
- Mục đích: yêu cầu host tải file bằng curl và trả kết quả local.
- Ví dụ: hostCurl(rawCurl, 'a.mp4', 'k1');

hostCurlDone (event)
- Mục đích: nhận trạng thái download + localUrl/path
- Ví dụ:
  window.addEventListener('hostCurlDone', function(ev){
    var d = ev.detail || {};
    if (d.ok) video.src = d.localUrl;
  });

hostPickImages(requestId)
- Mục đích: Mở native picker để lấy ảnh + full path tuyệt đối
- Ví dụ: hostPickImages('img_req_1');

hostImagesPicked (event)
- Mục đích: Nhận danh sách ảnh host đã chọn (có path + dataUrl)
- Ví dụ:
  window.addEventListener('hostImagesPicked', function(ev){
    var d = ev.detail || {};
    if (d.ok) { console.log(d.files); }
  });

hostSubmit()
- Mục đích: Yêu cầu host đọc DOM theo PARAM";

        private const string TemplateRealtimeJs =
@"(function(){
  // HTML gợi ý:
  // <div id=""label""></div>
  // <div id=""value""></div>
  var label = document.getElementById('label');
  var value = document.getElementById('value');
  if (window.hostLive && typeof window.hostLive.on === 'function') {
    // Realtime từ InputMappings
    window.hostLive.on('price', 'datas', function(price, datas){
      if (label) label.textContent = 'Realtime';
      if (value) value.textContent = (price != null && price !== '') ? String(price) : JSON.stringify(datas || {});
    });
  }
})();";

        private const string TemplateLocalVideoJs =
@"(function(){
  // HTML gợi ý:
  // <input id=""localVideoPath"" placeholder=""C:\...\video.mp4"" />
  // <video id=""vid"" controls playsinline></video>
  var video = document.getElementById('vid');
  var videoPathInput = document.getElementById('localVideoPath');
  var req = 'req_' + Date.now();

  window.addEventListener('hostPathResolved', function(ev){
    var d = (ev && ev.detail) || {};
    if (d.requestId !== req) return;
    if (d.ok && d.localUrl && video) {
      video.src = d.localUrl;
      video.load();
    }
  });

  if (typeof hostResolvePath === 'function') {
    var localPath = (videoPathInput && videoPathInput.value) ? videoPathInput.value.trim() : 'C:\\path\\video.mp4';
    hostResolvePath(localPath, req);
  }
})();";

        private const string TemplateSubmitFormJs =
@"(function(){
  // HTML gợi ý:
  // <input id=""noteInput"" />
  // <div id=""resultBox""></div>
  // <button id=""btnRun"">Run</button>
  var btn = document.getElementById('btnRun');
  var inp = document.getElementById('noteInput');
  var out = document.getElementById('resultBox');
  if (!btn) return;
  btn.addEventListener('click', function(){
    if (out && inp) out.textContent = inp.value || '';
    if (typeof hostSubmit === 'function') hostSubmit();
  });
})();";

        private const string TemplateFullNewApiJs =
@"(function(){
  // FULL NEW API DEMO
  // HTML gợi ý:
  // <input id=""localImagePath"" placeholder=""C:\...\img.png"" />
  // <input id=""localVideoPath"" placeholder=""C:\...\video.mp4"" />
  // <img id=""previewImage"" />
  // <video id=""previewVideo"" controls playsinline></video>
  // <button id=""btnRun"">Run</button>

  var reqImage = 'img_' + Date.now();
  var reqVideo = 'vid_' + Date.now();
  var img = document.getElementById('previewImage');
  var vid = document.getElementById('previewVideo');
  var imgPath = document.getElementById('localImagePath');
  var vidPath = document.getElementById('localVideoPath');
  var btnRun = document.getElementById('btnRun');

  if (window.hostLive && typeof window.hostLive.on === 'function') {
    window.hostLive.on('datas', function(datas){
      console.log('live datas', datas);
    });
  }

  window.addEventListener('hostPathResolved', function(ev){
    var d = (ev && ev.detail) || {};
    if (!d.ok || !d.localUrl) return;
    if (d.requestId === reqImage && img) img.src = d.localUrl;
    if (d.requestId === reqVideo && vid) {
      vid.src = d.localUrl;
      try { vid.load(); } catch (_) {}
    }
  });

  window.addEventListener('hostCurlDone', function(ev){
    var d = (ev && ev.detail) || {};
    if (d.ok) console.log('curl done:', d.localUrl || d.path);
  });

  window.addEventListener('hostImagesPicked', function(ev){
    var d = (ev && ev.detail) || {};
    if (d.ok) console.log('images picked:', d.files);
  });

  if (typeof hostResolvePath === 'function') {
    hostResolvePath((imgPath && imgPath.value) ? imgPath.value.trim() : 'C:\\path\\image.png', reqImage);
    hostResolvePath((vidPath && vidPath.value) ? vidPath.value.trim() : 'C:\\path\\video.mp4', reqVideo);
  }
  if (typeof hostCurl === 'function') hostCurl('curl --location ""https://example.com/a.mp4""', 'a.mp4', 'k1');
  if (typeof hostPickImages === 'function') hostPickImages('img_req_1');

  if (btnRun) {
    btnRun.addEventListener('click', function(){
      if (typeof hostSubmit === 'function') hostSubmit();
    });
  }
})();";

        private readonly ShowInputMsgNodeDialogViewModel _viewModel;
        private bool _isClosePrepared;
        private bool _deferCloseInProgress;
        private bool _forceClose;
        private CancellationTokenSource? _prepareCts;

        private readonly List<SyntaxHighlightCodeEditor> _jsEditors = new();
        private readonly Dictionary<SyntaxHighlightCodeEditor, JsTabMeta> _jsTabMeta = new();
        private const string MultiJsTabMarkerPrefix = "// [FLOW_JS_TAB:";
        private bool _isSyncingJsTabsToViewModel;

        private sealed class JsTabMeta
        {
            public int Priority { get; set; }
            public string Title { get; set; } = string.Empty;
        }

        private System.Collections.ObjectModel.ObservableCollection<PresetDisplayItem> _jsPresets  = new();
        private System.Collections.ObjectModel.ObservableCollection<PresetDisplayItem> _cssPresets = new();

        public ShowInputMsgNodeDialog(ShowInputMsgNode node, IWorkflowEditorHost host, Window? owner)
            : base()
        {
            InitializeComponent();
            _viewModel = new ShowInputMsgNodeDialogViewModel(node, host);
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            InitializeBase(_viewModel, owner ?? Application.Current?.MainWindow);

            UpdateTitleColorPreview();

            Closing += ShowInputMsgNodeDialog_Closing;
            Closed += ShowInputMsgNodeDialog_ClosedCleanup;
        }

        private void ShowInputMsgNodeDialog_ClosedCleanup(object? sender, EventArgs e)
        {
            Closed -= ShowInputMsgNodeDialog_ClosedCleanup;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            try { _prepareCts?.Cancel(); } catch { }
            try { _prepareCts?.Dispose(); } catch { }
            _prepareCts = null;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ShowInputMsgNodeDialogViewModel.JsCode) || _isSyncingJsTabsToViewModel)
                return;

            Dispatcher.InvokeAsync(() =>
            {
                if (!IsLoaded) return;
                InitializeJsTabsFromViewModel();
            }, DispatcherPriority.Background);
        }

        public void NotifyHostForceClose()
        {
            try { _prepareCts?.Cancel(); } catch { }
            _deferCloseInProgress = false;
            _forceClose = true;
            try
            {
                Close();
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                _forceClose = false;
            }
        }

        protected override Panel? GetInputsPanel() => InputsPanel;
        protected override Panel? GetOutputsPanel() => OutputsPanel;

        protected override void BeforeSaveOnClose()
        {
            try
            {
                UpdateAllBindings();
            }
            catch { }
        }

        protected override async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            await PrepareBeforeCloseAsync();
            _forceClose = true;
            try { Close(); }
            finally { _forceClose = false; }
        }

        private void ShowInputMsgNodeDialog_Closing(object? sender, CancelEventArgs e)
        {
            if (_forceClose)
                return;

            if (_isClosePrepared)
                return;

            e.Cancel = true;
            if (_deferCloseInProgress)
                return;

            _deferCloseInProgress = true;
            _ = FinishCloseAfterPrepareAsync();
        }

        private async Task FinishCloseAfterPrepareAsync()
        {
            try
            {
                try
                {
                    await PrepareBeforeCloseAsync();
                }
                catch (OperationCanceledException)
                {
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (!IsVisible) return;
                    _forceClose = true;
                    try { Close(); }
                    catch (InvalidOperationException)
                    {
                    }
                    finally { _forceClose = false; }
                }, DispatcherPriority.ApplicationIdle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ShowInputMsgNodeDialog FinishCloseAfterPrepare: {ex.Message}");
            }
            finally
            {
                _deferCloseInProgress = false;
            }
        }

        private async System.Threading.Tasks.Task PrepareBeforeCloseAsync()
        {
            if (_isClosePrepared) return;

            _prepareCts?.Cancel();
            _prepareCts?.Dispose();
            _prepareCts = new CancellationTokenSource();
            var ct = _prepareCts.Token;

            try
            {
                try { UpdateAllBindings(); } catch { }
                await EnsureEnabledOfflineAssetsDownloadedAsync(ct);
                ct.ThrowIfCancellationRequested();
                ViewModel?.SaveTitleCommand?.Execute(null);

                _isClosePrepared = true;
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async System.Threading.Tasks.Task EnsureEnabledOfflineAssetsDownloadedAsync(CancellationToken ct)
        {
            if (_viewModel == null) return;

            var targets = _viewModel.OfflineAssetsList
                .Where(a => a.IsEnabled && !a.IsLocalAvailable)
                .ToList();
            if (targets.Count == 0) return;

            foreach (var item in targets)
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(item.SourceUrl))
                {
                    item.StatusMessage = "⚠ Thiếu URL nguồn, bỏ qua tải tự động";
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.LocalFileName))
                {
                    var guessed = HtmlOfflineAssetService.GuessFileNameFromUrl(item.SourceUrl);
                    if (!string.IsNullOrWhiteSpace(guessed))
                        item.LocalFileName = guessed;
                }

                if (string.IsNullOrWhiteSpace(item.LocalFileName))
                {
                    item.StatusMessage = "⚠ Không xác định được tên file, bỏ qua tải tự động";
                    continue;
                }

                item.IsDownloading = true;
                item.StatusMessage = "⏳ Đang tải tự động trước khi đóng...";
                SetAssetStatus($"⏳ Đang tải: {item.LocalFileName}...");

                try
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    linked.CancelAfter(TimeSpan.FromSeconds(120));
                    var progress = new Progress<string>(msg => item.StatusMessage = msg);
                    var saved = await HtmlOfflineAssetService.DownloadAssetAsync(
                        item.SourceUrl, item.LocalFileName, progress, linked.Token);
                    item.LocalFileName = saved;
                    item.StatusMessage = "✓ Tải xong";
                }
                catch (OperationCanceledException)
                {
                    item.StatusMessage = "Đã hủy tải";
                    throw;
                }
                catch (Exception ex)
                {
                    item.StatusMessage = $"✗ Lỗi tải tự động: {ex.Message}";
                }
                finally
                {
                    item.IsDownloading = false;
                    item.NotifyLocalAvailabilityChanged();
                }
            }

            RefreshPresetStatus();
        }

        private int _lastImportJsOverwriteCount;
        private int _lastImportJsAppendCount;

        private void ExpandHtmlUiButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, "Tính năng Phóng to chỉ hỗ trợ ở Node HTML UI.", "Thông tin", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CopyAllCodeForAiButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is not ShowInputMsgNodeDialogViewModel vm)
                    return;

                UpdateAllBindings();

                var html = vm.HtmlCode ?? string.Empty;
                var css = vm.CssCode ?? string.Empty;
                var js = vm.JsCode ?? string.Empty;
                var param = vm.ParamsCode ?? string.Empty;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("#region HTML");
                sb.AppendLine(html);
                sb.AppendLine("#endregion");
                sb.AppendLine();
                sb.AppendLine("#region CSS");
                sb.AppendLine(css);
                sb.AppendLine("#endregion");
                sb.AppendLine();
                sb.AppendLine("#region JS");
                sb.AppendLine(js);
                sb.AppendLine("#endregion");
                sb.AppendLine();
                sb.AppendLine("#region PARAM");
                sb.AppendLine(param);
                sb.AppendLine("#endregion");

                System.Windows.Clipboard.SetText(sb.ToString());
                SetAssetStatus("✅ Đã copy toàn bộ HTML/CSS/JS/PARAM vào clipboard để gửi cho AI.");
            }
            catch (Exception ex)
            {
                SetAssetStatus($"❌ Copy thất bại: {ex.Message}", isError: true);
            }
        }

        private void ImportFourPartsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _lastImportJsOverwriteCount = 0;
                _lastImportJsAppendCount = 0;
                if (DataContext is not ShowInputMsgNodeDialogViewModel vm) return;

                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import HTML/CSS/JS/PARAM hoặc Gói Zip",
                    Filter = "UI Package / Code files|*.webpkg.zip;*.zip;*.html;*.htm;*.css;*.js;*.txt;*.*|Zip Package (*.zip;*.webpkg.zip)|*.webpkg.zip;*.zip|All files|*.*",
                    Multiselect = true
                };
                if (dlg.ShowDialog(this) != true) return;

                var files = dlg.FileNames
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (files.Count == 0) return;

                // Trường hợp chọn 1 file nén (.zip hoặc .webpkg.zip)
                if (files.Count == 1 && (files[0].EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || files[0].EndsWith(".webpkg.zip", StringComparison.OrdinalIgnoreCase)))
                {
                    var bundle = UiNodeBundleService.ImportFromZip(files[0]);
                    if (!string.IsNullOrWhiteSpace(bundle.HtmlCode)) vm.HtmlCode = bundle.HtmlCode;
                    if (!string.IsNullOrWhiteSpace(bundle.CssCode)) vm.CssCode = bundle.CssCode;
                    if (!string.IsNullOrWhiteSpace(bundle.ParamsCode)) vm.ParamsCode = bundle.ParamsCode;
                    if (!string.IsNullOrWhiteSpace(bundle.JsCode))
                    {
                        SetJsTabsFromSingleCode(bundle.JsCode);
                        SortJsTabsByPriority();
                        SyncJsTabsToViewModel();
                    }

                    MessageBox.Show(this, "Đã import thành công trọn bộ UI từ gói nén .zip!", "Import thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var htmlFiles = files.Where(f =>
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".html" || ext == ".htm";
                }).ToList();
                var cssFiles = files.Where(f => string.Equals(Path.GetExtension(f), ".css", StringComparison.OrdinalIgnoreCase)).ToList();
                var jsFiles = files.Where(f => string.Equals(Path.GetExtension(f), ".js", StringComparison.OrdinalIgnoreCase)).ToList();

                if (htmlFiles.Count > 1 || cssFiles.Count > 1)
                {
                    MessageBox.Show(
                        this,
                        "Mỗi lần import chỉ được tối đa 1 file cho HTML và CSS.\n" +
                        "Riêng JS có thể chọn nhiều file để tách thành nhiều tab con.",
                        "Import không hợp lệ",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (htmlFiles.Count == 1)
                    vm.HtmlCode = File.ReadAllText(htmlFiles[0]);
                if (cssFiles.Count == 1)
                    vm.CssCode = File.ReadAllText(cssFiles[0]);
                if (jsFiles.Count > 0)
                {
                    var jsParts = ReadJsImportParts(jsFiles);

                    var existingByTitle = BuildJsEditorMapByTitle();
                    var duplicated = jsParts
                        .Where(p => existingByTitle.ContainsKey((p.Title ?? string.Empty).Trim()))
                        .ToList();

                    var allowOverwriteDuplicated = false;
                    if (duplicated.Count > 0)
                    {
                        var duplicateList = string.Join(", ", duplicated.Select(x => x.Title));
                        var ask = MessageBox.Show(
                            this,
                            $"Đã có tab JS trùng tên: {duplicateList}\nBạn có muốn ghi đè các tab trùng tên này không?",
                            "Phát hiện trùng tên tab JS",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                        allowOverwriteDuplicated = ask == MessageBoxResult.Yes;
                    }

                    var appendedCount = 0;
                    var overwrittenCount = 0;

                    for (int i = 0; i < jsParts.Count; i++)
                    {
                        var part = jsParts[i];
                        var normalizedTitle = (part.Title ?? string.Empty).Trim();

                        if (allowOverwriteDuplicated && existingByTitle.TryGetValue(normalizedTitle, out var existingEditor))
                        {
                            existingEditor.Text = part.Code ?? string.Empty;
                            if (_jsTabMeta.TryGetValue(existingEditor, out var meta))
                                meta.Priority = part.Priority == int.MaxValue ? meta.Priority : part.Priority;
                            overwrittenCount++;
                        }
                        else
                        {
                            AddDynamicJsTab(
                                part.Code,
                                selectTab: false,
                                priority: part.Priority == int.MaxValue ? 1000 + i : part.Priority,
                                title: part.Title);
                            appendedCount++;
                        }
                    }
                    SortJsTabsByPriority();
                    SyncJsTabsToViewModel();

                    _lastImportJsOverwriteCount = overwrittenCount;
                    _lastImportJsAppendCount = appendedCount;
                }

                var paramFiles = files.Where(f =>
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext != ".html" && ext != ".htm" && ext != ".css" && ext != ".js";
                }).ToList();

                if (paramFiles.Count > 0)
                {
                    var parts = new System.Collections.Generic.List<string>();
                    foreach (var pf in paramFiles)
                    {
                        var name = Path.GetFileName(pf);
                        var content = File.ReadAllText(pf);
                        parts.Add($"# file: {name}\n{content}");
                    }
                    vm.ParamsCode = string.Join("\n\n", parts);
                }

                MessageBox.Show(
                    this,
                    jsFiles.Count > 0
                        ? $"Đã import file vào các tab tương ứng (HTML/CSS/PARAM/JS). Ghi đè {_lastImportJsOverwriteCount} tab, thêm {_lastImportJsAppendCount} tab JS."
                        : "Đã import file vào các tab tương ứng (HTML/CSS/PARAM).",
                    "Import thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Activate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Import thất bại: {ex.Message}", "Lỗi import", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Dictionary<string, SyntaxHighlightCodeEditor> BuildJsEditorMapByTitle()
        {
            var map = new Dictionary<string, SyntaxHighlightCodeEditor>(StringComparer.OrdinalIgnoreCase);
            foreach (var editor in _jsEditors)
            {
                if (editor == null) continue;
                if (!_jsTabMeta.TryGetValue(editor, out var meta)) continue;
                var title = (meta.Title ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(title)) continue;
                if (!map.ContainsKey(title))
                    map[title] = editor;
            }
            return map;
        }

        public void UpdateAllBindings()
        {
            try
            {
                HtmlEditor?.ForceUpdateBinding();
                foreach (var editor in _jsEditors) editor?.ForceUpdateBinding();
                SyncJsTabsToViewModel();
                CssEditor?.ForceUpdateBinding();
                ParamsEditor?.ForceUpdateBinding();
            }
            catch { }

            if (Keyboard.FocusedElement is UIElement element)
            {
                element.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }

        private void ReloadCodeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CodeTabsControl?.SelectedItem is TabItem selectedTab)
                {
                    SyntaxHighlightCodeEditor? editor = null;
                    if (selectedTab.Header?.ToString() == "HTML") editor = HtmlEditor;
                    else if (selectedTab.Header?.ToString() == "JS") editor = GetActiveJsEditor();
                    else if (selectedTab.Header?.ToString() == "CSS") editor = CssEditor;
                    else if (selectedTab.Header?.ToString()?.Contains("Params") == true) editor = ParamsEditor;

                    editor?.RefreshHighlight();
                }
            }
            catch { }
        }

        private void CodeEditor_Loaded(object sender, RoutedEventArgs e)
        {
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();
            InitializeJsTabsFromViewModel();
            UpdateTitleColorPreview();
            if (AiGuidePromptTextBox != null && string.IsNullOrWhiteSpace(AiGuidePromptTextBox.Text))
                AiGuidePromptTextBox.Text = AiGuidePromptDetailed;

            _jsPresets = new(HtmlOfflineAssetItemViewModel.WellKnownPresets
                .Where(p => p.Type?.ToLower() == "js")
                .Select(p => new PresetDisplayItem(p)));
            _cssPresets = new(HtmlOfflineAssetItemViewModel.WellKnownPresets
                .Where(p => p.Type?.ToLower() == "css")
                .Select(p => new PresetDisplayItem(p)));

            if (JsPresetsItemsControl != null) JsPresetsItemsControl.ItemsSource = _jsPresets;
            if (CssPresetsItemsControl != null) CssPresetsItemsControl.ItemsSource = _cssPresets;

            LoadCustomPresetsFromFile();

            if (_viewModel?.OfflineAssetsList is System.Collections.Specialized.INotifyCollectionChanged col)
                col.CollectionChanged += (_, _) => RefreshPresetStatus();

            RefreshPresetStatus();

            if (AssetsFolderText != null)
                AssetsFolderText.Text = HtmlOfflineAssetService.GetAssetsFolder();
        }

        private void AddJsTabButton_Click(object sender, RoutedEventArgs e)
        {
            AddDynamicJsTab(string.Empty, selectTab: true);
            SyncJsTabsToViewModel();
        }

        private void MoveJsTabUpButton_Click(object sender, RoutedEventArgs e)
        {
            MoveSelectedJsTabBy(-1);
        }

        private void MoveJsTabDownButton_Click(object sender, RoutedEventArgs e)
        {
            MoveSelectedJsTabBy(+1);
        }

        private void DeleteJsTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (JsSubTabsControl == null) return;

            if (_jsEditors.Count <= 1)
            {
                MessageBox.Show(
                    this,
                    "Phải giữ lại ít nhất 1 tab JS.",
                    "Không thể xóa",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (JsSubTabsControl.SelectedItem is not TabItem selectedTab || selectedTab.Content is not SyntaxHighlightCodeEditor selectedEditor)
                return;

            var removedIndex = _jsEditors.IndexOf(selectedEditor);
            _jsTabMeta.Remove(selectedEditor);
            _jsEditors.Remove(selectedEditor);
            JsSubTabsControl.Items.Remove(selectedTab);

            for (int i = 0; i < _jsEditors.Count; i++)
            {
                if (_jsTabMeta.TryGetValue(_jsEditors[i], out var meta))
                    meta.Priority = i + 1;
            }

            SortJsTabsByPriority();
            JsSubTabsControl.SelectedIndex = Math.Max(0, Math.Min(JsSubTabsControl.Items.Count - 1, removedIndex));
            SyncJsTabsToViewModel();
        }

        private void MoveSelectedJsTabBy(int delta)
        {
            if (JsSubTabsControl?.SelectedItem is not TabItem selectedTab || selectedTab.Content is not SyntaxHighlightCodeEditor selectedEditor)
                return;

            var currentIndex = _jsEditors.IndexOf(selectedEditor);
            if (currentIndex < 0) return;

            var targetIndex = currentIndex + delta;
            if (targetIndex < 0 || targetIndex >= _jsEditors.Count) return;

            var orderedEditors = _jsEditors.ToList();
            (orderedEditors[currentIndex], orderedEditors[targetIndex]) = (orderedEditors[targetIndex], orderedEditors[currentIndex]);

            for (int i = 0; i < orderedEditors.Count; i++)
            {
                if (_jsTabMeta.TryGetValue(orderedEditors[i], out var meta))
                    meta.Priority = i + 1;
            }

            SortJsTabsByPriority();
            var newSelectedTab = JsSubTabsControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(t => ReferenceEquals(t.Content as SyntaxHighlightCodeEditor, selectedEditor));
            if (newSelectedTab != null)
                JsSubTabsControl.SelectedItem = newSelectedTab;

            SyncJsTabsToViewModel();
        }

        private void InitializeJsTabsFromViewModel()
        {
            if (JsSubTabsControl == null || JsEditor == null || _viewModel == null) return;

            while (JsSubTabsControl.Items.Count > 1)
                JsSubTabsControl.Items.RemoveAt(JsSubTabsControl.Items.Count - 1);

            _jsEditors.Clear();
            _jsEditors.Add(JsEditor);
            _jsTabMeta.Clear();

            var segments = SplitJsCodeToSegments(_viewModel.JsCode);
            JsEditor.Text = segments.Count > 0 ? segments[0].Code : string.Empty;
            _jsTabMeta[JsEditor] = new JsTabMeta
            {
                Priority = segments.Count > 0 ? segments[0].Priority : 1000,
                Title = segments.Count > 0 ? segments[0].Title : "JS 1"
            };

            for (int i = 1; i < segments.Count; i++)
                AddDynamicJsTab(segments[i].Code, selectTab: false, priority: segments[i].Priority, title: segments[i].Title);

            SortJsTabsByPriority();
            JsSubTabsControl.SelectedIndex = 0;
        }

        private void SetJsTabsFromSingleCode(string jsCode, int? priority = null, string? title = null)
        {
            if (JsSubTabsControl == null || JsEditor == null) return;

            while (JsSubTabsControl.Items.Count > 1)
                JsSubTabsControl.Items.RemoveAt(JsSubTabsControl.Items.Count - 1);

            _jsEditors.Clear();
            _jsEditors.Add(JsEditor);
            _jsTabMeta.Clear();
            JsEditor.Text = jsCode ?? string.Empty;
            _jsTabMeta[JsEditor] = new JsTabMeta
            {
                Priority = priority ?? 1000,
                Title = string.IsNullOrWhiteSpace(title) ? "JS 1" : title.Trim()
            };

            SortJsTabsByPriority();
            JsSubTabsControl.SelectedIndex = 0;
            SyncJsTabsToViewModel();
        }

        private void AddDynamicJsTab(string initialCode, bool selectTab, int? priority = null, string? title = null)
        {
            if (JsSubTabsControl == null) return;

            var editor = new SyntaxHighlightCodeEditor
            {
                Text = initialCode ?? string.Empty,
                SyntaxLanguage = "JavaScript",
                IsAutoHighlightEnabled = false,
                MinHeight = 140,
                MaxHeight = 320
            };
            editor.SetBinding(SyntaxHighlightCodeEditor.CodeFontSizeProperty, new Binding("CodeFontSize") { Mode = BindingMode.TwoWay });
            editor.Loaded += CodeEditor_Loaded;

            var tab = new TabItem
            {
                Header = "JS",
                Content = editor,
                Style = (Style)FindResource("HttpTabItemStyle"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };

            JsSubTabsControl.Items.Add(tab);
            _jsEditors.Add(editor);
            _jsTabMeta[editor] = new JsTabMeta
            {
                Priority = priority ?? GetNextJsPriority(),
                Title = string.IsNullOrWhiteSpace(title) ? "JS" : title.Trim()
            };
            SortJsTabsByPriority();

            if (selectTab)
                JsSubTabsControl.SelectedItem = tab;
        }

        private int GetNextJsPriority()
        {
            return _jsTabMeta.Count == 0 ? 1000 : _jsTabMeta.Values.Max(x => x.Priority) + 1;
        }

        private void SortJsTabsByPriority()
        {
            if (JsSubTabsControl == null) return;

            var selectedEditor = GetActiveJsEditor();
            var orderedTabs = JsSubTabsControl.Items
                .OfType<TabItem>()
                .Select(t => new { Tab = t, Editor = t.Content as SyntaxHighlightCodeEditor })
                .Where(x => x.Editor != null)
                .OrderBy(x => _jsTabMeta.TryGetValue(x.Editor!, out var meta) ? meta.Priority : int.MaxValue)
                .ThenBy(x => _jsEditors.IndexOf(x.Editor!))
                .ToList();

            JsSubTabsControl.Items.Clear();
            _jsEditors.Clear();
            foreach (var x in orderedTabs)
            {
                JsSubTabsControl.Items.Add(x.Tab);
                _jsEditors.Add(x.Editor!);
            }

            RefreshJsSubTabHeaders();
            if (selectedEditor != null)
            {
                var selectedTab = orderedTabs.FirstOrDefault(x => ReferenceEquals(x.Editor, selectedEditor))?.Tab;
                if (selectedTab != null) JsSubTabsControl.SelectedItem = selectedTab;
            }
        }

        private void RefreshJsSubTabHeaders()
        {
            if (JsSubTabsControl == null) return;
            for (int i = 0; i < JsSubTabsControl.Items.Count; i++)
            {
                if (JsSubTabsControl.Items[i] is not TabItem tab || tab.Content is not SyntaxHighlightCodeEditor ed) continue;
                var meta = _jsTabMeta.TryGetValue(ed, out var m) ? m : null;
                var title = meta?.Title;
                var pr = meta?.Priority ?? i + 1;
                if (string.IsNullOrWhiteSpace(title) || title!.StartsWith("JS ", StringComparison.OrdinalIgnoreCase))
                    tab.Header = $"#{pr} JS-{i + 1}";
                else
                    tab.Header = $"#{pr} {title}";
            }
        }

        private SyntaxHighlightCodeEditor? GetActiveJsEditor()
        {
            if (JsSubTabsControl?.SelectedItem is TabItem tab && tab.Content is SyntaxHighlightCodeEditor selectedEditor)
                return selectedEditor;
            return JsEditor;
        }

        private void SyncJsTabsToViewModel()
        {
            if (_viewModel == null) return;
            _isSyncingJsTabsToViewModel = true;
            try
            {
                _viewModel.JsCode = BuildCombinedJsCodeFromTabs();
            }
            finally
            {
                _isSyncingJsTabsToViewModel = false;
            }
        }

        private string BuildCombinedJsCodeFromTabs()
        {
            var editors = _jsEditors.Where(e => e != null).ToList();
            if (editors.Count <= 1)
                return editors.FirstOrDefault()?.Text ?? string.Empty;

            var parts = new List<string>();
            for (int i = 0; i < editors.Count; i++)
            {
                var code = editors[i].Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                var meta = _jsTabMeta.TryGetValue(editors[i], out var m) ? m : new JsTabMeta { Priority = i + 1, Title = $"JS-{i + 1}" };
                var safeTitle = (meta.Title ?? $"JS-{i + 1}").Replace("]", ")");
                parts.Add($"{MultiJsTabMarkerPrefix}{i + 1}|P:{meta.Priority}|T:{safeTitle}]");
                parts.Add(code);
            }

            return parts.Count == 0 ? string.Empty : string.Join("\n\n", parts);
        }

        private sealed class JsSegment
        {
            public string Code { get; set; } = string.Empty;
            public int Priority { get; set; } = 1000;
            public string Title { get; set; } = "JS";
        }

        private static List<JsSegment> SplitJsCodeToSegments(string? jsCode)
        {
            var text = jsCode ?? string.Empty;
            if (!text.Contains(MultiJsTabMarkerPrefix))
                return new List<JsSegment> { new JsSegment { Code = text, Priority = 1000, Title = "JS 1" } };

            var blocks = new List<JsSegment>();
            var regex = new Regex(@"^\s*//\s*\[FLOW_JS_TAB:(\d+)(?:\|P:(\d+))?(?:\|T:(.*?))?\]\s*$", RegexOptions.Multiline);
            var matches = regex.Matches(text).Cast<Match>().ToList();
            if (matches.Count == 0)
                return new List<JsSegment> { new JsSegment { Code = text, Priority = 1000, Title = "JS 1" } };

            for (int i = 0; i < matches.Count; i++)
            {
                var start = matches[i].Index + matches[i].Length;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                var content = text[start..end].Trim();
                var idx = int.TryParse(matches[i].Groups[1].Value, out var parsedIdx) ? parsedIdx : i + 1;
                var pr = int.TryParse(matches[i].Groups[2].Value, out var parsedPr) ? parsedPr : idx;
                var title = matches[i].Groups[3].Success ? matches[i].Groups[3].Value.Trim() : $"JS-{idx}";
                blocks.Add(new JsSegment { Code = content, Priority = pr, Title = title });
            }

            return blocks.Count == 0
                ? new List<JsSegment> { new JsSegment { Code = string.Empty, Priority = 1000, Title = "JS 1" } }
                : blocks.OrderBy(x => x.Priority).ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static int ParsePriorityFromJsFileName(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            var m = Regex.Match(name, @"^\s*#\s*(\d+)\s*_");
            return m.Success && int.TryParse(m.Groups[1].Value, out var p) ? p : int.MaxValue;
        }

        private static string ParseTabTitleFromJsFileName(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path) ?? "JS";
            var trimmed = name.Trim();
            var m = Regex.Match(trimmed, @"^\s*#\s*\d+\s*_(Base)?(.+)$"); // Match simple or Base tags
            if (m.Success)
            {
                var cleaned = (m.Groups[trimmed.Contains("_Base") ? 2 : 1].Value ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(cleaned))
                    return cleaned;
            }
            return trimmed;
        }

        private List<(string Path, int Priority, string Title, string Code)> ReadJsImportParts(IEnumerable<string> jsFiles)
        {
            return jsFiles
                .Select(f => (
                    Path: f,
                    Priority: ParsePriorityFromJsFileName(f),
                    Title: ParseTabTitleFromJsFileName(f),
                    Code: File.ReadAllText(f)))
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void ImportJsOverwriteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import JS (Ghi đè toàn bộ tab JS)",
                    Filter = "JavaScript files (*.js)|*.js|All files (*.*)|*.*",
                    Multiselect = true
                };
                if (dlg.ShowDialog(this) != true) return;

                var jsFiles = dlg.FileNames
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(f => string.Equals(Path.GetExtension(f), ".js", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (jsFiles.Count == 0) return;

                var jsParts = ReadJsImportParts(jsFiles);
                SetJsTabsFromSingleCode(
                    jsParts[0].Code,
                    jsParts[0].Priority == int.MaxValue ? 1000 : jsParts[0].Priority,
                    jsParts[0].Title);

                for (int i = 1; i < jsParts.Count; i++)
                {
                    AddDynamicJsTab(
                        jsParts[i].Code,
                        selectTab: false,
                        priority: jsParts[i].Priority == int.MaxValue ? 1000 + i : jsParts[i].Priority,
                        title: jsParts[i].Title);
                }

                SortJsTabsByPriority();
                SyncJsTabsToViewModel();

                MessageBox.Show(
                    this,
                    $"Đã ghi đè toàn bộ tab JS bằng {jsParts.Count} file JS đã chọn.",
                    "Import JS ghi đè thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Activate();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Import JS ghi đè thất bại: {ex.Message}", "Lỗi import JS", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshPresetStatus()
        {
            var addedList = _viewModel?.OfflineAssetsList
                            ?? Enumerable.Empty<HtmlOfflineAssetItemViewModel>();
            foreach (var item in _jsPresets.Concat(_cssPresets))
                item.Refresh(addedList);
        }

        private void LoadCustomPresetsFromFile()
        {
            var customs = CustomPresetService.Load();
            foreach (var p in customs)
            {
                var item = new PresetDisplayItem(p, isCustom: true);
                if (p.Type?.ToLower() == "css") _cssPresets.Add(item);
                else _jsPresets.Add(item);
            }
            RefreshPresetStatus();
        }

        private void ToggleManualAddSection_Click(object sender, RoutedEventArgs e)
        {
            if (ManualAddSectionGrid == null || ToggleManualAddButton == null) return;
            var show = ManualAddSectionGrid.Visibility != Visibility.Visible;
            ManualAddSectionGrid.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            ToggleManualAddButton.Content = show ? "Thu gọn" : "Mở form";
        }

        private void ManagePresets_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CustomPresetManagerDialog { Owner = this };
            dlg.ShowDialog();

            var customJs  = _jsPresets.Where(x => x.IsCustom).ToList();
            var customCss = _cssPresets.Where(x => x.IsCustom).ToList();
            foreach (var x in customJs)  _jsPresets.Remove(x);
            foreach (var x in customCss) _cssPresets.Remove(x);
            LoadCustomPresetsFromFile();
        }

        private void AddToPreset_Click(object sender, RoutedEventArgs e)
        {
            var title    = NewAssetTitleBox?.Text?.Trim();
            var typeStr  = (NewAssetTypeCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "js";
            var url      = NewAssetUrlBox?.Text?.Trim() ?? string.Empty;
            var fileName = NewAssetFileNameBox?.Text?.Trim() ?? string.Empty;
            var desc     = NewAssetDescBox?.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(title))
            {
                SetAssetStatus("⚠ Vui lòng nhập Tên hiển thị trước khi thêm vào preset.", isError: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(url))
            {
                try { fileName = System.IO.Path.GetFileName(new Uri(url).LocalPath); } catch { }
            }
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"{title.Replace(" ", "_").ToLower()}.{typeStr}";

            var preset = new AssetPreset(
                Title:       $"[NEW] {title}",
                Description: string.IsNullOrWhiteSpace(desc) ? $"Custom preset: {title}" : desc,
                Url:         url,
                FileName:    fileName,
                Type:        typeStr);

            var displayItem = new PresetDisplayItem(preset, isCustom: true);

            if (typeStr == "css")
                _cssPresets.Add(displayItem);
            else
                _jsPresets.Add(displayItem);

            CustomPresetService.AddPreset(preset);

            RefreshPresetStatus();
            SetAssetStatus($"✅ Đã thêm preset '{preset.Title}' vào hàng {typeStr.ToUpper()}.");
        }

        private void CopySnippet_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string snippet && !string.IsNullOrEmpty(snippet))
            {
                try { System.Windows.Clipboard.SetText(snippet); } catch { }
            }
        }

        private void AddPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.Tag is not PresetDisplayItem displayItem) return;
            if (DataContext is not ShowInputMsgNodeDialogViewModel vm) return;

            if (displayItem.IsAdded)
            {
                var toRemove = vm.OfflineAssetsList.FirstOrDefault(a =>
                    string.Equals(a.LocalFileName, displayItem.FileName, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(displayItem.Url)
                        && string.Equals(a.SourceUrl, displayItem.Url, StringComparison.OrdinalIgnoreCase)));
                if (toRemove != null)
                    vm.OfflineAssetsList.Remove(toRemove);
            }
            else
            {
                vm.AddOfflineAssetFromPresetCommand.Execute(displayItem.Preset);
            }
        }

        private async void AddAssetFromUrl_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ShowInputMsgNodeDialogViewModel vm) return;

            var url = NewAssetUrlBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(url))
            {
                SetAssetStatus("⚠ Vui lòng nhập URL.", isError: true);
                return;
            }

            var title = NewAssetTitleBox?.Text?.Trim();
            var desc = NewAssetDescBox?.Text?.Trim() ?? string.Empty;
            var fileName = NewAssetFileNameBox?.Text?.Trim();
            var assetType = (NewAssetTypeCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLower() ?? "js";

            if (string.IsNullOrWhiteSpace(fileName))
                fileName = HtmlOfflineAssetService.GuessFileNameFromUrl(url);
            if (string.IsNullOrWhiteSpace(title))
                title = Path.GetFileNameWithoutExtension(fileName);

            var item = new HtmlOfflineAssetItemViewModel
            {
                Title = title ?? fileName,
                Description = desc,
                SourceUrl = url,
                LocalFileName = fileName,
                AssetType = assetType,
                IsEnabled = true,
                IsDownloading = true,
                StatusMessage = "⏳ Đang tải..."
            };
            vm.OfflineAssetsList.Add(item);
            SetAssetStatus($"⏳ Đang tải {fileName}...");

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                var progress = new Progress<string>(msg => item.StatusMessage = msg);
                var savedFileName = await HtmlOfflineAssetService.DownloadAssetAsync(
                    url, fileName, progress, cts.Token);
                item.LocalFileName = savedFileName;
                item.IsDownloading = false;
                item.StatusMessage = "✓ Tải xong";
                item.NotifyLocalAvailabilityChanged();
                SetAssetStatus($"✅ Đã tải: {savedFileName}");
                RefreshPresetStatus();

                if (NewAssetTitleBox != null) NewAssetTitleBox.Text = string.Empty;
                if (NewAssetDescBox != null) NewAssetDescBox.Text = string.Empty;
                if (NewAssetUrlBox != null) NewAssetUrlBox.Text = string.Empty;
                if (NewAssetFileNameBox != null) NewAssetFileNameBox.Text = string.Empty;
            }
            catch (Exception ex)
            {
                item.IsDownloading = false;
                item.StatusMessage = $"✗ Lỗi: {ex.Message}";
                SetAssetStatus($"❌ Lỗi tải: {ex.Message}", isError: true);
            }
        }

        private void AddAssetFromFile_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ShowInputMsgNodeDialogViewModel vm) return;

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn file JS hoặc CSS",
                Filter = "JS/CSS files (*.js;*.css)|*.js;*.css|All files (*.*)|*.*",
                Multiselect = false
            };

            if (dlg.ShowDialog(this) != true) return;

            try
            {
                var savedFileName = HtmlOfflineAssetService.CopyAssetFromFile(dlg.FileName);
                var ext = Path.GetExtension(savedFileName).TrimStart('.').ToLower();
                var assetType = ext == "css" ? "css" : "js";
                var title = NewAssetTitleBox?.Text?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                    title = Path.GetFileNameWithoutExtension(savedFileName);

                vm.OfflineAssetsList.Add(new HtmlOfflineAssetItemViewModel
                {
                    Title = title,
                    Description = NewAssetDescBox?.Text?.Trim() ?? string.Empty,
                    SourceUrl = dlg.FileName,
                    LocalFileName = savedFileName,
                    AssetType = assetType,
                    IsEnabled = true,
                    StatusMessage = "✓ Có sẵn (từ file local)"
                });
                SetAssetStatus($"✅ Đã copy: {savedFileName}");
                RefreshPresetStatus();
            }
            catch (Exception ex)
            {
                SetAssetStatus($"❌ Lỗi: {ex.Message}", isError: true);
            }
        }

        private async void DownloadAsset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not HtmlOfflineAssetItemViewModel item) return;
            await DownloadOfflineAssetItemAsync(item);
        }

        private async Task DownloadOfflineAssetItemAsync(HtmlOfflineAssetItemViewModel item)
        {
            if (string.IsNullOrWhiteSpace(item.SourceUrl))
            {
                SetAssetStatus("⚠ Asset này không có URL nguồn để tải.", isError: true);
                return;
            }

            item.IsDownloading = true;
            item.StatusMessage = "⏳ Đang tải...";
            SetAssetStatus($"⏳ Đang tải lại: {item.LocalFileName}...");

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                var progress = new Progress<string>(msg => item.StatusMessage = msg);
                var savedFileName = await HtmlOfflineAssetService.DownloadAssetAsync(
                    item.SourceUrl, item.LocalFileName, progress, cts.Token);
                item.LocalFileName = savedFileName;
                item.StatusMessage = "✓ Tải xong";
                item.NotifyLocalAvailabilityChanged();
                SetAssetStatus($"✅ Đã tải lại: {savedFileName}");
                RefreshPresetStatus();
            }
            catch (Exception ex)
            {
                item.StatusMessage = $"✗ Lỗi: {ex.Message}";
                SetAssetStatus($"❌ Lỗi tải: {ex.Message}", isError: true);
            }
            finally
            {
                item.IsDownloading = false;
            }
        }

        private void OpenAssetsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folder = HtmlOfflineAssetService.GetAssetsFolder();
                System.Diagnostics.Process.Start("explorer.exe", folder);
            }
            catch { }
        }

        private void AssetsFolderText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var path = HtmlOfflineAssetService.GetAssetsFolder();
                System.Windows.Clipboard.SetText(path);
                SetAssetStatus("📋 Đã copy đường dẫn!");
            }
            catch { }
        }

        private void SetAssetStatus(string message, bool isError = false)
        {
            if (AssetStatusText == null) return;
            AssetStatusText.Text = message;
            AssetStatusText.Foreground = isError
                ? (TryFindResource("DangerBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71)))
                : (TryFindResource("WarningBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)));
        }

        private void ApplyApiTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (DataContext is not ShowInputMsgNodeDialogViewModel vm) return;

            var tag = btn.Tag as string;
            if (string.IsNullOrWhiteSpace(tag)) return;

            switch (tag)
            {
                case "tpl_realtime":
                    SetJsTabsFromSingleCode(TemplateRealtimeJs);
                    break;
                case "tpl_local_video":
                    SetJsTabsFromSingleCode(TemplateLocalVideoJs);
                    break;
                case "tpl_submit_form":
                    SetJsTabsFromSingleCode(TemplateSubmitFormJs);
                    break;
                case "tpl_full_new_api":
                    SetJsTabsFromSingleCode(TemplateFullNewApiJs);
                    break;
                default:
                    return;
            }

            try
            {
                if (MainTabsControl != null) MainTabsControl.SelectedIndex = 0;
                if (CodeTabsControl != null) CodeTabsControl.SelectedIndex = 1;
                JsSubTabsControl?.SetCurrentValue(TabControl.SelectedIndexProperty, 0);
                JsEditor?.Focus();
                JsEditor?.RefreshHighlight();
            }
            catch { }

            if (AiGuideStatusText != null)
                AiGuideStatusText.Text = "✅ Đã apply template vào tab JS (đã chuyển sang tab JS).";
        }

        private void CopyApiDocAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(ApiDocAllText);
                if (AiGuideStatusText != null) AiGuideStatusText.Text = "✅ Đã copy toàn bộ bảng API JS↔C#.";
            }
            catch
            {
                if (AiGuideStatusText != null) AiGuideStatusText.Text = "❌ Copy all API thất bại.";
            }
        }

        private void CopyAiGuidePrompt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var text = AiGuidePromptTextBox?.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (AiGuideStatusText != null) AiGuideStatusText.Text = "⚠ Không có nội dung để copy";
                    return;
                }

                System.Windows.Clipboard.SetText(text);
                if (AiGuideStatusText != null) AiGuideStatusText.Text = "✅ Đã copy prompt. Dán cho AI để tạo 4 phần HTML/CSS/JS/PARAM.";
            }
            catch
            {
                if (AiGuideStatusText != null) AiGuideStatusText.Text = "❌ Copy thất bại";
            }
        }

        private void ApplyExample_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (DataContext is not ShowInputMsgNodeDialogViewModel vm) return;

            switch (btn.Tag as string)
            {
                case "ex1": ApplyEx1(vm); break;
                case "ex2": ApplyEx2(vm); break;
                case "ex3": ApplyEx3(vm); break;
                case "ex4": ApplyEx4(vm); break;
                case "ex_grok_local": ApplyExGrokLocal(vm); break;
                default: return;
            }

            try
            {
                if (MainTabsControl != null) MainTabsControl.SelectedIndex = 0;
                if (CodeTabsControl != null) CodeTabsControl.SelectedIndex = 1;
                JsSubTabsControl?.SetCurrentValue(TabControl.SelectedIndexProperty, 0);
                JsEditor?.Focus();
                JsEditor?.RefreshHighlight();
            }
            catch { }

            if (AiGuideStatusText != null)
                AiGuideStatusText.Text = "✅ Đã apply ví dụ vào các tab code (đã chuyển sang tab JS).";
        }

        private void CopyApiDocRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            var text = btn.Tag as string;
            if (string.IsNullOrWhiteSpace(text)) return;
            try
            {
                System.Windows.Clipboard.SetText(text);
                if (AiGuideStatusText != null) AiGuideStatusText.Text = "✅ Đã copy API snippet.";
            }
            catch
            {
                if (AiGuideStatusText != null) AiGuideStatusText.Text = "❌ Copy snippet thất bại.";
            }
        }

        private static void ApplyEx1(ShowInputMsgNodeDialogViewModel vm)
        {
            vm.HtmlCode =
@"<div class=""card"">
  <div class=""lbl"" id=""lbl"">Đang chờ biến...</div>
  <div class=""val"" id=""val"">—</div>
  <div class=""ts""  id=""ts"">—</div>
</div>";

            vm.CssCode =
@"* { box-sizing: border-box; margin: 0; padding: 0; }
body {
  background: #0f172a; color: #e2e8f0;
  font-family: 'Segoe UI', sans-serif;
  display: flex; align-items: center;
  justify-content: center; height: 100vh;
}
.card {
  background: #1e293b; border-radius: 14px;
  border: 1px solid #334155; padding: 28px 36px;
  text-align: center; min-width: 220px;
}
.lbl { font-size: 12px; color: #94a3b8; margin-bottom: 8px; }
.val {
  font-size: 38px; font-weight: 700; color: #38bdf8;
  transition: color .25s, transform .25s;
}
.val.flash { color: #4ade80; transform: scale(1.08); }
.ts { font-size: 11px; color: #475569; margin-top: 10px; }";

            vm.JsCode =
@"window.hostLive.on(function(live) {
    var keys = Object.keys(live);
    if (!keys.length) return;
    var key = keys[0];
    document.getElementById('lbl').textContent = 'Biến: ' + key;
    var el = document.getElementById('val');
    el.textContent = live[key] || '—';
    el.classList.add('flash');
    setTimeout(function() { el.classList.remove('flash'); }, 350);
    document.getElementById('ts').textContent =
        '⏱ ' + new Date().toLocaleTimeString();
});";

            vm.ParamsCode = string.Empty;
        }

        private static void ApplyEx2(ShowInputMsgNodeDialogViewModel vm)
        {
            vm.HtmlCode =
@"<div class=""wrap"">
  <div class=""card"">
    <div class=""lbl"">price</div>
    <div class=""val"" id=""price"">—</div>
  </div>
  <div class=""op"">×</div>
  <div class=""card"">
    <div class=""lbl"">quantity</div>
    <div class=""val"" id=""quantity"">—</div>
  </div>
  <div class=""op"">=</div>
  <div class=""card total"">
    <div class=""lbl"">Tổng</div>
    <div class=""val"" id=""total"">—</div>
  </div>
</div>
<div class=""ts"" id=""ts"">—</div>";

            vm.CssCode =
@"* { box-sizing: border-box; margin: 0; padding: 0; }
body {
  background: #0f172a; color: #e2e8f0;
  font-family: 'Segoe UI', sans-serif;
  display: flex; flex-direction: column;
  align-items: center; justify-content: center; height: 100vh; gap: 12px;
}
.wrap { display: flex; align-items: center; gap: 12px; }
.card {
  background: #1e293b; border-radius: 12px;
  border: 1px solid #334155; padding: 16px 24px;
  text-align: center; min-width: 110px;
  transition: border-color .3s;
}
.card.flash { border-color: #4ade80; }
.card.total { border-color: #818cf8; min-width: 140px; }
.lbl { font-size: 11px; color: #94a3b8; margin-bottom: 6px; }
.val { font-size: 28px; font-weight: 700; color: #38bdf8; }
.total .val { color: #a78bfa; }
.op { font-size: 24px; color: #64748b; }
.ts { font-size: 11px; color: #475569; }";

            vm.JsCode =
@"function flash(id) {
    var el = document.getElementById(id);
    el.closest('.card').classList.add('flash');
    setTimeout(function() { el.closest('.card').classList.remove('flash'); }, 400);
}
window.hostLive.on('price', 'quantity', function(price, quantity) {
    document.getElementById('price').textContent = price || '—';
    document.getElementById('quantity').textContent = quantity || '—';
    var total = parseFloat(price || 0) * parseFloat(quantity || 0);
    document.getElementById('total').textContent = isNaN(total) ? '—' : total.toFixed(2);
    flash('price'); flash('quantity'); flash('total');
    document.getElementById('ts').textContent =
        '⏱ ' + new Date().toLocaleTimeString();
});";

            vm.ParamsCode = string.Empty;
        }

        private static void ApplyExGrokLocal(ShowInputMsgNodeDialogViewModel vm)
        {
            vm.HtmlCode =
@"<div class=""card-media card-media-inline"">
  <video id=""previewVideo"" class=""card-video-inline"" controls playsinline preload=""metadata""></video>
</div>
<div class=""status"" id=""statusText"">Đang chờ datas...</div>";

            vm.CssCode =
@".card-media-inline { position: relative; width: 100%; aspect-ratio: 16/9; background: #0b1220; border: 1px solid #334155; border-radius: 10px; overflow: hidden; }
.card-video-inline { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; background: #000; }
.status { margin-top: 8px; color: #94a3b8; font-size: 12px; }";

            vm.JsCode =
@"(function () {
  var currentGuildId = (window.hostLive && window.hostLive.values) ? (window.hostLive.values.outputGuildId || '') : '';
  var videoEl = document.getElementById('previewVideo');
  var statusEl = document.getElementById('statusText');
  if (!videoEl) return;

  function setStatus(t) { if (statusEl) statusEl.textContent = t || ''; }

  function normalizeDatas(raw) {
    if (!raw) return null;
    if (typeof raw === 'string') {
      try { raw = JSON.parse(raw); } catch (_) { return null; }
    }
    return (raw && typeof raw === 'object') ? raw : null;
  }

  function playFromLocalPath(localPath) {
    if (!localPath || typeof hostResolvePath !== 'function') return;
    var reqId = 'play_' + Date.now();
    function onResolved(ev) {
      var d = (ev && ev.detail) ? ev.detail : {};
      if (String(d.requestId || '') !== reqId) return;
      window.removeEventListener('hostPathResolved', onResolved);
      if (!d.ok || !d.localUrl) { setStatus('Resolve failed'); return; }
      videoEl.src = d.localUrl;
      try { videoEl.load(); } catch (_) {}
      var pp = videoEl.play && videoEl.play();
      if (pp && typeof pp.catch === 'function') pp.catch(function(){});
      setStatus('Loaded: ' + d.localUrl);
    }
    window.addEventListener('hostPathResolved', onResolved);
    hostResolvePath(localPath, reqId);
    setStatus('Resolving local path...');
  }

  if (window.hostLive && typeof window.hostLive.on === 'function') {
    window.hostLive.on('datas', function (datas) {
      var dict = normalizeDatas(datas);
      if (!dict) return;
      var lp = currentGuildId ? dict[currentGuildId] : '';
      if (typeof lp !== 'string' || !lp.trim()) return;
      playFromLocalPath(lp.trim());
    });
  }
})();";

            vm.ParamsCode = string.Empty;
        }

        private static void ApplyEx3(ShowInputMsgNodeDialogViewModel vm)
        {
            vm.HtmlCode =
@"<div class=""wrap"">
  <div class=""card"">
    <div class=""lbl"">📊 Chart giá trị</div>
    <div class=""bar-wrap""><div class=""bar"" id=""bar""></div></div>
    <div class=""val"" id=""chart-val"">—</div>
  </div>
  <div class=""card"">
    <div class=""lbl"">🔖 Badge</div>
    <div class=""badge"" id=""badge"">—</div>
  </div>
  <div class=""card hist"">
    <div class=""lbl"">📜 Lịch sử (5 gần nhất)</div>
    <ul id=""history""></ul>
  </div>
</div>
<div class=""ts"" id=""ts"">—</div>";

            vm.CssCode =
@"* { box-sizing: border-box; margin: 0; padding: 0; }
body {
  background: #0f172a; color: #e2e8f0;
  font-family: 'Segoe UI', sans-serif;
  display: flex; flex-direction: column;
  align-items: center; justify-content: center; height: 100vh; gap: 12px;
}
.wrap { display: flex; gap: 12px; align-items: flex-start; }
.card {
  background: #1e293b; border-radius: 12px;
  border: 1px solid #334155; padding: 14px 18px;
  min-width: 130px; text-align: center;
}
.lbl { font-size: 11px; color: #94a3b8; margin-bottom: 8px; }
.val { font-size: 24px; font-weight: 700; color: #38bdf8; margin-top: 6px; }
.bar-wrap { background: #0f172a; border-radius: 6px; height: 80px; display: flex; align-items: flex-end; overflow: hidden; }
.bar { width: 100%; background: #38bdf8; transition: height .4s ease; border-radius: 4px 4px 0 0; }
.badge {
  display: inline-block; padding: 8px 18px;
  background: #7c3aed; border-radius: 999px;
  font-size: 20px; font-weight: 700; color: white;
  margin-top: 8px; transition: background .3s;
}
.badge.flash { background: #4ade80; }
.hist { text-align: left; min-width: 160px; }
.hist ul { list-style: none; font-size: 12px; }
.hist ul li { padding: 2px 0; color: #94a3b8; border-bottom: 1px solid #1e293b; }
.hist ul li:first-child { color: #4ade80; font-weight: 600; }
.ts { font-size: 11px; color: #475569; }";

            vm.JsCode =
@"var _history = [];

function updateChart(val) {
    var num = parseFloat(val) || 0;
    var pct = Math.min(100, Math.max(0, Math.abs(num) % 101));
    document.getElementById('bar').style.height = pct + '%';
    document.getElementById('chart-val').textContent = val || '—';
}
function updateBadge(val) {
    var el = document.getElementById('badge');
    el.textContent = val || '—';
    el.classList.add('flash');
    setTimeout(function() { el.classList.remove('flash'); }, 400);
}
function updateHistory(val) {
    _history.unshift(val);
    if (_history.length > 5) _history.pop();
    var ul = document.getElementById('history');
    ul.innerHTML = '';
    _history.forEach(function(v, i) {
        var li = document.createElement('li');
        li.textContent = (i === 0 ? '▶ ' : '   ') + v;
        ul.appendChild(li);
    });
}

window.hostLive.on(function(live) {
    var keys = Object.keys(live);
    if (!keys.length) return;
    var val = live[keys[0]];
    updateChart(val);
    updateBadge(val);
    updateHistory(val);
    document.getElementById('ts').textContent =
        '⏱ ' + new Date().toLocaleTimeString();
});";

            vm.ParamsCode = string.Empty;
        }

        private static void ApplyEx4(ShowInputMsgNodeDialogViewModel vm)
        {
            vm.HtmlCode =
@"<div class=""dashboard"">
  <div class=""title"">📡 Live Dashboard (polling 500ms)</div>
  <div id=""rows"" class=""rows""></div>
  <div class=""ts"" id=""ts"">—</div>
</div>";

            vm.CssCode =
@"* { box-sizing: border-box; margin: 0; padding: 0; }
body {
  background: #0f172a; color: #e2e8f0;
  font-family: 'Segoe UI', sans-serif;
  display: flex; align-items: center;
  justify-content: center; height: 100vh;
}
.dashboard {
  background: #1e293b; border-radius: 14px;
  border: 1px solid #334155; padding: 24px 28px;
  min-width: 260px;
}
.title { font-size: 13px; font-weight: 600; color: #94a3b8; margin-bottom: 14px; }
.rows { display: flex; flex-direction: column; gap: 8px; }
.row { display: flex; justify-content: space-between; align-items: center; }
.key { font-size: 12px; color: #94a3b8; font-family: Consolas; }
.value { font-size: 18px; font-weight: 700; color: #38bdf8; }
.ts { font-size: 11px; color: #475569; margin-top: 12px; text-align: right; }";

            vm.JsCode =
@"setInterval(function() {
    var live = window.hostLive ? window.hostLive.values : {};
    var keys = Object.keys(live);
    var container = document.getElementById('rows');
    container.innerHTML = '';
    if (!keys.length) {
        container.innerHTML = '<div style=""color:#475569;font-size:12px"">Chưa có dữ liệu...</div>';
    } else {
        keys.forEach(function(key) {
            var row = document.createElement('div');
            row.className = 'row';
            row.innerHTML =
                '<span class=""key"">' + key + '</span>' +
                '<span class=""value"">' + (live[key] || '—') + '</span>';
            container.appendChild(row);
        });
    }
    document.getElementById('ts').textContent =
        '⏱ Poll: ' + new Date().toLocaleTimeString();
}, 500);";

            vm.ParamsCode = string.Empty;
        }

        private void ExportBundleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateAllBindings();
                var html = _viewModel.HtmlCode ?? string.Empty;
                var css = _viewModel.CssCode ?? string.Empty;
                var js = BuildCombinedJsCodeFromTabs();
                var param = _viewModel.ParamsCode ?? string.Empty;
                var title = _viewModel?.NodeTitle ?? "show_input_msg_bundle";

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export UI Bundle Package",
                    Filter = "Web Package (*.webpkg.zip)|*.webpkg.zip|Zip Package (*.zip)|*.zip|All files (*.*)|*.*",
                    FileName = $"{title}.webpkg.zip"
                };

                if (dlg.ShowDialog(this) != true) return;

                UiNodeBundleService.ExportToZip(dlg.FileName, title, html, css, js, param);

                MessageBox.Show(
                    this,
                    $"Đã export thành công gói UI Bundle:\n{dlg.FileName}",
                    "Export thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Export thất bại: {ex.Message}", "Lỗi Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
