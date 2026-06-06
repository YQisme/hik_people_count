GetACSEvent 独立项目

目录
- 后端独立项目: D:\soft\personnumber\acs-event-standalone
- 前端看板项目: D:\soft\personnumber\newdemo

这次整理完成的内容
- 已将 SDK 示例目录中的 GetACSEvent 提炼为独立可编译项目
- 新增 `/api/dashboard` 聚合接口，供 newdemo 直接消费
- 保留原有 `/events`、`/config`、`/config/edit`、`/images` 能力
- 已为 Web 接口加入 CORS，前端开发模式可直接访问
- 前端 newdemo 已改成真实读取后端接口，不再依赖静态假数据

后端编译结果
- 已通过 x64 Debug 编译
- 输出程序: D:\soft\personnumber\acs-event-standalone\GetACSEvent\bin\x64\Debug\ACSEventConsole.exe

前端构建结果
- 已通过 `npm run build`

newdemo 对接地址
- 默认接口地址: http://localhost:8081/api/dashboard
- 如需修改，在 newdemo 目录配置 `VITE_API_BASE_URL`

建议运行顺序
1. 先启动 `ACSEventConsole.exe`
2. 再在 `D:\soft\personnumber\newdemo` 下执行 `npm run dev`
3. 打开前端页面查看联动效果

后端新增接口
- `GET /api/dashboard`: 人员看板聚合数据
- `GET /events`: 原始门禁事件列表
- `GET /config`: 当前设备配置
- `GET /config/edit`: 在线编辑配置
- `GET /images`: 事件图片列表
- `GET /health`: 健康检查

可选配置
在 `backend/config/DeviceConfig.json` 的 `config` 节点下可增加：
```xml
<LimitCount>500</LimitCount>
<StayWarningMinutes>30</StayWarningMinutes>
<RecentRecordCount>10</RecentRecordCount>
<CapacityWarningRatio>0.9</CapacityWarningRatio>
```

员工信息来源
- 启动时仍会从 `[ApiBaseUrl]/api/employee` 拉取员工数据
- 已新增员工目录匹配层，会尽量用 `employeeId` / `employeeNo` / `cardNo` / `name` 关联人员信息

说明
- 当前停留人员和报警是基于最近门禁事件在内存中聚合计算的
- 如果门方向配置不完整，系统会尽量按员工号/卡号事件推断“进场记录”


