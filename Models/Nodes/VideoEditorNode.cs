// NOTICE TO AI DEVELOPERS / AGENTS:
// 1. DO NOT integrate too much logic or complexity directly into this single file. Keep it modular and split logic into smaller helper classes, extension methods, or separate service files.
// 2. If this file exceeds ~1200-1500 lines of code, you MUST refactor and split it into smaller, manageable partial classes or smaller files to ensure maintainability.
// 3. Always include this exact notice block at the top of any newly created split files so that subsequent AI agents continue to respect this architectural rule.

using FlowMy.Models;
using System;
using System.Collections.ObjectModel;

namespace FlowMy.Models.Nodes
{
    // ✅ KHÔNG thêm INotifyPropertyChanged — WorkflowNode đã implement
    // ✅ KHÔNG khai báo lại: PropertyChanged, OnPropertyChanged, TitleDisplayMode, TitleColorMode, TitleColorKey
    /// <summary>
    /// Chỉnh sửa video node — được tạo tự động bởi NodeGeneratorService.
    /// TODO: Thêm properties đặc thù, logic, và mô tả chi tiết.
    /// </summary>
    public sealed class VideoEditorNode : WorkflowNode
    {
        // TODO: Khai báo properties đặc thù của node ở đây.
        // Ví dụ:
        // private string _someProperty = string.Empty;
        // public string SomeProperty
        // {
        //     get => _someProperty;
        //     set { if (_someProperty != value) { _someProperty = value; OnPropertyChanged(); } }
        // }

        public VideoEditorNode()
        {
            Type = NodeType.VideoEditor;
            Title = "Chỉnh sửa video";

            // ⚠️ KHÔNG thêm Ports ở đây — TemplateFactory sẽ tạo port để tránh duplicate.
            // Ports sẽ được tạo trong TemplateFactory.CreateYourNode()
        }
    }
}
