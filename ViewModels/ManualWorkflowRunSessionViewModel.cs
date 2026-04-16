using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FlowMy.ViewModels;

/// <summary>
/// Một phiên chạy thủ công (mỗi lần bấm Bắt đầu hoặc chạy từ node).
/// </summary>
public sealed partial class ManualWorkflowRunSessionViewModel : ObservableObject
{
    public string SessionId { get; }
    public string LineText { get; }

    private readonly WorkflowEditorViewModel _host;

    public ManualWorkflowRunSessionViewModel(string sessionId, WorkflowEditorViewModel host)
    {
        SessionId = sessionId;
        _host = host;
        var shortId = sessionId.Length >= 6 ? sessionId[..6] : sessionId;
        LineText = $"Phiên {shortId}… · {DateTime.Now:HH:mm:ss}";
    }

    [RelayCommand]
    private void StopThisSession() => _host.CancelManualRunSession(SessionId);
}
