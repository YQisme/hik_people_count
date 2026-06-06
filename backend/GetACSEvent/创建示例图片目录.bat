@echo off
chcp 65001 >nul
echo ========================================
echo 创建示例图片目录结构（新格式）
echo ========================================
echo.

echo 1. 创建基础图片目录...
if not exist "D:\Picture" (
    mkdir "D:\Picture"
    echo [√] 创建目录: D:\Picture
) else (
    echo [√] 目录已存在: D:\Picture
)

echo.
echo 2. 创建示例设备目录...
if not exist "D:\Picture\前门门禁" (
    mkdir "D:\Picture\前门门禁"
    echo [√] 创建设备目录: D:\Picture\前门门禁
) else (
    echo [√] 设备目录已存在: D:\Picture\前门门禁
)

if not exist "D:\Picture\后门门禁" (
    mkdir "D:\Picture\后门门禁"
    echo [√] 创建设备目录: D:\Picture\后门门禁
) else (
    echo [√] 设备目录已存在: D:\Picture\后门门禁
)

echo.
echo 3. 创建员工目录...
if not exist "D:\Picture\前门门禁\张三" (
    mkdir "D:\Picture\前门门禁\张三"
    echo [√] 创建员工目录: D:\Picture\前门门禁\张三
) else (
    echo [√] 员工目录已存在: D:\Picture\前门门禁\张三
)

if not exist "D:\Picture\前门门禁\李四" (
    mkdir "D:\Picture\前门门禁\李四"
    echo [√] 创建员工目录: D:\Picture\前门门禁\李四
) else (
    echo [√] 员工目录已存在: D:\Picture\前门门禁\李四
)

if not exist "D:\Picture\后门门禁\王五" (
    mkdir "D:\Picture\后门门禁\王五"
    echo [√] 创建员工目录: D:\Picture\后门门禁\王五
) else (
    echo [√] 员工目录已存在: D:\Picture\后门门禁\王五
)

echo.
echo 4. 创建示例图片文件...
echo 正在创建示例图片文件...

REM 创建示例图片文件（使用echo命令创建简单的文本文件作为示例）
echo 这是张三的示例图片文件 > "D:\Picture\前门门禁\张三\张三_20240115_143025.jpg"
echo 这是张三的示例图片文件 > "D:\Picture\前门门禁\张三\张三_20240115_154512.jpg"
echo 这是李四的示例图片文件 > "D:\Picture\前门门禁\李四\李四_20240115_162033.jpg"
echo 这是王五的示例图片文件 > "D:\Picture\后门门禁\王五\王五_20240115_143025.jpg"

echo [√] 创建了4个示例图片文件

echo.
echo 5. 显示目录结构...
echo.
echo D:\Picture\
echo ├── 前门门禁\
echo │   ├── 张三\
echo │   │   ├── 张三_20240115_143025.jpg
echo │   │   └── 张三_20240115_154512.jpg
echo │   └── 李四\
echo │       └── 李四_20240115_162033.jpg
echo └── 后门门禁\
echo     └── 王五\
echo         └── 王五_20240115_143025.jpg

echo.
echo ========================================
echo 示例目录创建完成！
echo ========================================
echo.
echo 新的目录结构说明:
echo - 图片按设备名称分类存储
echo - 每个设备下按员工姓名创建子文件夹
echo - 图片文件名格式: 姓名_时间.jpg
echo.
echo 现在可以启动程序并访问以下URL测试图片服务器:
echo - 图片列表: http://localhost:8080/images
echo - 示例图片1: http://localhost:8080/images/前门门禁/张三/张三_20240115_143025.jpg
echo - 示例图片2: http://localhost:8080/images/前门门禁/张三/张三_20240115_154512.jpg
echo - 示例图片3: http://localhost:8080/images/前门门禁/李四/李四_20240115_162033.jpg
echo - 示例图片4: http://localhost:8080/images/后门门禁/王五/王五_20240115_143025.jpg
echo.
pause 