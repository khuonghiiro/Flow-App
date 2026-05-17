using FlowMy.Models;
using FlowMy.Services.Git;
using System.Diagnostics;
using System.IO;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    /// <summary>
    /// Executor cho GitSourceNode:
    /// 1. Pull latest (nếu repo đã clone)
    /// 2. Mở VSCodium với source (nếu AutoOpenOnExecute = true)
    /// 3. Output: localPath, branch, lastCommit
    /// </summary>
    internal sealed class GitSourceNodeExecutor : INodeExecutor
    {
        public bool CanExecute(WorkflowNode node) => node is GitSourceNode;

        public async Task ExecuteAsync(WorkflowNode node, NodeExecutionEnvironment env)
        {
            var gitNode = (GitSourceNode)node;
            var sw = Stopwatch.StartNew();
            var gitService = new GitService();

            try
            {
                var localPath = gitNode.LocalPath;

                // Nếu repo chưa clone và có URL → clone
                if (!string.IsNullOrWhiteSpace(gitNode.RepoUrl) &&
                    (string.IsNullOrWhiteSpace(localPath) || !Directory.Exists(localPath) || !gitService.IsGitRepository(localPath)))
                {
                    if (string.IsNullOrWhiteSpace(localPath))
                    {
                        env.OnNodeFailed?.Invoke(gitNode, "LocalPath chưa được cấu hình.");
                        throw new InvalidOperationException("LocalPath chưa được cấu hình.");
                    }

                    var cloneResult = await Task.Run(() =>
                        gitService.CloneRepository(gitNode.RepoUrl, localPath, gitNode.Branch));

                    if (!cloneResult.Success)
                    {
                        env.OnNodeFailed?.Invoke(gitNode, $"Clone thất bại: {cloneResult.ErrorMessage}");
                        throw new InvalidOperationException($"Clone thất bại: {cloneResult.ErrorMessage}");
                    }

                    gitNode.LastCommitHash = cloneResult.LastCommitHash;
                    gitNode.LastPullTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                }
                else if (gitService.IsGitRepository(localPath))
                {
                    // Pull latest
                    var pullResult = await Task.Run(() => gitService.PullRepository(localPath));
                    if (pullResult.Success)
                    {
                        gitNode.LastCommitHash = pullResult.LastCommitHash;
                        gitNode.LastPullTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    // Pull fail không block execution — vẫn mở VSCodium
                }

                // Mở VSCodium nếu cấu hình
                if (gitNode.AutoOpenOnExecute && Directory.Exists(localPath))
                {
                    var exePath = string.IsNullOrWhiteSpace(gitNode.VscodiumPath) ? "vscodium" : gitNode.VscodiumPath;
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = exePath,
                            Arguments = $"\"{localPath}\"",
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        // Không block nếu VSCodium không tìm thấy
                    }
                }

                // Publish outputs
                if (!env.RefreshOnly && !string.IsNullOrWhiteSpace(env.ExecutionId))
                {
                    env.Service.SetScopedNodeStringOutput(env.ExecutionId, gitNode.Id, "localPath", localPath ?? string.Empty);
                    env.Service.SetScopedNodeStringOutput(env.ExecutionId, gitNode.Id, "branch", gitNode.Branch);
                    env.Service.SetScopedNodeStringOutput(env.ExecutionId, gitNode.Id, "lastCommit", gitNode.LastCommitHash);
                }
            }
            catch (Exception ex)
            {
                env.OnNodeFailed?.Invoke(gitNode, ex.Message);
                throw;
            }

            sw.Stop();
            env.OnNodeCompleted?.Invoke(gitNode, sw.Elapsed);

            await env.TraverseOutputsAsync(gitNode);
        }
    }
}
