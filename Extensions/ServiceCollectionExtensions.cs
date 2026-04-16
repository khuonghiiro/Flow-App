using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FlowMy.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static void AddViewModelsAndViews(this IServiceCollection services, Assembly assembly)
        {
            var types = assembly.GetTypes();

            // Thêm logging
            services.AddLogging(config =>
            {
                config.AddConsole();
                config.AddDebug(); // hiện log ở Output Debug Window
            });

            // Đăng ký tất cả ViewModel
            var viewModels = types.Where(t => t.Name.EndsWith("ViewModel") && 
                                            !t.IsAbstract && 
                                            t.Name != "WorkflowEditorViewModel");
            foreach (var vm in viewModels)
            {
                services.AddSingleton(vm); // ✅ ViewModel là Singleton (ngoại trừ WorkflowEditorViewModel)
            }

            // Đăng ký View theo Transient (tạo mới mỗi lần gọi)
            var views = types.Where(t => t.IsClass &&
                (t.Name.EndsWith("Window") || t.Name.EndsWith("Page") || t.Name.EndsWith("View")) && !t.IsAbstract);

            foreach (var view in views)
            {
                services.AddTransient(view); // ✅ View là Transient để tránh reuse instance đã .Close()
            }
        }

    }
}
