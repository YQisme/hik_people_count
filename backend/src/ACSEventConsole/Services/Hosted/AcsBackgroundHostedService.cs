namespace ACSEventConsole.Services.Hosted;

public sealed class AcsBackgroundHostedService : IHostedService
{
    private readonly ILogger<AcsBackgroundHostedService> _logger;
    private ACSEventMultiDeviceService _deviceService;
    private AlarmMonitorService _alarmMonitor;
    private AreaAlertMonitorService _areaAlertMonitor;
    private AbnormalMonitorService _abnormalMonitor;
    private PeopleCountMonitorService _peopleCountMonitor;
    private CapacityDoorControlService _capacityDoorControl;

    public AcsBackgroundHostedService(ILogger<AcsBackgroundHostedService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("门禁后台服务启动中，配置文件: {ConfigPath}", ConfigPaths.DeviceConfigPath);

        try
        {
            string employeeConfigPath = ConfigPaths.EmployeeConfigPath;
            if (File.Exists(employeeConfigPath))
            {
                EmployeeNameRegistry.LoadFromFile(employeeConfigPath);
                _logger.LogInformation("已从本地 JSON 加载 {Count} 条员工信息: {Path}",
                    EmployeeNameRegistry.NameMap.Count, employeeConfigPath);
            }
            else
            {
                _logger.LogWarning("本地员工表不存在: {Path}", employeeConfigPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取本地员工表失败");
        }

        RuntimeConfig runtimeConfig = RuntimeConfig.LoadDefault();

        _deviceService = new ACSEventMultiDeviceService();
        _deviceService.Start();

        _alarmMonitor = new AlarmMonitorService(ACSEventMultiDeviceService.SharedEventStore);
        _alarmMonitor.Start();

        _areaAlertMonitor = new AreaAlertMonitorService(runtimeConfig);
        _areaAlertMonitor.Start();

        _abnormalMonitor = new AbnormalMonitorService(runtimeConfig);
        _abnormalMonitor.Start();

        _peopleCountMonitor = new PeopleCountMonitorService(runtimeConfig);
        _peopleCountMonitor.Start();

        _capacityDoorControl = new CapacityDoorControlService(_deviceService);
        _capacityDoorControl.Start();

        _logger.LogInformation("门禁后台服务已全部启动");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try { _capacityDoorControl?.Stop(); } catch { }
        try { _peopleCountMonitor?.Stop(); } catch { }
        try { _abnormalMonitor?.Stop(); } catch { }
        try { _areaAlertMonitor?.Stop(); } catch { }
        try { _alarmMonitor?.Stop(); } catch { }
        try { _deviceService?.Stop(); } catch { }

        _logger.LogInformation("门禁后台服务已停止");
        return Task.CompletedTask;
    }
}
