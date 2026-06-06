# 事件API说明

## 概述

事件API提供了门禁事件的实时数据访问功能，返回JSON格式的事件列表，包含完整的事件信息和对应的图片URL。

## API端点

### 事件列表API
- **URL**: `http://localhost:8080/events`
- **方法**: GET
- **返回格式**: JSON
- **描述**: 获取所有门禁事件的列表

## 返回数据格式

### 事件对象结构
```json
{
  "time": "2024-01-15 14:30:25",
  "timeUtc": "2024-01-15T06:30:25.0000000Z",
  "deviceIP": "192.168.1.100",
  "deviceName": "前门门禁",
  "deviceID": "DEV001",
  "areaID": "AREA001",
  "remark": "前门入口",
  "majorType": "事件",
  "minorType": "未知事件类型(75)",
  "cardNo": "12345678",
  "employeeNo": "EMP001",
  "personName": "张三",
  "cardType": "普通卡",
  "doorNo": 1,
  "imageUrl": "http://192.168.1.50:8080/images/%E5%89%8D%E9%97%A8%E9%97%A8%E7%A6%81/%E5%BC%A0%E4%B8%89/%E5%BC%A0%E4%B8%89_20240115_143025.jpg"
}
```

### 字段说明

| 字段名 | 类型 | 说明 |
|--------|------|------|
| time | string | 事件时间（设备原始时间，格式：yyyy-MM-dd HH:mm:ss） |
| timeUtc | string | UTC时间（ISO 8601格式） |
| deviceIP | string | 设备IP地址 |
| deviceName | string | 设备名称 |
| deviceID | string | 设备ID |
| areaID | string | 区域ID |
| remark | string | 设备备注 |
| majorType | string | 主事件类型（报警/异常/操作/事件） |
| minorType | string | 次事件类型（具体事件描述） |
| cardNo | string | 卡号 |
| employeeNo | string | 员工号 |
| personName | string | 员工姓名 |
| cardType | string | 卡类型 |
| doorNo | number | 门编号 |
| imageUrl | string | 图片URL（如果有图片） |

## 时间格式说明

### 统一时间格式
- **time字段**: 使用设备原始时间，格式为 `yyyy-MM-dd HH:mm:ss`（与控制台输出保持一致）
- **timeUtc字段**: 使用UTC时间，ISO 8601格式
- **时间来源**: time字段直接从门禁设备获取，无需时区转换

### 示例
```json
{
  "time": "2024-01-15 14:30:25",
  "timeUtc": "2024-01-15T06:30:25.0000000Z"
}
```

## 图片URL说明

### URL格式
图片URL会自动将localhost替换为本机IP地址，确保外部访问正常：

```
原始URL: http://localhost:8080/images/前门门禁/张三/张三_20240115_143025.jpg
替换后: http://192.168.1.50:8080/images/前门门禁/张三/张三_20240115_143025.jpg
```

### URL编码
中文路径会自动进行URL编码：

```
中文路径: /images/前门门禁/张三/张三_20240115_143025.jpg
编码路径: /images/%E5%89%8D%E9%97%A8%E9%97%A8%E7%A6%81/%E5%BC%A0%E4%B8%89/%E5%BC%A0%E4%B8%89_20240115_143025.jpg
```

## 使用示例

### 1. 获取事件列表
```bash
curl http://localhost:8080/events
```

### 2. 使用JavaScript获取
```javascript
fetch('http://localhost:8080/events')
  .then(response => response.json())
  .then(events => {
    events.forEach(event => {
      console.log(`时间: ${event.time}`);
      console.log(`设备: ${event.deviceName}`);
      console.log(`人员: ${event.personName}`);
      if (event.imageUrl) {
        console.log(`图片: ${event.imageUrl}`);
      }
    });
  });
```

### 3. 使用Python获取
```python
import requests
import json

response = requests.get('http://localhost:8080/events')
events = response.json()

for event in events:
    print(f"时间: {event['time']}")
    print(f"设备: {event['deviceName']}")
    print(f"人员: {event['personName']}")
    if event.get('imageUrl'):
        print(f"图片: {event['imageUrl']}")
    print("---")
```

## 数据排序

- 事件按时间倒序排列（最新的在最前面）
- 最多保存1000条事件记录
- 超过限制时自动删除最旧的事件

## 错误处理

### 常见错误码
- **200**: 成功
- **404**: 服务未启动或路径错误
- **500**: 服务器内部错误

### 错误响应格式
```json
{
  "error": "错误描述"
}
```

## 性能说明

- API响应时间通常在100ms以内
- 支持并发访问
- 数据实时更新（有新事件时立即可用）

## 安全特性

- 只返回门禁事件数据
- 不包含敏感信息
- 支持跨域访问（CORS）

## 测试方法

### 1. 使用测试脚本
运行 `测试事件API.bat` 脚本进行自动化测试。

### 2. 浏览器测试
直接在浏览器中访问：`http://localhost:8080/events`

### 3. 命令行测试
```bash
# 获取事件列表
curl http://localhost:8080/events

# 检查响应头
curl -I http://localhost:8080/events
```

## 注意事项

1. **数据实时性**: 事件数据是实时更新的，每次请求都会返回最新数据
2. **数据持久性**: 事件数据存储在内存中，程序重启后会清空
3. **图片URL**: 只有保存了图片的事件才会有imageUrl字段
4. **IP地址**: 图片URL中的IP地址会自动获取本机IP，确保外部访问正常
5. **中文支持**: 完全支持中文设备名称和员工姓名

## 技术实现

### 1. 事件存储
```csharp
public class AcsEvent
{
    public DateTime TimeUtc { get; set; }
    public string DeviceIP { get; set; }
    public string DeviceName { get; set; }
    // ... 其他字段
    public string ImageUrl { get; set; }
}
```

### 2. 时间处理
```csharp
// 使用设备原始时间，与控制台输出保持一致
string time = string.Format("{0:D4}-{1:D2}-{2:D2} {3:D2}:{4:D2}:{5:D2}", 
    struAcsAlarmInfo.struTime.dwYear, 
    struAcsAlarmInfo.struTime.dwMonth, 
    struAcsAlarmInfo.struTime.dwDay, 
    struAcsAlarmInfo.struTime.dwHour, 
    struAcsAlarmInfo.struTime.dwMinute, 
    struAcsAlarmInfo.struTime.dwSecond);
```

### 3. IP地址替换
```csharp
// 将localhost替换为本机IP
string imageUrlWithIP = ReplaceLocalhostWithIP(e.ImageUrl);
```

通过以上实现，事件API提供了完整、实时的门禁事件数据访问功能。 