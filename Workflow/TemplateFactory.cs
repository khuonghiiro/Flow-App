using FlowMy.Models;
using FlowMy.Models.Nodes;
using FlowMy.Services.Utilities;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace FlowMy.Workflow
{
    public sealed class TemplateFactory
    {
        private readonly ColorThemeService _colorThemeService;

        public TemplateFactory(ColorThemeService colorThemeService)
        {
            _colorThemeService = colorThemeService ?? throw new ArgumentNullException(nameof(colorThemeService));
        }

        public WorkflowNode Create(string nodeType, double x, double y)
        {
            return nodeType switch
            {
                "Start" => CreateStartNode(x, y),
                "End" => CreateEndNode(x, y),
                "ScreenPosition" => CreateScreenPositionNode(x, y),
                "Input" => CreateInputNode(x, y),
                "Output" => CreateOutputNode(x, y),
                "Notification" => CreateNotificationNode(x, y),
                "Process" => CreateProcessNode(x, y),
                "IfElse" => CreateIfElseNode(x, y),
                "Loop" => CreateLoopNode(x, y),
                "Delay" => CreateDelayNode(x, y),
                "KeyPressEvent" => CreateKeyPressEventNode(x, y),
                "HotkeyPressEvent" => CreateHotkeyPressEventNode(x, y),
                "MouseEvent" => CreateMouseEventNode(x, y),
                "Variable" => CreateVariableNode(x, y),
                "Function" => CreateFunctionNode(x, y),
                "ScreenCapture" => CreateScreenCaptureNode(x, y),
                "Break" => CreateBreakNode(x, y),
                "Continue" => CreateContinueNode(x, y),
                "StringSplit" => CreateStringSplitNode(x, y),
                "ListOut" => CreateListOutNode(x, y),
                "HttpRequest" => CreateHttpRequestNode(x, y),
                "AssignData" => CreateAssignDataNode(x, y),
                "MediaGallery" => CreateMediaGalleryNode(x, y),
                "ImageProcessing" => CreateImageProcessingNode(x, y),
                "VideoProcessing" => CreateVideoProcessingNode(x, y),
                "Code" => CreateCodeNode(x, y),
                "HtmlUi" => CreateHtmlUiNode(x, y),
                "Folder" => CreateFolderNode(x, y),
                "Web" => CreateWebNode(x, y),
                "AsyncTask" => CreateAsyncTaskNode(x, y),
                "AsyncTaskDispatchCollect" => CreateAsyncTaskDispatchCollectNode(x, y),
                "Storage" => CreateStorageNode(x, y),
                "Callback" => CreateCallbackNode(x, y),
                "DataFetcher" => CreateDataFetcherNode(x, y),
                "FileDownload" => CreateFileDownloadNode(x, y),
                "BodyContainer" => CreateBodyContainerNode(x, y),
                "FolderFilePaths" => CreateFolderFilePathsNode(x, y),
                "KeyValueBridge" => CreateKeyValueBridgeNode(x, y),
                "FlowOverwrite" => CreateFlowOverwriteNode(x, y),
                "GitSource" => CreateGitSourceNode(x, y),
                "MacroRecorder" => CreateMacroRecorderNode(x, y),
                "BorderHighlight" => CreateBorderHighlightNode(x, y),
                "TextScan" => CreateTextScanNode(x, y),
                _ => throw new NotSupportedException($"Unknown node type '{nodeType}'.")
            };
        }

        private WorkflowNode CreateAsyncTaskDispatchCollectNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("SkyAzureBrush")
                ?? _colorThemeService.GetBrush("PrimaryBrush")
                ?? Brushes.DodgerBlue;

            var node = new AsyncTaskDispatchCollectNode
            {
                Id = $"Node_AsyncTaskDispatchCollect_{Guid.NewGuid()}",
                Title = "Collect AsyncTask Results",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "SkyAzure",
                Type = NodeType.AsyncTaskDispatchCollect
            };

            return node;
        }

        private WorkflowNode CreateImageProcessingNode(double x, double y)
        {
            var node = new ImageProcessingNode
            {
                Id = $"Node_ImageProcessing_{Guid.NewGuid()}",
                Title = "Xử lý ảnh",
                X = x - 180,
                Y = y - 140,
                ColorKey = "CharcoalDark",
                NodeBrush = Application.Current.TryFindResource("CharcoalDarkBrush") as Brush ?? Brushes.MediumPurple,
                Type = NodeType.ImageProcessing,
                Width = 360,
                Height = 280
            };

            // Flow ports
            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            });
            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"
            });

            // Outputs (dùng UserValueOverride để NodeDataPanelService có thể resolve chung)
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "imagePath", DisplayName = "Image - Path", IsMultiple = false, OutputType = WorkflowDataType.String });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "imageBase64", DisplayName = "Image - Base64 (PNG)", IsMultiple = false, OutputType = WorkflowDataType.String });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "imageWidth", DisplayName = "Image - Width", IsMultiple = false, OutputType = WorkflowDataType.Number });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "imageHeight", DisplayName = "Image - Height", IsMultiple = false, OutputType = WorkflowDataType.Number });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "cropListBase64", DisplayName = "Crops - List Base64 (JSON)", IsMultiple = false, OutputType = WorkflowDataType.String });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "cropBase64", DisplayName = "Crop - Base64 (từ Image Processor)", IsMultiple = false, OutputType = WorkflowDataType.String });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "aspectRatio", DisplayName = "Aspect Ratio (16:9 hoặc 9:16)", IsMultiple = false, OutputType = WorkflowDataType.String });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "promptSize", DisplayName = "Prompt Size (số lần gửi)", IsMultiple = false, OutputType = WorkflowDataType.Number });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "prompt", DisplayName = "Prompt (text từ Image Processor)", IsMultiple = false, OutputType = WorkflowDataType.String });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "cropName", DisplayName = "Crop Name (Image_{Order}_{DateTime})", IsMultiple = false, OutputType = WorkflowDataType.String });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "cropWidth", DisplayName = "Crop Width", IsMultiple = false, OutputType = WorkflowDataType.Number });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "cropHeight", DisplayName = "Crop Height", IsMultiple = false, OutputType = WorkflowDataType.Number });
            
            // Mặc định checked cho imageBase64 và cropListBase64 (skip = true nghĩa là không xử lý)
            // Nhưng theo yêu cầu: checked = không xử lý, unchecked = xử lý
            // Vậy mặc định checked = thêm vào SkipOutputs
            node.SkipOutputs.Add("imageBase64");
            node.SkipOutputs.Add("cropListBase64");

            return node;
        }

        private WorkflowNode CreateVideoProcessingNode(double x, double y)
        {
            var node = new VideoProcessingNode
            {
                Id = $"Node_VideoProcessing_{Guid.NewGuid()}",
                Title = "Video Processing",
                X = x - 270,
                Y = y - 170,
                ColorKey = "GraphiteGray",
                NodeBrush = Application.Current.TryFindResource("GraphiteGrayBrush") as Brush ?? Brushes.DimGray,
                Type = NodeType.VideoProcessing,
                Width = 1360,
                Height = 768,
                OutputBase64 = true,
                PreferGpu = true
            };

            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            });
            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"
            });

            node.DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "video_input",
                DisplayName = "Video Input",
                IsMultiple = false,
                OutputType = WorkflowDataType.String
            });
            node.DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "folder_input",
                DisplayName = "Output Folder Input",
                IsMultiple = false,
                OutputType = WorkflowDataType.String
            });
            node.DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "audio_inputs",
                DisplayName = "Audio Inputs",
                IsMultiple = true,
                OutputType = WorkflowDataType.String
            });

            node.DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "frames_output",
                DisplayName = "Frames Output",
                IsMultiple = true,
                OutputType = WorkflowDataType.String
            });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "video_output",
                DisplayName = "Video Output",
                IsMultiple = false,
                OutputType = WorkflowDataType.String
            });

            return node;
        }

        private WorkflowNode CreateStartNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("SkyAzureBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.Blue;

            // Kích thước node tròn là 60x60
            var node = new WorkflowNode
            {
                Id = $"Node_Start_{Guid.NewGuid()}",
                Title = "Start",
                X = x - 30,  // Căn giữa node tròn 60x60
                Y = y - 30,
                NodeBrush = nodeBrush,
                ColorKey = "SkyAzure",
                Type = NodeType.Start
            };
            node.Ports.Add(new NodePort { IsInput = true, Position = PortPosition.Left, IsVisible = true });
            node.Ports.Add(new NodePort { IsInput = false, Position = PortPosition.Right, IsVisible = true });
            return node;
        }

        private WorkflowNode CreateEndNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("DangerBrush") ?? Brushes.Red;

            // Kích thước node tròn là 60x60
            var node = new WorkflowNode
            {
                Id = $"Node_End_{Guid.NewGuid()}",
                Title = "End",
                X = x - 30,  // Căn giữa node tròn 60x60
                Y = y - 30,
                NodeBrush = nodeBrush,
                ColorKey = "Danger",
                Type = NodeType.End
            };
            node.Ports.Add(new NodePort { IsInput = true, Position = PortPosition.Left, IsVisible = true });
            node.Ports.Add(new NodePort { IsInput = false, Position = PortPosition.Right, IsVisible = true });
            return node;
        }

        private WorkflowNode CreateScreenPositionNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("AmethystBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.Purple;

            var node = new ScreenPositionPickerNode
            {
                Id = $"Node_ScreenPosition_{Guid.NewGuid()}",
                Title = "Screen Position",
                X = x - 100,
                Y = y - 60,
                NodeBrush = nodeBrush,
                ColorKey = "Amethyst"
            };
            node.Ports.Add(new NodePort { IsInput = true, Position = PortPosition.Left, IsVisible = true });
            node.Ports.Add(new NodePort { IsInput = false, Position = PortPosition.Right, IsVisible = true });

            // Dynamic data (outputs)
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "x", DisplayName = "X", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "y", DisplayName = "Y", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "position", DisplayName = "Position (X,Y)", IsMultiple = false });
            return node;
        }

        private WorkflowNode CreateScreenCaptureNode(double x, double y)
        {
            var node = new ScreenCaptureNode
            {
                Id = $"Node_ScreenCapture_{Guid.NewGuid()}",
                Title = "Screen Capture",
                X = x - 110,  // Căn giữa (220/2 = 110)
                Y = y - 70,   // Căn giữa (140/2 = 70)
                NodeBrush = _colorThemeService.GetBrush("SuccessBrush") ?? Brushes.Green,
                ColorKey = "Success",
                Type = NodeType.ScreenCapture
            };

            // Thêm ports
            node.Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"           // Port IN: dùng màu Info theo guideline
            });

            node.Ports.Add(new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"   // Port OUT: dùng màu SunsetOrange theo guideline
            });

            // Dynamic data (outputs)
            // Ảnh
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "imageSizeBytes", DisplayName = "Dung lượng ảnh (bytes)", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "imageWidth", DisplayName = "Ảnh - Width", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "imageHeight", DisplayName = "Ảnh - Height", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "imageBase64", DisplayName = "Ảnh - Base64 (PNG)", IsMultiple = false });

            // Vùng chụp
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "captureX", DisplayName = "Vị trí chụp - X", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "captureY", DisplayName = "Vị trí chụp - Y", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "captureWidth", DisplayName = "Vùng chụp - Width", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "captureHeight", DisplayName = "Vùng chụp - Height", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "captureRect", DisplayName = "Vùng chụp - Rect (X,Y,W,H)", IsMultiple = false });

            return node;
        }

        private WorkflowNode CreateBreakNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("DangerBrush")
                            ?? Brushes.Red;

            var node = new BreakNode
            {
                Id = $"Node_Break_{Guid.NewGuid()}",
                Title = "Break",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "Danger",
                Type = NodeType.Break
            };

            // Chỉ có port IN (vì break không có output)
            //node.Ports.Add(new NodePort
            //{
            //    IsInput = true,
            //    Position = PortPosition.Left,
            //    IsVisible = true
            //});

            return node;
        }

        public static WorkflowNode CreateStringSplitNode(double x, double y)
        {
            var node = new StringSplitNode
            {
                Id = Guid.NewGuid().ToString(),
                X = x,
                Y = y,
                // ✅ Theme color
                ColorKey = "Retro",
                NodeBrush = Application.Current.TryFindResource("RetroBrush") as Brush ?? Brushes.MidnightBlue
            };

            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
            });

            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
            });

            return node;
        }

        public static WorkflowNode CreateListOutNode(double x, double y)
        {
            var node = new ListOutNode
            {
                Id = Guid.NewGuid().ToString(),
                X = x,
                Y = y,
                // ✅ Theme color - using a distinct color
                ColorKey = "Fluidity",
                NodeBrush = Application.Current.TryFindResource("FluidityBrush") as Brush ?? Brushes.Teal
            };

            // Input port (left) - receives flow
            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
            });

            // Output port (right) - sends flow to downstream nodes
            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
            });

            return node;
        }

        private WorkflowNode CreateAssignDataNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("BerryPurpleBrush")
                            ?? _colorThemeService.GetBrush("InfoBrush")
                            ?? Brushes.DarkCyan;

            var node = new AssignDataNode
            {
                Id = $"Node_AssignData_{Guid.NewGuid()}",
                Title = "Gán dữ liệu",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "BerryPurple",
                Type = NodeType.AssignData
            };
            // Ports đã được thêm trong AssignDataNode constructor
            return node;
        }

        private WorkflowNode CreateMediaGalleryNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("CharcoalMistBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.Gray;

            var node = new MediaGalleryNode
            {
                Id = $"Node_MediaGallery_{Guid.NewGuid()}",
                Title = "Gallery ảnh/video",
                X = x - 160,
                Y = y - 140,
                NodeBrush = nodeBrush,
                ColorKey = "CharcoalMist",
                Type = NodeType.MediaGallery
            };
            return node;
        }

        private WorkflowNode CreateStorageNode(double x, double y)
        {
            // Node lưu trữ dữ liệu toàn cục – dùng VioletHaze theo yêu cầu
            var nodeBrush = _colorThemeService.GetBrush("VioletHazeBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.MediumPurple;

            var node = new StorageNode
            {
                Id = $"Node_Storage_{Guid.NewGuid()}",
                Title = "Storage",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "VioletHaze",
                Type = NodeType.Storage,
                IsInputMode = true // Mặc định: port IN visible
            };

            // Thêm port IN (input port) - nhận flow từ node khác
            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = node.IsInputMode, // Chỉ visible khi IsInputMode = true
                ColorKey = "Info"          // Port IN: dùng màu Info theo guideline
            });

            // Thêm port OUT (output port) - gửi flow tiếp
            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = !node.IsInputMode, // Chỉ visible khi IsInputMode = false
                ColorKey = "SunsetOrange"  // Port OUT: dùng màu SunsetOrange theo guideline
            });

            // DynamicOutputs ban đầu có một key mặc định "value".
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "value",
                DisplayName = "Stored Value",
                IsMultiple = false,
                OutputType = WorkflowDataType.String
            });

            return node;
        }

        private WorkflowNode CreateCodeNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("PrussianBlueBrush")
                ?? _colorThemeService.GetBrush("WarningBrush")
                ?? Brushes.Orange;

            var node = new CodeNode
            {
                Id = $"Node_Code_{Guid.NewGuid()}",
                Title = "Code",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "PrussianBlue",
                Type = NodeType.Code
            };
            return node;
        }

        private WorkflowNode CreateFolderNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("GoldenYellowBrush")
                ?? _colorThemeService.GetBrush("SuccessBrush")
                ?? Brushes.DarkOliveGreen;

            var node = new FolderNode
            {
                Id = $"Node_Folder_{Guid.NewGuid()}",
                Title = "Folder",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "GoldenYellow",
                Type = NodeType.Folder
            };
            return node;
        }

        private WorkflowNode CreateWebNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("SkyAzureBrush")
                ?? _colorThemeService.GetBrush("PrimaryBrush")
                ?? Brushes.DodgerBlue;

            var node = new WebNode
            {
                Id = $"Node_Web_{Guid.NewGuid()}",
                Title = "Web",
                X = x - 210,
                Y = y - 160,
                NodeBrush = nodeBrush,
                ColorKey = "SkyAzure",
                Type = NodeType.Web
            };
            return node;
        }

        private WorkflowNode CreateHtmlUiNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("EspressoBrownBrush")
                ?? _colorThemeService.GetBrush("PrimaryBrush")
                ?? Brushes.Gray;

            var node = new HtmlUiNode
            {
                Id = $"Node_HtmlUi_{Guid.NewGuid()}",
                Title = "HTML UI",
                X = x - 210,
                Y = y - 160,
                NodeBrush = nodeBrush,
                ColorKey = "EspressoBrown",
                Type = NodeType.HtmlUi,
                Width = 420,
                Height = 320
            };
            return node;
        }

        private WorkflowNode CreateContinueNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("InfoBrush")
                            ?? Brushes.Cyan;

            var node = new ContinueNode
            {
                Id = $"Node_Continue_{Guid.NewGuid()}",
                Title = "Continue",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "Info",
                Type = NodeType.Continue
            };

            // Chỉ có port IN (vì continue không có output)
            //node.Ports.Add(new NodePort
            //{
            //    IsInput = true,
            //    Position = PortPosition.Left,
            //    IsVisible = true
            //});


            return node;
        }

        private WorkflowNode CreateInputNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("OceanBrush")
                            ?? _colorThemeService.GetBrush("InfoBrush")
                            ?? Brushes.Cyan;

            var node = new InputNode
            {
                Id = $"Node_Input_{Guid.NewGuid()}",
                Title = "Input",
                X = x - 140,
                Y = y - 90,
                NodeBrush = nodeBrush,
                ColorKey = "Ocean",
                Type = NodeType.Input,
                Key = string.Empty,
                Value = string.Empty,
                DataType = WorkflowDataType.String
            };
            node.Ports.Add(new NodePort { IsInput = false, Position = PortPosition.Right, IsVisible = true });

            // Dynamic data (outputs) - sử dụng Key từ node, nếu Key rỗng thì dùng "Input" làm default
            var outputKey = string.IsNullOrWhiteSpace(node.Key) ? "Input" : node.Key;
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = outputKey,
                DisplayName = "Value",
                IsMultiple = false,
                OutputType = node.DataType // Value có type theo DataType của node
            });
            return node;
        }

        private WorkflowNode CreateOutputNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("EmeraldBrush")
                            ?? _colorThemeService.GetBrush("SuccessBrush")
                            ?? Brushes.Green;

            var node = new OutputNode
            {
                Id = $"Node_Output_{Guid.NewGuid()}",
                Title = "Output",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "Emerald",
                Type = NodeType.Output,
                OutputKey = "output",
                FormatString = string.Empty
            };

            // Input port (left) - receives flow
            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            });

            // Output port (right) - sends flow to downstream nodes
            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"
            });

            // Rebuild DynamicOutputs với OutputKey
            node.RebuildDynamicOutputs();

            return node;
        }

        private WorkflowNode CreateNotificationNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("CantaloupeOrangeBrush")
                            ?? _colorThemeService.GetBrush("WarningBrush")
                            ?? Brushes.Orange;

            var node = new NotificationNode
            {
                Id = $"Node_Notification_{Guid.NewGuid()}",
                Title = "Notification",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "CantaloupeOrange",
                Type = NodeType.Notification,
                DefaultDurationSeconds = 5
            };

            return node;
        }

        private WorkflowNode CreateProcessNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("IndigoBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.Blue;

            var node = new WorkflowNode
            {
                Id = $"Node_Process_{Guid.NewGuid()}",
                Title = "Process",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "Indigo",
                Type = NodeType.Process
            };
            node.Ports.Add(new NodePort { IsInput = true, Position = PortPosition.Left, IsVisible = true });
            node.Ports.Add(new NodePort { IsInput = false, Position = PortPosition.Right, IsVisible = true });

            // Dynamic data (pass-through)
            node.DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "in",
                DisplayName = "Data In",
                IsMultiple = true
            });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "out",
                DisplayName = "Data Out",
                IsMultiple = true
            });
            return node;
        }

        private WorkflowNode CreateIfElseNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("LavenderBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.Blue;

            var node = new WorkflowNode
            {
                Id = $"Node_IfElse_{Guid.NewGuid()}",
                Title = "Điều kiện",
                X = x - 100,
                Y = y - 60,
                NodeBrush = nodeBrush,
                ColorKey = "Lavender",
                Type = NodeType.IfElse,
                Condition = "Điều kiện",
                ConditionalVisualMode = ConditionalVisualMode.Diamond
            };

            node.Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            }
            );

            var ifBranch = new ConditionalBranch { Label = "if", Condition = "condition", CanRemove = false };
            var ifPort = new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ExecutionOrder = 0,
                ColorKey = "SunsetOrange",
                ExecutionMode = PortExecutionMode.Sequential
            };
            ifBranch.Port = ifPort;
            node.Ports.Add(ifPort);
            node.ConditionalBranches.Add(ifBranch);

            var elseBranch = new ConditionalBranch { Label = "else", Condition = null, CanRemove = false };
            var elsePort = new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ExecutionOrder = 1,
                ColorKey = "SunsetOrange",
                ExecutionMode = PortExecutionMode.Sequential
            };
            elseBranch.Port = elsePort;
            node.Ports.Add(elsePort);
            node.ConditionalBranches.Add(elseBranch);

            return node;
        }

        private WorkflowNode CreateAsyncTaskNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("MintChocolateBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.Blue;

            var node = new AsyncTaskNode
            {
                Id = $"Node_AsyncTask_{Guid.NewGuid()}",
                Title = "Async Task",
                X = x - 100,
                Y = y - 60,
                NodeBrush = nodeBrush,
                ColorKey = "MintChocolate",
                UiPresentationMode = AsyncTaskUiPresentationMode.LoopLikeDispatch
            };
            ConfigureAsyncTaskLoopLikePorts(node);

            return node;
        }

        private WorkflowNode CreateLoopNode(double x, double y)
        {
            // ✅ Random màu cho mỗi Loop Node tạo mới
            // var nodeBrush = GetRandomBrush();

            var nodeBrush = _colorThemeService.GetBrush("WarningBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.Orange;

            var node = new LoopNode
            {
                Id = $"Node_Loop_{Guid.NewGuid()}",
                Title = "Repeat N Times",
                X = x - 120,
                Y = y - 80,
                NodeBrush = nodeBrush,
                //ColorKey = "Custom", // Đánh dấu là Custom để không bị Theme ghi đè
                ColorKey = "Warning",
                Type = NodeType.Loop
            };

            // ✅ CHỈ CÓ 3 PORTS CƠ BẢN + 1 PORT INDEX (FOR/REPEAT/FOREACH):
            // Tổng cộng tối đa 4 ports.

            // 1. Port IN (Trái) - Nhận luồng điều khiển (Flow In)
            node.Ports.Add(new NodePort
            {
                Id = "LoopNodeIn",
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            });

            // 2. Port OUT bên phải (sau khi loop xong - nhận từ Loop Body Left)
            node.Ports.Add(new NodePort
            {
                Id = "LoopNodeOut",
                IsInput = false,  // Port OUT 
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange" // Có thể set màu riêng ở đây (ví dụ: Info = Cyan)
            });

            // 3. Port BOTTOM (kết nối xuống Loop Body Top - là OUTPUT)
            node.Ports.Add(new NodePort
            {
                Id = "LoopNodeBottom",
                IsInput = false,  // ✅ OUTPUT - đẩy xuống Loop Body Top
                Position = PortPosition.Bottom,
                IsVisible = true,
                CanDeleteConnection = false, // ✅ Không cho xóa connection này
                ColorKey = "ChocolateBrown"
            });

            // 4. Port Index (Output) - Cho For/Repeat/ForEach Loop (Mặc định Visible)
            node.Ports.Add(new NodePort
            {
                Id = "LoopIndexOut",
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "CoralVivid" // Cyan/Blue cho biến số
            });

            // Dynamic data (outputs)
            //node.DynamicOutputs.Add(new WorkflowDynamicDataPort
            //{
            //    Key = "index",
            //    DisplayName = "Loop Index",
            //    IsMultiple = false
            //});

            return node;
        }

        private WorkflowNode CreateDelayNode(double x, double y)
        {
            // Lấy màu từ theme - dùng ForestPine (theo yêu cầu thiết kế mới)
            var nodeBrush = _colorThemeService.GetBrush("ForestPineBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.ForestGreen;

            var node = new DelayNode
            {
                Id = $"Node_Delay_{Guid.NewGuid()}",
                Title = "Delay",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "ForestPine",
                Type = NodeType.Delay,
                DelayUnit = DelayTimeUnit.Seconds,
                DelayValue = 1d
            };

            // Input (trái)
            node.Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"           // Port IN: dùng màu Info theo guideline
            });

            // Output (phải)
            node.Ports.Add(new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"   // Port OUT: dùng màu SunsetOrange theo guideline
            });

            return node;
        }

        private WorkflowNode CreateKeyPressEventNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("ChocolateBrownBrush")
                            ?? _colorThemeService.GetBrush("InfoBrush")
                            ?? Brushes.DeepSkyBlue;

            var node = new KeyPressEventNode
            {
                Id = $"Node_KeyPressEvent_{Guid.NewGuid()}",
                Title = "Key Press",
                X = x - 90,
                Y = y - 55,
                NodeBrush = nodeBrush,
                ColorKey = "ChocolateBrown",
                Type = NodeType.KeyPressEvent,
            };

            // Default key
            node.Key = "F8";

            // Flow ports
            node.Ports.Add(new NodePort { IsInput = true, Position = PortPosition.Left, IsVisible = true });
            node.Ports.Add(new NodePort { IsInput = false, Position = PortPosition.Right, IsVisible = true });

            // Dynamic data (output): selected key - sử dụng Key từ node, nếu Key rỗng thì dùng "key" làm default
            var outputKey = string.IsNullOrWhiteSpace(node.Key) ? "key" : node.Key;
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = outputKey,
                DisplayName = "Key",
                IsMultiple = false,
                OutputType = WorkflowDataType.String
            });

            return node;
        }

        private WorkflowNode CreateHotkeyPressEventNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("IndigoBrush")
                            ?? _colorThemeService.GetBrush("ChocolateBrownBrush")
                            ?? Brushes.Indigo;

            var node = new HotkeyPressEventNode
            {
                Id = $"Node_HotkeyPressEvent_{Guid.NewGuid()}",
                Title = "Hotkey Press",
                X = x - 100,
                Y = y - 55,
                NodeBrush = nodeBrush,
                ColorKey = "Indigo",
                Type = NodeType.HotkeyPressEvent,
            };

            // Default hotkey
            node.Key = "Ctrl+Alt+F8";

            // Flow ports
            node.Ports.Add(new NodePort { IsInput = true, Position = PortPosition.Left, IsVisible = true });
            node.Ports.Add(new NodePort { IsInput = false, Position = PortPosition.Right, IsVisible = true });

            // Dynamic data (output): selected hotkey - sử dụng Key từ node, nếu Key rỗng thì dùng "key" làm default
            var outputKey = string.IsNullOrWhiteSpace(node.Key) ? "key" : node.Key;
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = outputKey,
                DisplayName = "Hotkey",
                IsMultiple = false,
                OutputType = WorkflowDataType.String
            });

            return node;
        }

        private WorkflowNode CreateMouseEventNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("TurquoiseBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.Teal;

            var node = new MouseEventNode
            {
                Id = $"Node_MouseEvent_{Guid.NewGuid()}",
                Title = "Mouse Event",
                X = x - 90,
                Y = y - 50,
                NodeBrush = nodeBrush,
                ColorKey = "Turquoise",
                Type = NodeType.MouseEvent
            };

            // Input port (trái)
            node.Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            });

            // Output port (phải)
            node.Ports.Add(new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"
            });

            return node;
        }

        private WorkflowNode CreateVariableNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("FluidityBrush")
                            ?? _colorThemeService.GetBrush("InfoBrush")
                            ?? Brushes.Cyan;

            var node = new WorkflowNode
            {
                Id = $"Node_Variable_{Guid.NewGuid()}",
                Title = "Variable",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "Fluidity",
                Type = NodeType.Variable
            };
            node.Ports.Add(new NodePort { IsInput = true, Position = PortPosition.Left, IsVisible = true });
            node.Ports.Add(new NodePort { IsInput = false, Position = PortPosition.Right, IsVisible = true });

            // Dynamic data (pass-through)
            node.DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "value",
                DisplayName = "Value",
                IsMultiple = true
            });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "value",
                DisplayName = "Value",
                IsMultiple = true
            });
            return node;
        }

        private WorkflowNode CreateFunctionNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("PlumPurpleBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.Blue;

            var node = new WorkflowNode
            {
                Id = $"Node_Function_{Guid.NewGuid()}",
                Title = "Function",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "PlumPurple",
                Type = NodeType.Function
            };
            node.Ports.Add(new NodePort { IsInput = true, Position = PortPosition.Left, IsVisible = true });
            node.Ports.Add(new NodePort { IsInput = false, Position = PortPosition.Right, IsVisible = true });

            // Dynamic data (pass-through)
            node.DynamicInputs.Add(new WorkflowDynamicDataPort
            {
                Key = "in",
                DisplayName = "Args / Data In",
                IsMultiple = true
            });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort
            {
                Key = "out",
                DisplayName = "Return / Data Out",
                IsMultiple = true
            });
            return node;
        }

        public static WorkflowNode CreateHttpRequestNode(double x, double y)
        {
            var node = new HttpRequestNode
            {
                Id = Guid.NewGuid().ToString(),
                X = x,
                Y = y,
                // Using Cyan/Teal theme for HTTP/API operations
                ColorKey = "Turquoise",
                NodeBrush = Application.Current.TryFindResource("TurquoiseBrush") as Brush ?? Brushes.Teal
            };

            // Input port (left) - receives flow
            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
            });

            // Output port (right) - sends flow to downstream nodes
            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
            });

            return node;
        }

        public static WorkflowNode CreateCallbackNode(double x, double y)
        {
            var node = new CallbackNode
            {
                Id = Guid.NewGuid().ToString(),
                X = x,
                Y = y,
                ColorKey = "CrimsonRose",
                NodeBrush = Application.Current.TryFindResource("CrimsonRoseBrush") as Brush ?? Brushes.Crimson
            };

            // Callback node now supports optional continue-flow mode after jump.
            // Keep ports normalized here for new nodes and imported templates.
            node.EnsurePorts();
            node.SyncPortsForBehavior();

            return node;
        }

        private WorkflowNode CreateDataFetcherNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("MintChocolateBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.SeaGreen;

            var node = new DataFetcherNode
            {
                Id = $"Node_DataFetcher_{Guid.NewGuid()}",
                Title = "Data Fetcher",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "MintChocolate",
                Type = NodeType.DataFetcher
            };
            // Ports are added in DataFetcherNode constructor.
            return node;
        }

        private WorkflowNode CreateFileDownloadNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("CantaloupeOrangeBrush")
                            ?? _colorThemeService.GetBrush("WarningBrush")
                            ?? Brushes.Salmon;

            var node = new FileDownloadNode
            {
                Id = $"Node_FileDownload_{Guid.NewGuid()}",
                Title = "Tải file",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "CantaloupeOrange",
                Type = NodeType.FileDownload
            };
            return node;
        }

        private WorkflowNode CreateFolderFilePathsNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("ForestPineBrush")
                            ?? _colorThemeService.GetBrush("SuccessBrush")
                            ?? Brushes.SeaGreen;

            var node = new FolderFilePathsNode
            {
                Id = $"Node_FolderFilePaths_{Guid.NewGuid()}",
                Title = "File trong thư mục",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "ForestPine",
                Type = NodeType.FolderFilePaths
            };
            return node;
        }

        private WorkflowNode CreateKeyValueBridgeNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("SeaFoamBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.MediumAquamarine;

            var node = new KeyValueBridgeNode
            {
                Id = $"Node_KeyValueBridge_{Guid.NewGuid()}",
                Title = "KeyValue Bridge",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "SeaFoam",
                Type = NodeType.KeyValueBridge
            };
            node.RefreshFlowPortsVisibility();
            return node;
        }

        private WorkflowNode CreateFlowOverwriteNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("KiwiGreenBrush")
                            ?? _colorThemeService.GetBrush("SuccessBrush")
                            ?? Brushes.ForestGreen;

            var node = new FlowOverwriteNode
            {
                Id = $"Node_FlowOverwrite_{Guid.NewGuid()}",
                Title = "Flow Overwrite",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush,
                ColorKey = "KiwiGreen",
                Type = NodeType.FlowOverwrite
            };
            return node;
        }

        private WorkflowNode CreateGitSourceNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("IndigoBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.Indigo;

            var node = new GitSourceNode
            {
                Id = $"Node_GitSource_{Guid.NewGuid()}",
                Title = "Git Source",
                X = x - 30,
                Y = y - 30,
                NodeBrush = nodeBrush,
                ColorKey = "Indigo",
                Type = NodeType.GitSource
            };

            node.Ports.Clear();
            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            });
            node.Ports.Add(new NodePort
            {
                Id = Guid.NewGuid().ToString(),
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"
            });

            return node;
        }

        private WorkflowNode CreateBodyContainerNode(double x, double y)
        {
            var node = new BodyContainerNode
            {
                Id = $"Node_BodyContainer_{Guid.NewGuid()}",
                X = x - 400,
                Y = y - 200,
                NodeBrush = _colorThemeService.GetBrush("CharcoalMistBrush") ?? Brushes.SlateGray,
                ColorKey = "CharcoalMist",
                Type = NodeType.BodyContainer
            };
            return node;
        }

        private WorkflowNode CreateMacroRecorderNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("MangoTangoBrush")
                            ?? _colorThemeService.GetBrush("WarningBrush")
                            ?? Brushes.OrangeRed;

            return new MacroRecorderNode
            {
                Id = $"Node_MacroRecorder_{Guid.NewGuid()}",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush
            };
        }

        private WorkflowNode CreateBorderHighlightNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("AzureBlueBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.DodgerBlue;

            return new BorderHighlightNode
            {
                Id = $"Node_BorderHighlight_{Guid.NewGuid()}",
                X = x - 75,
                Y = y - 40,
                NodeBrush = nodeBrush
            };
        }

        private WorkflowNode CreateTextScanNode(double x, double y)
        {
            var nodeBrush = _colorThemeService.GetBrush("VioletDeepBrush")
                            ?? _colorThemeService.GetBrush("PrimaryBrush")
                            ?? Brushes.MediumPurple;

            var node = new TextScanNode
            {
                Id = $"Node_TextScan_{Guid.NewGuid()}",
                Title = "Text Scan (OCR)",
                X = x - 110,
                Y = y - 70,
                NodeBrush = nodeBrush,
                ColorKey = "VioletDeep",
                Type = NodeType.TextScan
            };

            // Thêm ports
            node.Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            });

            node.Ports.Add(new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"
            });

            // Dynamic data (outputs)
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "extractedText", DisplayName = "Extracted Text", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "extractedTextLines", DisplayName = "Extracted Text (Lines)", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "wordCount", DisplayName = "Word Count", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "imageWidth", DisplayName = "Image - Width", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "imageHeight", DisplayName = "Image - Height", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "imageBase64", DisplayName = "Image - Base64 (PNG)", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "captureX", DisplayName = "Capture - X", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "captureY", DisplayName = "Capture - Y", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "captureWidth", DisplayName = "Capture - Width", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "captureHeight", DisplayName = "Capture - Height", IsMultiple = false });
            node.DynamicOutputs.Add(new WorkflowDynamicDataPort { Key = "ocrLanguage", DisplayName = "OCR Language", IsMultiple = false });

            return node;
        }

        private Brush GetRandomBrush()
        {
            var random = new Random();
            var color = Color.FromRgb((byte)random.Next(50, 200), (byte)random.Next(50, 200), (byte)random.Next(50, 200));
            return new SolidColorBrush(color);
        }

        /// <summary>Chuyển AsyncTask sang cổng giống Loop + tạo body ảo (gọi sau khi host đã gỡ kết nối nhánh tay).</summary>
        public void ConfigureAsyncTaskLoopLikePorts(AsyncTaskNode node)
        {
            foreach (var br in node.AsyncTaskBranches.ToList())
            {
                if (br.Port != null)
                    node.Ports.Remove(br.Port);
            }
            node.AsyncTaskBranches.Clear();
            node.Ports.Clear();

            node.Ports.Add(new NodePort
            {
                Id = "LoopNodeIn",
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            });
            node.Ports.Add(new NodePort
            {
                Id = "LoopNodeOut",
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ColorKey = "SunsetOrange"
            });
            node.Ports.Add(new NodePort
            {
                Id = "LoopNodeBottom",
                IsInput = false,
                Position = PortPosition.Bottom,
                IsVisible = true,
                CanDeleteConnection = false,
                ColorKey = "ChocolateBrown"
            });
            node.Ports.Add(new NodePort
            {
                Id = "LoopIndexOut",
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = false,
                ColorKey = "CoralVivid"
            });

            node.AsyncTaskBodyNode ??= new AsyncTaskBodyNode
            {
                Id = $"AsyncTaskBody_{Guid.NewGuid()}",
                ParentAsyncTaskNode = node,
                Title = "Async Task Body"
            };
            node.AsyncTaskBodyNode.ParentAsyncTaskNode = node;
            node.EnsureDispatchDynamicPorts();
        }

        /// <summary>Khôi phục hai nhánh task mặc định (sau khi host đã gỡ kết nối loop/body).</summary>
        public void ConfigureAsyncTaskManualPorts(AsyncTaskNode node)
        {
            node.DefaultConnection = null;
            // Keep references to container/body visuals so renderer can remove them properly
            // when switching back from loop-like mode.
            if (node.AsyncTaskBodyNode != null)
                node.AsyncTaskBodyNode.ParentAsyncTaskNode = node;

            foreach (var br in node.AsyncTaskBranches.ToList())
            {
                if (br.Port != null)
                    node.Ports.Remove(br.Port);
            }
            node.AsyncTaskBranches.Clear();
            node.Ports.Clear();

            node.Ports.Add(new NodePort
            {
                IsInput = true,
                Position = PortPosition.Left,
                IsVisible = true,
                ColorKey = "Info"
            });

            var task1Branch = new AsyncTaskBranch { Label = "Task", CanRemove = false };
            var task1Port = new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ExecutionOrder = 0,
                ColorKey = "ChocolateBrown",
                ExecutionMode = node.RunInParallel ? PortExecutionMode.Parallel : PortExecutionMode.Sequential
            };
            task1Branch.Port = task1Port;
            node.Ports.Add(task1Port);
            node.AsyncTaskBranches.Add(task1Branch);

            var task2Branch = new AsyncTaskBranch { Label = "Task", CanRemove = true };
            var task2Port = new NodePort
            {
                IsInput = false,
                Position = PortPosition.Right,
                IsVisible = true,
                ExecutionOrder = 1,
                ColorKey = "OceanBlue",
                ExecutionMode = node.RunInParallel ? PortExecutionMode.Parallel : PortExecutionMode.Sequential
            };
            task2Branch.Port = task2Port;
            node.Ports.Add(task2Port);
            node.AsyncTaskBranches.Add(task2Branch);
        }
    }
}

