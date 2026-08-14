// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using FlowMy.Models;
using FlowMy.Services.Interaction;
using FlowMy.Services.Rendering;
using FlowMy.Services.Workflow;
using FlowMy.ViewModels;
using FlowMy.Views.NodeControls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace FlowMy.Views
{
    /// <summary>
    /// Undo/Redo cho workflow editor — dùng snapshot (serialize JSON) sau mỗi thao tác cấu trúc.
    /// Ctrl+Z = lùi 1 bước, Ctrl+Y = khôi phục bước sau.
    /// </summary>
    public partial class WorkflowEditorWindow
    {
        private const int MaxUndoSteps = 50;

        /// <summary>Stack lưu các snapshot JSON trước đó (undo).</summary>
        private readonly Stack<string> _undoStack = new();

        /// <summary>Stack lưu các snapshot JSON đã undo (redo).</summary>
        private readonly Stack<string> _redoStack = new();

        /// <summary>Đánh dấu đang restore snapshot để tránh chụp snapshot đệ quy.</summary>
        private bool _isRestoringSnapshot;

        /// <summary>
        /// Chụp trạng thái hiện tại thành JSON string.
        /// Sử dụng ExportToJson có sẵn từ persistence service.
        /// </summary>
        private string? CaptureUndoSnapshot()
        {
            var vm = ViewModel;
            if (vm == null) return null;

            try
            {
                var persistence = new FileWorkflowPersistenceService(_templateFactory);
                return persistence.ExportToJson(
                    vm.CurrentWorkflowName ?? string.Empty,
                    vm.Nodes,
                    vm.Connections,
                    vm.ZoomLevel,
                    vm.PanX,
                    vm.PanY);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UndoRedo] CaptureSnapshot failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Push snapshot hiện tại vào undo stack.
        /// Gọi TRƯỚC mỗi thao tác cấu trúc (add/delete/move node, add/delete connection, paste).
        /// </summary>
        public void PushUndoSnapshot()
        {
            if (_isRestoringSnapshot) return;

            var snapshot = CaptureUndoSnapshot();
            if (snapshot == null) return;

            _undoStack.Push(snapshot);

            // Giới hạn stack size
            if (_undoStack.Count > MaxUndoSteps)
            {
                // Stack<T> không hỗ trợ RemoveAt → chuyển sang array trim
                var items = _undoStack.ToArray();
                _undoStack.Clear();
                // items[0] là top (mới nhất), giữ MaxUndoSteps phần tử đầu
                for (int i = MaxUndoSteps - 1; i >= 0; i--)
                    _undoStack.Push(items[i]);
            }

            // Khi có thao tác mới → xóa redo stack (nhánh redo bị hủy)
            _redoStack.Clear();
        }

        /// <summary>
        /// Ctrl+Z: Lùi lại 1 bước.
        /// Pop snapshot từ UndoStack, push trạng thái hiện tại vào RedoStack, restore.
        /// </summary>
        private void UndoLastAction()
        {
            if (_undoStack.Count == 0)
            {
                ToastNotificationService.ShowToast(
                    "Undo",
                    "Không có thao tác nào để lùi lại.",
                    durationSeconds: 1,
                    titleColorKey: "TextOnWarningBrush",
                    contentColorKey: "TextOnWarningBrush",
                    backgroundColorKey: "WarningBrush",
                    backgroundOpacity: 0.95);
                return;
            }

            // Lưu trạng thái hiện tại vào redo stack
            var currentSnapshot = CaptureUndoSnapshot();
            if (currentSnapshot != null)
                _redoStack.Push(currentSnapshot);

            // Pop snapshot cũ và restore
            var previousSnapshot = _undoStack.Pop();
            RestoreFromSnapshot(previousSnapshot);
        }

        /// <summary>
        /// Ctrl+Y: Khôi phục bước sau (redo).
        /// Pop snapshot từ RedoStack, push trạng thái hiện tại vào UndoStack, restore.
        /// </summary>
        private void RedoLastAction()
        {
            if (_redoStack.Count == 0)
            {
                ToastNotificationService.ShowToast(
                    "Redo",
                    "Không có thao tác nào để khôi phục.",
                    durationSeconds: 1,
                    titleColorKey: "TextOnWarningBrush",
                    contentColorKey: "TextOnWarningBrush",
                    backgroundColorKey: "WarningBrush",
                    backgroundOpacity: 0.95);
                return;
            }

            // Lưu trạng thái hiện tại vào undo stack
            var currentSnapshot = CaptureUndoSnapshot();
            if (currentSnapshot != null)
                _undoStack.Push(currentSnapshot);

            // Pop snapshot redo và restore
            var nextSnapshot = _redoStack.Pop();
            RestoreFromSnapshot(nextSnapshot);
        }

        /// <summary>
        /// Restore toàn bộ workflow từ snapshot JSON.
        /// Dùng ImportFromJson + ApplyWorkflowLoadResult có sẵn.
        /// </summary>
        private void RestoreFromSnapshot(string snapshotJson)
        {
            var vm = ViewModel;
            if (vm == null) return;

            _isRestoringSnapshot = true;
            try
            {
                var persistence = new FileWorkflowPersistenceService(_templateFactory);
                var loadResult = persistence.ImportFromJson(snapshotJson);
                if (loadResult == null)
                {
                    Debug.WriteLine("[UndoRedo] RestoreFromSnapshot: ImportFromJson returned null");
                    return;
                }

                // Dọn UI cũ
                ClearVisualsForReload();

                // Giữ tên workflow hiện tại (không đổi tên khi undo/redo)
                var currentName = vm.CurrentWorkflowName;

                // Apply trạng thái mới
                // Sử dụng reflection-free approach: set IsLoading, replace collections
                vm.IsLoading = true;
                try
                {
                    vm.ZoomLevel = loadResult.ZoomLevel;
                    vm.PanX = loadResult.PanX;
                    vm.PanY = loadResult.PanY;

                    if (!string.IsNullOrWhiteSpace(loadResult.ConnectionLineStyle) &&
                        Enum.TryParse<ConnectionLineStyle>(loadResult.ConnectionLineStyle, out var restoredStyle))
                        vm.ConnectionLineStyle = restoredStyle;

                    // Batch replace collections
                    vm.Nodes = new ObservableCollection<WorkflowNode>(loadResult.Nodes);
                    vm.Connections = new ObservableCollection<WorkflowConnection>(loadResult.Connections);

                    // Giữ tên workflow
                    vm.CurrentWorkflowName = currentName;
                }
                finally
                {
                    vm.IsLoading = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UndoRedo] RestoreFromSnapshot failed: {ex.Message}");
            }
            finally
            {
                _isRestoringSnapshot = false;
            }
        }

        /// <summary>
        /// Xóa toàn bộ undo/redo stacks.
        /// Gọi khi load workflow mới hoặc tạo workflow mới.
        /// </summary>
        public void ClearUndoRedoStacks()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        /// <summary>
        /// Xử lý phím tắt Ctrl+Z (undo) và Ctrl+Y (redo).
        /// Trả về true nếu đã handle.
        /// </summary>
        private bool TryHandleUndoRedoShortcuts(System.Windows.Input.KeyEventArgs e)
        {
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) !=
                System.Windows.Input.ModifierKeys.Control)
                return false;

            // Không xử lý khi đang nhập text
            if (IsEditingTextInput())
                return false;

            if (e.Key == System.Windows.Input.Key.Z)
            {
                UndoLastAction();
                e.Handled = true;
                return true;
            }

            if (e.Key == System.Windows.Input.Key.Y)
            {
                RedoLastAction();
                e.Handled = true;
                return true;
            }

            return false;
        }
    }
}
