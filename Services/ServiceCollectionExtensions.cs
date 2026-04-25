using FlowMy.Services.Geometry;
using FlowMy.Services.Interaction;
using FlowMy.Services.Interfaces;
using FlowMy.Services.Layout;
using FlowMy.Services.Rendering;
using FlowMy.Services.Utilities;
using FlowMy.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using FlowMy.Services.Workflow;

namespace FlowMy.Services
{
    // Extension methods để đăng ký services
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Đăng ký để sử dụng DBLink remote và các services nền khác.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddRemoteServices(this IServiceCollection services)
        {
            //services.AddSingleton<IViewFactoryService, ViewFactoryService>(); // bỏ comment dòng này nếu muốn dùng tự động lấy router

            services.AddSingleton<IViewFactoryService, OptimizedViewFactoryService>(); // comment dòng này nếu muốn dùng ViewFactoryService tự động lấy router

            // ViewCache Service - Singleton để share cache across app
            services.AddSingleton<IViewCacheService, ViewCacheService>(provider =>
                new ViewCacheService(maxCacheSize: 30)); // Tăng cache size cho app 

            // Tray Service - quản lý NotifyIcon, menu tray, logic MVVM-friendly
            services.AddSingleton<ITrayService, TrayService>();

            return services;
        }

        /// <summary>
        /// Đăng ký các services phục vụ WorkflowEditor (scoped theo mỗi window).
        /// </summary>
        public static IServiceCollection AddWorkflowEditorServices(this IServiceCollection services)
        {
            // Per-workflow-editor scope state
            services.AddScoped<WorkflowEditorViewModel>();
            services.AddScoped<IWorkflowEditorHostAccessor, WorkflowEditorHostAccessor>();
            services.AddScoped<FlowMy.Workflow.ZIndexManager>();

            // Utilities
            services.AddSingleton<ColorThemeService>();
            services.AddScoped<GridPatternService>();
            services.AddScoped<MinimapService>();
            services.AddScoped<CanvasSizeManager>();
            services.AddScoped<ViewportCullingService>();

            // Geometry (stateless)
            services.AddSingleton<BezierGeometryGenerator>();
            services.AddSingleton<OrthogonalGeometryGenerator>();
            services.AddSingleton<StraightGeometryGenerator>();
            services.AddSingleton<OrthogonalV2GeometryGenerator>();

            // Rendering
            services.AddScoped<PortRenderer>();
            services.AddScoped<ConditionalNodeRenderer>();
            services.AddScoped<AsyncTaskNodeRenderer>();
            services.AddScoped<ScreenPositionNodeRenderer>();
            services.AddScoped<ScreenCaptureNodeRenderer>();
            services.AddScoped<LoopNodeRenderer>();
            services.AddScoped<InputNodeRenderer>();
            services.AddScoped<DelayNodeRenderer>();
            services.AddScoped<CallbackNodeRenderer>();
            services.AddScoped<KeyPressEventNodeRenderer>();
            services.AddScoped<HotkeyPressEventNodeRenderer>();
            services.AddScoped<MouseEventNodeRenderer>();
            services.AddScoped<StringSplitNodeRenderer>();
            services.AddScoped<ListOutNodeRenderer>();
            services.AddScoped<StorageNodeRenderer>();
            services.AddScoped<AssignDataNodeRenderer>();
            services.AddScoped<MediaGalleryNodeRenderer>();
            services.AddScoped<ImageProcessingNodeRenderer>();
            services.AddScoped<VideoProcessingNodeRenderer>();
            services.AddScoped<DataFetcherNodeRenderer>();
            services.AddScoped<KeyValueBridgeNodeRenderer>();
            services.AddScoped<FlowOverwriteNodeRenderer>();
            services.AddScoped<BodyContainerNodeRenderer>();
            services.AddScoped<WebNodeRenderer>();
            services.AddScoped<CodeNodeRenderer>();
            services.AddScoped<FolderNodeRenderer>();
            services.AddScoped<FileDownloadNodeRenderer>();
            services.AddScoped<FolderFilePathsNodeRenderer>();
            services.AddScoped<HtmlUiNodeRenderer>();
            services.AddScoped<OutputNodeRenderer>();
            services.AddScoped<NotificationNodeRenderer>();
            services.AddScoped<HttpRequestNodeRenderer>();
            // Không cần renderer riêng cho Break/Continue vì chúng dùng default UI
            services.AddScoped<NodeRenderer>();
            services.AddScoped<INodeRenderer>(sp => sp.GetRequiredService<NodeRenderer>());
            services.AddScoped<IConnectionRenderer, ConnectionRenderer>();

            services.AddScoped(sp => new Dictionary<FlowMy.Models.NodeType, INodeRenderer>());
            services.AddScoped<NodeRendererFactory>();

            // Layout
            services.AddScoped<HierarchicalLayout>();
            services.AddScoped<GridLayout>(sp => new GridLayout(sp.GetRequiredService<INodeRenderer>(), nodesPerRow: 4, spacing: 60));
            services.AddScoped<AutoLayoutService>();

            // Interaction
            services.AddScoped<ConnectionHandler>();
            services.AddScoped<DragDropHandler>();
            services.AddScoped<ZoomPanHandler>();
            services.AddScoped<CollisionResolver>();
            services.AddScoped<WorkflowEditorEventService>();
            services.AddScoped<NodeDialogManager>();
            services.AddSingleton<GlobalKeyboardHookService>();
            services.AddSingleton<KeyboardInputService>();
            services.AddSingleton<MouseInputService>();

            // Workflow
            services.AddScoped<FlowMy.Workflow.TemplateFactory>();
            services.AddScoped<FlowMy.Services.Workflow.WorkflowExecutionService>();
            services.AddScoped<IWorkflowPersistenceService, FileWorkflowPersistenceService>();
            services.AddScoped<IWorkflowExecutionVisualizer, WorkflowExecutionVisualizer>();

            return services;
        }
    }
}
