using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace ACSEventConsole.Controllers;

/// <summary>
/// 显式注册控制器，避免 MVC 扫描整个程序集。
/// CHCNetSDK 内含部分对齐异常的互操作结构体，全量 GetTypes() 会触发 ReflectionTypeLoadException。
/// </summary>
internal sealed class ExplicitControllersFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    private static readonly Type[] ControllerTypes =
    [
        typeof(AbnormalController),
        typeof(AcsEventsController),
        typeof(ChannelsController),
        typeof(ConfigController),
        typeof(DashboardController),
        typeof(DevicesController),
        typeof(EmployeesController),
        typeof(EventsController),
        typeof(HealthController),
        typeof(HomeController),
        typeof(ImagesController),
        typeof(LimitCountController),
    ];

    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (Type controllerType in ControllerTypes)
        {
            feature.Controllers.Add(controllerType.GetTypeInfo());
        }
    }
}
