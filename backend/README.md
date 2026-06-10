# ACSEventConsole 后端

标准 ASP.NET Core 8 Web API 项目，负责门禁事件采集、看板数据聚合与配置管理。

## 目录结构

```
backend/
├── ACSEventConsole.sln
├── config/                          # 运行时配置（DeviceConfig.json、EmployeeConfig.json）
├── scripts/                         # 运维脚本
└── src/
    └── ACSEventConsole/
        ├── Program.cs               # ASP.NET Core 入口
        ├── appsettings.json
        ├── Controllers/             # Web API 控制器
        ├── Services/                # 应用服务（序列化、员工表等）
        │   └── Hosted/              # IHostedService 后台任务
        └── Infrastructure/          # 门禁 SDK、MQTT、配置存储等
            ├── Acs/
            ├── Builders/
            ├── Config/
            ├── Monitors/
            ├── Network/
            ├── Sdk/
            ├── State/
            └── Storage/
```

## 启动

```bash
cd backend/src/ACSEventConsole
dotnet run
```

或从解决方案根目录：

```bash
cd backend
dotnet run --project src/ACSEventConsole/ACSEventConsole.csproj
```

默认监听 `DeviceConfig.json` 中的 `webPort`（通常为 **8081**）。

## 主要接口

| 路径 | 说明 |
|------|------|
| `GET /health` | 健康检查 |
| `GET /api/dashboard` | 看板聚合数据 |
| `GET /api/dashboard/stream` | 看板 SSE 实时推送 |
| `GET /events` | 最近门禁事件 |
| `GET/POST /config` | 设备配置读写 |
| `GET /config/edit` | 在线编辑配置页面 |

更完整的接口说明见 `GetACSEvent/事件API说明.md`（历史文档目录）。

## 配置

配置文件位于 `backend/config/`。修改后需**重启后端**生效。

员工信息可通过 `backend/scripts/sync_employee_config.py` 从设备同步到 `EmployeeConfig.json`。
