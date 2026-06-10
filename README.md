# 人员计数

基于海康门禁 SDK 的人员进出监控与看板系统。后端通过 **SDK 实时布防**（`NET_DVR_SetupAlarmChan_V41`）采集门禁刷脸事件，经异步队列处理后对外提供 HTTP API 与 **SSE 推送**；前端 Vue 看板实时展示在场人数、进出记录、报警与异常信息。

## 门禁设备

![03a8e705-e186-4b20-bd27-1a1c2f742bc0](./03a8e705-e186-4b20-bd27-1a1c2f742bc0-1780920054550-4.png)  

## 效果图

![image-20260608194803483](./image-20260608194803483.png) 

## 项目结构

```
├── backend/                 # 后端（ASP.NET Core 8）
│   ├── config/              # 本地配置（*.json 不纳入 Git，见 *.example 模板）
│   ├── scripts/             # 运维脚本（如从设备同步员工配置）
│   ├── ACSEventConsole.sln  # 解决方案入口
│   └── src/
│       └── ACSEventConsole/ # Web API 主项目
│           ├── Program.cs
│           ├── Controllers/
│           ├── Services/
│           ├── Infrastructure/
│           └── 启动服务.bat
├── deploy/                  # 生产打包脚本与 IIS 配置
│   ├── publish.bat          # 全量打包（前端 + 后端）
│   ├── publish-backend.bat  # 仅打包后端
│   ├── publish-frontend.bat # 仅打包前端
│   ├── iis/web.config       # IIS SPA 路由配置
│   └── output/              # 打包输出（不纳入 Git）
└── frontend/                # 前端看板（Vue 3 + Vite + TypeScript）
    ├── src/
    └── dist/                # 生产构建产物
```

## 环境要求

| 组件       | 要求                                                |
| ---------- | --------------------------------------------------- |
| 操作系统   | Windows x64                                         |
| 后端运行时 | .NET 8                                              |
| 后端 SDK   | .NET SDK 8+                                         |
| 前端运行时 | Node.js 18+（推荐 20+）                             |
| 包管理器   | Yarn 1.x                                            |
| Python     | 3.8+（可选，用于从设备同步 `EmployeeConfig.json`）  |
| 网络       | 能访问门禁设备 IP；如需 MQTT 功能需连接 MQTT Broker |

## 快速开始（开发模式）

### 1. 准备配置并同步员工（推荐）

首次运行前，从模板复制配置文件并按实际环境修改：

```bash
cp backend/config/DeviceConfig.json.example backend/config/DeviceConfig.json
cp backend/config/EmployeeConfig.json.example backend/config/EmployeeConfig.json
```

编辑 `DeviceConfig.json` 填入门禁设备 IP、账号、密码后，从设备拉取人员列表写入 `EmployeeConfig.json`：

```bash
cd backend/scripts
python sync_employee_config.py
```

或在 Windows 下双击 `backend/scripts/同步员工配置.bat`。

也可使用一键启动（先同步员工，再启动后端）：

```bash
cd backend/src/ACSEventConsole
启动服务.bat
```

### 2. 启动后端

```bash
cd backend/src/ACSEventConsole
dotnet run
```

启动成功后，后端默认监听 **8081** 端口。可通过浏览器访问健康检查接口验证：

```
http://localhost:8081/health
```

### 3. 启动前端

新开一个终端：

```bash
cd frontend
yarn
yarn dev
```

浏览器打开 **http://localhost:5173** 即可查看看板。

Vite 开发服务器会将 `/api`、`/events`、`/images`、`/config`、`/health` 等请求代理到 `http://localhost:8081`（含 SSE 接口 `/api/dashboard/stream`），`.env.local` 中 `VITE_API_BASE_URL` 留空即可。

## 生产部署（Windows Server + IIS）

推荐架构：**IIS 托管前端静态页，后端独立进程运行**。后端需 7×24 连接海康 SDK 接收刷脸回调，不适合放入 IIS 应用池（应用池回收会断开 SDK 布防通道）。

