using FlowMy.Models;
using System;
using System.Collections.ObjectModel;

namespace FlowMy.Models.Nodes
{
    // ✅ KHÔNG thêm INotifyPropertyChanged — WorkflowNode đã implement
    // ✅ KHÔNG khai báo lại: PropertyChanged, OnPropertyChanged, TitleDisplayMode, TitleColorMode, TitleColorKey
    /// <summary>
    /// Nhập dữ liệu node — được tạo tự động bởi NodeGeneratorService.
    /// TODO: Thêm properties đặc thù, logic, và mô tả chi tiết.
    /// </summary>
    public sealed class ShowInputMsgNode : WorkflowNode
    {
        // TODO: Khai báo properties đặc thù của node ở đây.
        // Ví dụ:
        // private string _someProperty = string.Empty;
        // public string SomeProperty
        // {
        //     get => _someProperty;
        //     set { if (_someProperty != value) { _someProperty = value; OnPropertyChanged(); } }
        // }

        // Output keys mà node này produce:
        public const string OutputKey_JsonDynamic = "jsonDynamic";

        public ShowInputMsgNode()
        {
            Type = NodeType.ShowInputMsg;
            Title = "Nhập dữ liệu";

            // ⚠️ KHÔNG thêm Ports ở đây — TemplateFactory sẽ tạo port để tránh duplicate.
            // Ports sẽ được tạo trong TemplateFactory.CreateYourNode()
        }
    }
}
