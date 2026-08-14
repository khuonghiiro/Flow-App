using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Workflow.NodeExecutors;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal sealed class EmbedApplicationNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is EmbedApplicationNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var embedNode = (EmbedApplicationNode)node;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Kiểm tra process còn tồn tại không
                Process? targetProcess = null;
                try
                {
                    if (embedNode.ProcessId > 0)
                    {
                        targetProcess = Process.GetProcessById(embedNode.ProcessId);
                    }
                }
                catch (ArgumentException)
                {
                    // Process không tồn tại
                    env.OnNodeFailed?.Invoke(embedNode, $"Process ID {embedNode.ProcessId} không tồn tại");
                    throw new InvalidOperationException($"Process ID {embedNode.ProcessId} không còn chạy");
                }

                if (targetProcess == null || targetProcess.HasExited)
                {
                    env.OnNodeFailed?.Invoke(embedNode, "Ứng dụng đã đóng");
                    throw new InvalidOperationException("Ứng dụng đã đóng hoặc không tồn tại");
                }

                // Update window handle (có thể thay đổi)
                embedNode.WindowHandle = targetProcess.MainWindowHandle;
                embedNode.WindowTitle = targetProcess.MainWindowTitle;
                embedNode.ProcessName = targetProcess.ProcessName;

                // Publish outputs vào scoped store
                if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
                {
                    env.Service.SetScopedNodeStringOutput(
                        env.ExecutionId, embedNode.Id, "windowHandle", embedNode.WindowHandle.ToString());
                    
                    env.Service.SetScopedNodeStringOutput(
                        env.ExecutionId, embedNode.Id, "processId", embedNode.ProcessId.ToString());
                    
                    env.Service.SetScopedNodeStringOutput(
                        env.ExecutionId, embedNode.Id, "windowTitle", embedNode.WindowTitle ?? string.Empty);
                    
                    env.Service.SetScopedNodeStringOutput(
                        env.ExecutionId, embedNode.Id, "processName", embedNode.ProcessName ?? string.Empty);
                }

                // Active window nếu cần
                if (embedNode.IsActive && embedNode.WindowHandle != IntPtr.Zero)
                {
                    try
                    {
                        NativeMethods.SetForegroundWindow(embedNode.WindowHandle);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Cannot activate window: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                env.OnNodeFailed?.Invoke(embedNode, ex.Message);
                throw;
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(embedNode, sw.Elapsed);

            // Traverse sang node tiếp theo
            await env.TraverseOutputsAsync(embedNode);
        }
    }

    // Native methods cho window management
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
