# 人员计数

基于海康门禁 SDK 的人员进出监控与看板系统。后端采集门禁事件并对外提供 HTTP API，前端 Vue 看板实时展示在场人数、进出记录、报警与异常信息。

## 项目结构

```
├── backend/                 # 后端（C# / .NET）
│   ├── config/              # 本地配置（*.json 不纳入 Git，见 *.example 模板）
│   ├── ACSEventConsole.sln  # 解决方案入口
│   └── GetACSEvent/
│       ├── ACSEventConsole.csproj   # 控制台服务（推荐）
│       ├── GetACSEvent.csproj       # 带界面的 SDK 示例程序
│       └── bin/Debug/net8.0/            # dotnet 编译输出目录
└── frontend/                # 前端看板（Vue 3 + Vite + TypeScript）
    ├── src/
    └── dist/                # 生产构建产物
```

## 环境要求

| 组件 | 要求 |
|------|------|
| 操作系统 | Windows x64 |
| 后端运行时 | .NET 8 |
| 后端 SDK | .NET SDK 8+ |
| 前端运行时 | Node.js 18+（推荐 20+） |
| 包管理器 | Yarn 1.x |
| 网络 | 能访问门禁设备 IP；如需 MQTT 功能需连接 MQTT Broker |

## 快速开始（开发模式）

### 1. 启动后端

```bash
cd backend/GetACSEvent
dotnet run --project ACSEventConsole.csproj
```

> 首次运行前，请从模板复制配置文件并按实际环境修改：
>
> ```bash
> cp backend/config/DeviceConfig.json.example backend/config/DeviceConfig.json
> cp backend/config/EmployeeConfig.json.example backend/config/EmployeeConfig.json
> ```

启动成功后，后端默认监听 **8081** 端口。可通过浏览器访问健康检查接口验证：

```
http://localhost:8081/health
```

### 2. 启动前端

新开一个终端：

```bash
cd frontend
yarn
yarn dev
```

浏览器打开 **http://localhost:5173** 即可查看看板。

Vite 开发服务器会将 `/api`、`/events`、`/images`、`/config`、`/health` 等请求代理到 `http://localhost:8081`，`.env.local` 中 `VITE_API_BASE_URL` 留空即可。

### 3. 生产部署（可选）

```bash
# 构建前端静态资源
cd frontend
yarn build

# 预览构建结果
yarn preview
```

后端保持运行 `ACSEventConsole.exe`；前端可将 `frontend/dist/` 部署到任意静态文件服务器，并通过环境变量指定 API 地址（见下文）。

## 运行顺序

1. 启动后端 `dotnet run --project ACSEventConsole.csproj`
2. 启动前端 `yarn dev`
3. 打开浏览器访问 http://localhost:5173

## 配置说明

配置文件位于 `backend/config/` 目录。修改后需**重启后端**生效。

| 文件 | 是否纳入 Git | 说明 |
|------|-------------|------|
| `DeviceConfig.json` | 否（见 `.gitignore`） | 门禁设备、Web 端口、限员与 MQTT 等，含设备密码 |
| `EmployeeConfig.json` | 否（见 `.gitignore`） | 本地员工档案，含姓名、工号、部门等 |
| `DeviceConfig.json.example` | 是 | 设备配置模板 |
| `EmployeeConfig.json.example` | 是 | 员工配置模板 |

首次部署，从模板复制并编辑：

```bash
cp backend/config/DeviceConfig.json.example backend/config/DeviceConfig.json
cp backend/config/EmployeeConfig.json.example backend/config/EmployeeConfig.json
```

`DeviceConfig.json` 也可通过 Web 接口在线查看/编辑（`EmployeeConfig.json` 需手动编辑文件后重启）：

- `GET /config` — 查看当前设备配置
- `GET /config/edit` — 在线编辑设备配置

---

### DeviceConfig.json

门禁与系统运行参数，顶层分为 `config`（全局）和 `devices`（设备列表）。

> 该文件已加入 `.gitignore`，不会提交到仓库。含设备密码、MQTT 凭证等敏感信息，请在各环境自行维护。

#### config — 全局参数

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `apiBaseUrl` | string | — | 预留外部 API 根地址（当前未用于员工同步） |
| `webPort` | number | `8081` | 后端 Web 服务端口 |
| `limitCount` | number | `500` | 区域限制人数上限 |
| `stayWarningMinutes` | number | `30` | 停留超时报警（分钟） |
| `recentRecordCount` | number | `10` | 看板最近进出记录条数 |
| `exitGraceSeconds` | number | `8` | 出门宽限时间（秒） |
| `capacityWarningRatio` | number | `0.9` | 人数接近上限时的预警比例（0–1） |
| `alarmScanSeconds` | number | `5` | 报警扫描间隔（秒） |
| `mqttEnabled` | boolean | `true` | 是否启用门禁事件 MQTT |
| `mqttHost` / `mqttPort` | string / number | — | MQTT Broker 地址与端口 |
| `mqttTopic` | string | `acs/alarm/event` | 门禁事件主题 |
| `mqttClientId` | string | — | MQTT 客户端 ID |
| `mqttUsername` / `mqttPassword` | string | — | MQTT 认证（可选） |
| `personInfoMqttEnabled` | boolean | `true` | 是否发布刷脸信息 |
| `personInfoMqttHost` / `personInfoMqttPort` | string / number | — | 刷脸 MQTT 地址 |
| `personInfoMqttTopic` | string | `personinfo` | 刷脸默认主题 |
| `personInMqttTopic` | string | `person_in` | 进门事件主题 |
| `personOutMqttTopic` | string | `person_out` | 出门事件主题 |
| `areaAlertMqttEnabled` | boolean | `true` | 是否订阅区域报警 |
| `areaAlertMqttTopic` | string | `area_alert` | 区域报警主题 |
| `abnormalMqttEnabled` | boolean | `true` | 是否订阅异常消息 |
| `abnormalMqttTopic` | string | `abnormal` | 异常消息主题 |
| `peopleCountMqttEnabled` | boolean | `true` | 是否订阅人数统计 |
| `peopleCountMqttTopic` | string | `people_count` | 人数统计主题 |

