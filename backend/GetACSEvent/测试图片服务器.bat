@echo off
chcp 65001 >nul
echo ========================================
echo 门禁事件图片服务器测试脚本
echo ========================================
echo.

@REM echo 1. 检查程序是否运行...
@REM tasklist /FI "IMAGENAME eq GetACSEvent.exe" 2>NUL | find /I /N "GetACSEvent.exe">NUL
@REM if "%ERRORLEVEL%"=="0" (
@REM     echo [√] 程序正在运行
@REM ) else (
@REM     echo [×] 程序未运行，请先启动程序
@REM     pause
@REM     exit /b 1
@REM )

echo.
echo 2. 检查Web服务器是否响应...
curl -s -o nul -w "HTTP状态码: %%{http_code}\n" http://localhost:8080/ 2>nul
if "%ERRORLEVEL%"=="0" (
    echo [√] Web服务器响应正常
) else (
    echo [×] Web服务器无响应
)

echo.
echo 3. 检查图片目录...
if exist "D:\Picture" (
    echo [√] 图片目录存在: D:\Picture
    dir /s /b "D:\Picture\*.jpg" 2>nul | find /c /v "" > temp_count.txt
    set /p count=<temp_count.txt
    del temp_count.txt
    echo [√] 找到 %count% 个图片文件
) else (
    echo [×] 图片目录不存在: D:\Picture
)

echo.
echo 4. 测试图片列表页面...
curl -s -o nul -w "图片列表页面状态码: %%{http_code}\n" http://localhost:8080/images 2>nul

echo.
echo 5. 可用的访问地址:
echo    - 主页: http://localhost:8080/
echo    - 事件列表: http://localhost:8080/events
echo    - 图片列表: http://localhost:8080/images
echo    - 配置编辑: http://localhost:8080/config/edit

echo.
echo 6. 获取本机IP地址...
for /f "tokens=2 delims=:" %%a in ('ipconfig ^| findstr /r /c:"IPv4"') do (
    set ip=%%a
    set ip=!ip: =!
    echo [√] 本机IP: !ip!
    echo [√] 外部访问地址: http://!ip!:8080/
    goto :found_ip
)
:found_ip

echo.
echo ========================================
echo 测试完成！
echo ========================================
echo.
echo 提示:
echo - 如果图片无法访问，请检查防火墙设置
echo - 如果端口被占用，请修改app.config中的端口配置
echo - 图片URL格式: http://localhost:8080/images/{设备名称}/{员工姓名}/{文件名}
echo - 示例: http://localhost:8080/images/前门门禁/张三/张三_20240115_143025.jpg
echo.
pause 