```
浏览器
  ├─ http://服务器:80/           → IIS（前端静态资源）
  └─ http://服务器:8081/api/...  → ACSEventConsole.exe（Windows 服务）
                                       ↓ SDK
                                  门禁设备 :8000
```

### 服务器环境

| 组件 | 说明 |
| ---- | ---- |
| Windows Server x64 | 部署目标 |
| [.NET 8 Runtime x64](https://dotnet.microsoft.com/download/dotnet/8.0) | 运行后端 exe |
| [Visual C++ 2015–2022 可再发行组件 x64](https://learn.microsoft.com/zh-cn/cpp/windows/latest-supported-vc-redist) | 海康 SDK 依赖 |
| IIS + [URL Rewrite](https://www.iis.net/downloads/microsoft/url-rewrite) | 托管前端（**必须安装** URL Rewrite） |
| Node.js + Yarn | 仅在**构建机**打包前端时需要 |

### 打包

在开发机项目根目录执行（或双击 bat 文件）：

```bat
deploy\publish.bat            :: 全量打包（推荐）
deploy\publish-backend.bat    :: 仅后端
deploy\publish-frontend.bat   :: 仅前端
```

输出目录 `deploy/output/`：

```
deploy/output/
├── backend/
│   ├── service/     ← ACSEventConsole.exe + HCNetSDK.dll 等 SDK 依赖
│   ├── config/      ← DeviceConfig.json / EmployeeConfig.json
│   └── scripts/     ← 员工同步等运维脚本
└── frontend/        ← Vue 静态资源 + web.config（IIS 网站根目录）
```

打包脚本会自动：

- `dotnet publish` 发布后端（Release / win-x64）
- 复制 `Runtime/x64/` 下全部海康 SDK DLL 到 `service/`（与 exe 同目录）
- 复制配置模板；若本地已有 `backend/config/*.json` 会一并带入
- 构建前端并附带 `deploy/iis/web.config`

生产环境 API 地址：打包前可在 `frontend/.env.production` 中设置：

```env
VITE_API_BASE_URL=http://192.168.0.7:8081
```

留空则前端默认访问 **同主机名的 8081 端口**。

### 拷贝到服务器

建议目录（示例 `D:\person-count\`）：

```
D:\person-count\
├── backend\
│   ├── service\          ← publish 输出的 exe 和 DLL
│   ├── config\           ← 编辑 DeviceConfig.json
│   └── scripts\
└── frontend\             ← IIS 网站根目录
```

> **必须保留 `backend\config` 目录层级**，后端启动时会向上查找名为 `backend` 的目录来定位配置。

### 启动后端

1. 编辑 `backend\config\DeviceConfig.json`（设备 IP、账号、`webPort` 等）
2. （推荐）运行 `backend\scripts\同步员工配置.bat` 生成员工表
3. 启动服务：

```bat
cd /d D:\person-count\backend\service
ACSEventConsole.exe
```

生产环境建议注册为 **Windows 服务**（如 [NSSM](https://nssm.cc/)）或「任务计划程序 → 系统启动时运行」，避免登录注销后进程退出。

验证后端：

```
http://localhost:8081/health
http://localhost:8081/swagger
```

### IIS 配置前端

1. 安装 **URL Rewrite** 模块，执行 `iisreset` 重启 IIS
2. **应用程序池** → 添加 → 名称 `ACSDashboard`，**.NET CLR 版本 = 无托管代码**
3. **网站** → 添加 → 物理路径指向 `D:\person-count\frontend`，端口 **80**（或自定义如 3000）
4. 确认 `frontend\web.config` 存在（打包时已自动复制）
5. 给 `IIS_IUSRS` 和 `IIS AppPool\ACSDashboard` 授予 frontend 目录**读取**权限
6. 防火墙放行 **80**（前端）及 **8081**（后端 API，浏览器需能访问）

访问 `http://服务器IP/` 查看看板；F12 → Network 中应看到对 `:8081/api/dashboard/stream` 的 SSE 长连接。

### 部署验证清单

| 检查项 | 方法 |
| ------ | ---- |
| 后端健康 | `http://localhost:8081/health` |
| SDK 加载 | `service\` 下存在 `HCNetSDK.dll` 和 `HCNetSDKCom\` 目录 |
| SSE 推送 | 浏览器访问 `/api/dashboard/stream`，刷脸后看板即时更新 |
| 前端页面 | `http://服务器IP/` 正常显示，无 500 错误 |
| 员工姓名 | 已同步 `EmployeeConfig.json` 且重启过后端 |

### 部署常见问题

**后端启动报 `Unable to load DLL '.\HCNetSDK.dll'`**

- `service\` 目录缺少海康 SDK 文件。确认存在 `HCNetSDK.dll`、`HCCore.dll`、`HCNetSDKCom\` 等
- 重新运行 `deploy\publish-backend.bat` 打包，或手动将 `backend\src\ACSEventConsole\Runtime\x64\*` 复制到 `service\`
- 若 DLL 齐全仍报错，安装 **Visual C++ 2015–2022 x64 可再发行组件**

**IIS 报 HTTP 500.52，无法识别 `logicalAnd`**

- 服务器 URL Rewrite 版本较旧（2.0）。项目 `web.config` 已兼容，确认 `<conditions>` 标签**不含** `logicalAnd` 属性
- 重新打包前端或手动更新服务器上 `frontend\web.config`

**IIS 报 500.19（rewrite 相关）**

- 未安装 URL Rewrite 模块

**看板无数据**

- 后端未启动，或浏览器无法访问 **8081** 端口（防火墙拦截）
- 检查 `DeviceConfig.json` 设备配置与网络连通性

**更新部署**

| 更新内容 | 操作 |
| -------- | ---- |
| 前端 | `publish-frontend.bat` → 覆盖 `frontend\` |
| 后端 | `publish-backend.bat` → 覆盖 `service\` → **重启后端进程** |
| 配置 | 编辑 `backend\config\*.json` → **重启后端** |

## 运行顺序

1. （推荐）运行 `backend/scripts/sync_employee_config.py` 从设备同步员工
2. 启动后端 `dotnet run`（或使用 `backend/src/ACSEventConsole/启动服务.bat` 自动完成第 1、2 步）
3. 启动前端 `yarn dev`
4. 打开浏览器访问 http://localhost:5173

## 配置说明

配置文件位于 `backend/config/` 目录。修改后需**重启后端**生效。

| 文件                          | 是否纳入 Git          | 说明                                           |
| ----------------------------- | --------------------- | ---------------------------------------------- |
| `DeviceConfig.json`           | 否（见 `.gitignore`） | 门禁设备、Web 端口、限员与 MQTT 等，含设备密码 |
| `EmployeeConfig.json`         | 否（见 `.gitignore`） | 本地员工档案；可用脚本从设备同步生成         |
| `DeviceConfig.json.example`   | 是                    | 设备配置模板                                   |
| `EmployeeConfig.json.example` | 是                    | 员工配置模板                                   |

首次部署，从模板复制并编辑：

```bash
cp backend/config/DeviceConfig.json.example backend/config/DeviceConfig.json
cp backend/config/EmployeeConfig.json.example backend/config/EmployeeConfig.json
```

`DeviceConfig.json` 也可通过 Web 接口在线查看/编辑；`EmployeeConfig.json` 可通过同步脚本自动生成，手动修改后需重启后端：

- `GET /config` — 查看当前设备配置
- `GET /config/edit` — 在线编辑设备配置

---

### DeviceConfig.json

门禁与系统运行参数，顶层分为 `config`（全局）和 `devices`（设备列表）。

> 该文件已加入 `.gitignore`，不会提交到仓库。含设备密码、MQTT 凭证等敏感信息，请在各环境自行维护。

#### config — 全局参数

| 字段                                        | 类型            | 默认值            | 说明                                      |
| ------------------------------------------- | --------------- | ----------------- | ----------------------------------------- |
| `apiBaseUrl`                                | string          | —                 | 预留外部 API 根地址（当前未用于员工同步） |
| `webPort`                                   | number          | `8081`            | 后端 Web 服务端口                         |
| `limitCount`                                | number          | `500`             | 区域限制人数上限                          |
| `stayWarningMinutes`                        | number          | `30`              | 停留超时报警（分钟）                      |
| `recentRecordCount`                         | number          | `10`              | 看板最近进出记录条数                      |
| `exitGraceSeconds`                          | number          | `8`               | 出门宽限时间（秒）                        |
| `capacityWarningRatio`                      | number          | `0.9`             | 人数接近上限时的预警比例（0–1）           |
| `alarmScanSeconds`                          | number          | `5`               | 报警扫描兜底间隔（秒）；刷脸事件会**立即触发**报警扫描，该字段仅作定时兜底 |
| `mqttEnabled`                               | boolean         | `true`            | 是否启用门禁事件 MQTT                     |
| `mqttHost` / `mqttPort`                     | string / number | —                 | MQTT Broker 地址与端口                    |
| `mqttTopic`                                 | string          | `acs/alarm/event` | 门禁事件主题                              |
| `mqttClientId`                              | string          | —                 | MQTT 客户端 ID                            |
| `mqttUsername` / `mqttPassword`             | string          | —                 | MQTT 认证（可选）                         |
| `personInfoMqttEnabled`                     | boolean         | `true`            | 是否发布刷脸信息                          |
| `personInfoMqttHost` / `personInfoMqttPort` | string / number | —                 | 刷脸 MQTT 地址                            |
| `personInfoMqttTopic`                       | string          | `personinfo`      | 刷脸默认主题                              |
| `personInMqttTopic`                         | string          | `person_in`       | 进门事件主题                              |
| `personOutMqttTopic`                        | string          | `person_out`      | 出门事件主题                              |
| `areaAlertMqttEnabled`                      | boolean         | `true`            | 是否订阅区域报警                          |
| `areaAlertMqttTopic`                        | string          | `area_alert`      | 区域报警主题                              |
| `abnormalMqttEnabled`                       | boolean         | `true`            | 是否订阅异常消息                          |
| `abnormalMqttTopic`                         | string          | `abnormal`        | 异常消息主题                              |
| `peopleCountMqttEnabled`                    | boolean         | `true`            | 是否订阅人数统计                          |
| `peopleCountMqttTopic`                      | string          | `people_count`    | 人数统计主题                              |

各 MQTT 块均包含对应的 `*ClientId` 字段，用于区分客户端连接。

#### devices — 门禁设备

`devices` 为数组，每项代表一台门禁设备：

| 字段            | 类型    | 必填 | 说明                                        |
| --------------- | ------- | ---- | ------------------------------------------- |
| `ip`            | string  | 是   | 设备 IP                                     |
| `userName`      | string  | 是   | 登录用户名                                  |
| `password`      | string  | 是   | 登录密码                                    |
| `port`          | number  | 是   | **SDK 端口**，后端 C# 登录设备用，通常为 `8000` |
| `httpPort`      | number  | —    | **ISAPI/HTTP 端口**，员工同步脚本用，默认 `80`  |
| `enabled`       | boolean | 是   | 是否启用该设备                              |
| `name`          | string  | —    | 设备简称                                    |
| `deviceName`    | string  | —    | 设备显示名称                                |
| `deviceID`      | string  | —    | 设备唯一 ID                                 |
| `areaID`        | string  | —    | 所属区域 ID                                 |
| `remark`        | string  | —    | 备注                                        |
| `direction`     | string  | —    | 默认进出方向：`进` / `出`                   |
| `controlDoorNo` | number  | —    | 门控门号，默认 `1`                          |
| `doors`         | array   | —    | 多门方向配置，项含 `doorNo`、`direction` 等 |

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

可通过脚本从门禁设备批量拉取人员并自动生成该文件（见下文「从设备同步员工配置」）。

#### 常用字段

| 字段                    | 说明                           |
| ----------------------- | ------------------------------ |
| `employeeId`            | 员工主键，与门禁事件员工号匹配 |
| `employeeNo` / `workNo` | 工号别名，同样参与匹配         |
| `name`                  | 姓名（看板展示）               |
| `card_id`               | 卡号，参与匹配                 |
| `department`            | 部门                           |
| `position`              | 岗位                           |
| `phone`                 | 电话                           |
| `gender`                | 性别                           |
| `status`                | 在职状态                       |
| `type`                  | 人员类型（如内部职工、外协）   |
| `permission`            | 权限/区域                      |
| `remarks`               | 备注                           |

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

---

### 从设备同步员工配置

脚本 `backend/scripts/sync_employee_config.py` 通过海康 ISAPI 接口批量查询设备上全部人员（等价于 SDK 文档中的 `NET_DVR_GetPersonList` 使用场景），写入 `backend/config/EmployeeConfig.json`。

#### 用法

```bash
cd backend/scripts

# 默认读取 backend/config/DeviceConfig.json，输出到 EmployeeConfig.json
python sync_employee_config.py

# 指定配置文件
python sync_employee_config.py --device-config ../config/DeviceConfig.json

# 设备 ISAPI 走 HTTPS（默认 443 端口）
python sync_employee_config.py --https
```

Windows 也可双击 `backend/scripts/同步员工配置.bat`，或使用 `backend/src/ACSEventConsole/启动服务.bat` 在启动后端前自动同步。

#### 端口说明

门禁设备存在两套端口，用途不同：

| 用途 | 默认端口 | 配置字段 | 说明 |
| ---- | -------- | -------- | ---- |
| 后端门禁事件（C# SDK） | `8000` | `devices[].port` | `ACSEventConsole` 登录设备、`SetupAlarmChan_V41` 实时布防接收刷脸事件 |
| 员工同步 / 历史补查（ISAPI/HTTP） | `80` | `devices[].httpPort`（可选） | 调用 `UserInfo/Search`、`AcsEvent` 等 ISAPI 接口 |

脚本**不会**读取 `port: 8000`，未配置 `httpPort` 时默认访问 `http://设备IP:80`。

若设备 Web/ISAPI 不在 80 端口，在 `DeviceConfig.json` 对应设备下增加：

```json
"httpPort": 443
```

并视情况加上 `--https` 参数。

#### 调用的接口

```
POST http://<设备IP>:<httpPort>/ISAPI/AccessControl/UserInfo/Search?format=json
Content-Type: application/json

{
  "UserInfoSearchCond": {
    "searchID": "<随机ID>",
    "searchResultPosition": 0,
    "maxResults": 30
  }
}
```

脚本会分页循环请求，直到取完所有人员；多台设备的结果按 `employeeId` 去重合并。

#### Digest 认证说明

ISAPI 接口使用 **HTTP Digest 认证**，不是单独申请的密钥，而是设备 Web 登录同一套账号密码（即 `DeviceConfig.json` 中的 `userName` / `password`）。

流程简述：

1. 客户端发送 POST 请求
2. 设备返回 `401`，响应头携带 `realm`、`nonce` 等挑战信息
3. 客户端用 **用户名 + 密码 + 挑战信息** 计算出 Digest 值，放入 `Authorization: Digest ...` 请求头后重试
4. 验证通过后返回人员 JSON

脚本内部通过 Python 的 `HTTPDigestAuthHandler` 自动完成上述握手，**无需手动获取或拼接 Digest 字符串**。

#### 手动验证（curl）

```bash
curl -X POST "http://192.168.0.164/ISAPI/AccessControl/UserInfo/Search?format=json" \
  -u admin:你的密码 \
  -H "Content-Type: application/json" \
  -d "{\"UserInfoSearchCond\":{\"searchID\":\"1\",\"searchResultPosition\":0,\"maxResults\":30}}"
```

`-u admin:密码` 会触发 Digest 认证；curl 会自动处理 401 挑战。

#### 为什么浏览器直接打开 URL 会报错

在浏览器地址栏访问：

```
http://192.168.0.164/ISAPI/AccessControl/UserInfo/Search?format=json
```

等价于发送 **GET** 请求，而该接口要求 **POST + JSON 请求体 + Digest 认证**，因此设备会返回：

```json
{
  "statusCode": 4,
  "statusString": "Invalid Operation",
  "subStatusCode": "methodNotAllowed",
  "errorCode": 1073741828,
  "errorMsg": "methodNotAllowed"
}
```

这表示 **HTTP 方法不允许**（用了 GET 而非 POST），并非接口损坏。请使用脚本或 curl 按 POST 方式调用。

---

### 前端 API 地址

复制环境变量示例文件并按需修改：

```bash
cd frontend
cp .env.example .env.local
```

`.env.local` 示例：

```env
# 留空：开发模式走 Vite 代理；生产默认同主机名 :8081
# 填写完整地址：VITE_API_BASE_URL=http://192.168.0.29:8081
VITE_API_BASE_URL=
```

## 实时数据架构

系统采用「设备推送 → 后端异步处理 → SSE 推送看板」链路，避免轮询带来的延迟。

### 数据流

```
设备刷脸
  → SDK 回调（NET_DVR_SetupAlarmChan_V41，毫秒级）
  → AcsEventProcessingQueue（拷贝数据后立即返回，后台写盘 / MQTT / 控门）
  → EventStore（Revision + Changed 事件）
  → GET /api/dashboard/stream（SSE 推送给前端）
  → 看板即时更新
```

若 SSE 连接失败，前端自动降级为每 10 秒轮询 `GET /api/dashboard`。

### 后端优化要点

| 模块 | 说明 |
| ---- | ---- |
| `AcsEventProcessingQueue` | SDK 回调线程只做 Marshal 拷贝与入队；写图片、MQTT、EventStore、限员控门等重活放到后台线程 |
| `ACSEventMultiDeviceService` | 全局报警回调按设备 IP **只分发一次**，避免多设备重复处理 |
| `MqttConnectionPool` | 按 `host:port:clientId` 复用 MQTT TCP 长连接，避免每条刷脸事件新建连接 |
| `EventStore` | 维护 `Revision` 与 `Changed` 事件；新增事件或更新图片 URL 时通知下游 |
| `AlarmMonitorService` | 刷脸后**立即**扫描并发布报警 MQTT；定时兜底扫描约 60 秒 |
| `CapacityDoorControlService` | 事件触发限员评估；15 秒定时兜底（原 1.5 秒轮询） |

### 海康接口分工

| 通道 | 端口 | 用途 |
| ---- | ---- | ---- |
| **HCNetSDK** | `8000`（`devices[].port`） | 登录、实时布防、远程控门 |
| **ISAPI (HTTP)** | `80/443`（`devices[].httpPort`） | 人员同步、历史事件查询、抓拍图下载 |

常用 ISAPI 路径（本项目已使用或可用于扩展）：

- `POST /ISAPI/AccessControl/UserInfo/Search` — 人员列表（员工同步脚本）
- `POST /ISAPI/AccessControl/AcsEvent` — 历史门禁事件
- `GET /ISAPI/AccessControl/Event/picture` — 事件抓拍图

### 验证实时推送

```bash
# 1. 启动后端与前端（见「快速开始」）

# 2. 浏览器直接查看 SSE 流
http://localhost:8081/api/dashboard/stream

# 3. 触发刷脸后，看板应在百毫秒级更新（无需等待 3 秒轮询）
```

---

## 主要 API 接口

| 接口                          | 说明                                           |
| ----------------------------- | ---------------------------------------------- |
| `GET /api/dashboard`          | 人员看板聚合数据（一次性拉取）                 |
| `GET /api/dashboard/stream`   | 看板 SSE 实时推送（前端默认使用）              |
| `GET /events`                 | 原始门禁事件列表                               |
| `GET /config`                 | 当前设备配置                                   |
| `GET /config/edit`            | 在线编辑配置                                   |
| `GET /images`                 | 事件关联图片                                   |
| `GET /health`                 | 健康检查                                       |

默认服务地址：`http://localhost:8081`

更详细的接口说明见 `backend/GetACSEvent/事件API说明.md`。

## 常见问题

**看板更新延迟较大**

- 确认前端已连接 SSE：浏览器开发者工具 Network 中应存在 `/api/dashboard/stream` 长连接
- 若 SSE 失败会自动降级为 10 秒轮询；检查后端 `/health` 与 `webPort` 是否可达
- 设备到后端延迟通常毫秒级；若仅看板慢，多为前端未连上 SSE 或网络代理未转发流式响应

**前端页面无数据**

- 确认后端 `ACSEventConsole.exe` 已启动且 `/health` 可访问
- 确认 `DeviceConfig.json` 中门禁设备 IP、账号配置正确，设备网络可达
- 开发模式下确认前端运行在 5173 端口（Vite 代理依赖此端口）
- **生产部署**：确认浏览器能访问后端 **8081** 端口（IIS 只托管前端，API 仍走 8081）

**后端启动失败 / HCNetSDK.dll 找不到**

- 见上文「生产部署 → 部署常见问题」

**IIS 前端 500 错误**

- 500.52：URL Rewrite 版本过旧，更新 `web.config` 或升级 URL Rewrite 模块
- 500.19：未安装 URL Rewrite 模块

**端口冲突**

- 后端端口：修改 `DeviceConfig.json` 中 `config.webPort`
- 前端端口：修改 `frontend/vite.config.ts` 中 `server.port`

**MQTT 相关功能不可用**

- 检查 `DeviceConfig.json` 的 `config` 中 MQTT 主机地址与端口
- 若暂不需要 MQTT，可将对应 `*MqttEnabled` 设为 `false`

**看板显示工号而非姓名**

- 确认已运行 `backend/scripts/sync_employee_config.py` 生成 `EmployeeConfig.json`
- 确认刷脸事件中的员工号与 `EmployeeConfig.json` 中 `employeeId` / `employeeNo` 一致
- 修改 `EmployeeConfig.json` 后需重启后端

**员工同步脚本失败**

- 确认设备 IP 可达，且 `userName` / `password` 与设备 Web 登录一致
- 员工同步走 ISAPI **HTTP 80**（`httpPort`），不是 SDK 的 `port: 8000`
- 若 ISAPI 在其它端口，在设备配置中设置 `httpPort`，HTTPS 设备加 `--https`

**浏览器访问 ISAPI 返回 `methodNotAllowed`**

- 地址栏访问等价于 GET，该接口需 **POST + JSON + Digest 认证**
- 请使用 `sync_employee_config.py` 或 curl（见上文「从设备同步员工配置」）

## 相关文档

- `backend/README.md` — 后端独立项目说明
- `deploy/publish.bat` — 生产打包入口脚本
- `deploy/iis/web.config` — IIS 前端 SPA 路由配置
- `backend/scripts/sync_employee_config.py` — 从设备同步员工配置脚本
- `backend/GetACSEvent/事件API说明.md` — 事件 API 详细文档
- `backend/GetACSEvent/图片服务器使用说明.md` — 图片服务说明