各 MQTT 块均包含对应的 `*ClientId` 字段，用于区分客户端连接。

#### devices — 门禁设备

`devices` 为数组，每项代表一台门禁设备：

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `ip` | string | 是 | 设备 IP |
| `userName` | string | 是 | 登录用户名 |
| `password` | string | 是 | 登录密码 |
| `port` | number | 是 | SDK 端口，通常为 `8000` |
| `enabled` | boolean | 是 | 是否启用该设备 |
| `name` | string | — | 设备简称 |
| `deviceName` | string | — | 设备显示名称 |
| `deviceID` | string | — | 设备唯一 ID |
| `areaID` | string | — | 所属区域 ID |
| `remark` | string | — | 备注 |
| `direction` | string | — | 默认进出方向：`进` / `出` |
| `controlDoorNo` | number | — | 门控门号，默认 `1` |
| `doors` | array | — | 多门方向配置，项含 `doorNo`、`direction` 等 |

配置示例：

```json
{
  "config": {
    "webPort": 8081,
    "limitCount": 500,
    "stayWarningMinutes": 30,
    "recentRecordCount": 10,
    "exitGraceSeconds": 8,
    "capacityWarningRatio": 0.9,
    "mqttEnabled": true,
    "mqttHost": "192.168.0.12",
    "mqttPort": 1883
  },
  "devices": [
    {
      "ip": "192.168.0.164",
      "userName": "admin",
      "password": "your-password",
      "port": 8000,
      "enabled": true,
      "name": "门禁1",
      "deviceName": "测试门禁164",
      "direction": "进",
      "controlDoorNo": 1,
      "doors": []
    }
  ]
}
```

---

### EmployeeConfig.json

员工信息 JSON **数组**，启动时从本地加载，用于将门禁事件中的工号/卡号映射为姓名、部门等展示字段。

> 该文件已加入 `.gitignore`，不会提交到仓库。请在各环境自行维护，勿将含真实身份证、手机号等敏感信息的文件推送到 Git。

#### 常用字段

| 字段 | 说明 |
|------|------|
| `employeeId` | 员工主键，与门禁事件员工号匹配 |
| `employeeNo` / `workNo` | 工号别名，同样参与匹配 |
| `name` | 姓名（看板展示） |
| `card_id` | 卡号，参与匹配 |
| `department` | 部门 |
| `position` | 岗位 |
| `phone` | 电话 |
| `gender` | 性别 |
| `status` | 在职状态 |
| `type` | 人员类型（如内部职工、外协） |
| `permission` | 权限/区域 |
| `remarks` | 备注 |

后端会按 `employeeId`、`employeeNo`、`card_id`、`name` 等字段与刷脸事件关联。门禁设备返回的员工号需与表中 `employeeId` 或 `employeeNo` 一致，才能正确显示姓名。

配置示例：

```json
[
  {
    "employeeId": "10001",
    "employeeNo": "10001",
    "name": "张三",
    "card_id": "CARD001",
    "department": "技术部",
    "position": "工程师",
    "status": "在职"
  }
]
```

可通过 `GET /api/employee` 查看当前已加载的员工列表。

### 前端 API 地址

复制环境变量示例文件并按需修改：

```bash
cd frontend
cp .env.example .env.local
```

`.env.local` 示例：

```env
# 留空则开发模式走 Vite 代理；生产环境可填写完整后端地址
VITE_API_BASE_URL=http://192.168.0.29:8081
```

## 直接运行已编译版本（可选）

若无需修改后端代码，可跳过 `dotnet run`，直接运行已有产物：

```bash
cd backend/GetACSEvent/bin/Debug/net8.0
./ACSEventConsole.exe
```

## 主要 API 接口

| 接口 | 说明 |
|------|------|
| `GET /api/dashboard` | 人员看板聚合数据（前端主接口） |
| `GET /events` | 原始门禁事件列表 |
| `GET /config` | 当前设备配置 |
| `GET /config/edit` | 在线编辑配置 |
| `GET /images` | 事件关联图片 |
| `GET /health` | 健康检查 |

默认服务地址：`http://localhost:8081`

更详细的接口说明见 `backend/GetACSEvent/事件API说明.md`。

## 常见问题

**前端页面无数据**

- 确认后端 `ACSEventConsole.exe` 已启动且 `/health` 可访问
- 确认 `DeviceConfig.json` 中门禁设备 IP、账号配置正确，设备网络可达
- 开发模式下确认前端运行在 5173 端口（Vite 代理依赖此端口）

**端口冲突**

- 后端端口：修改 `DeviceConfig.json` 中 `config.webPort`
- 前端端口：修改 `frontend/vite.config.ts` 中 `server.port`

**MQTT 相关功能不可用**

- 检查 `DeviceConfig.json` 的 `config` 中 MQTT 主机地址与端口
- 若暂不需要 MQTT，可将对应 `*MqttEnabled` 设为 `false`

## 相关文档

- `backend/README.md` — 后端独立项目说明
- `backend/GetACSEvent/事件API说明.md` — 事件 API 详细文档
- `backend/GetACSEvent/图片服务器使用说明.md` — 图片服务说明
