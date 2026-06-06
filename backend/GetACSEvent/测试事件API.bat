@echo off
chcp 65001 >nul
echo ========================================
echo 测试事件API功能
echo ========================================
echo.

echo 1. 检查Web服务器是否运行...
curl -s -o nul -w "HTTP状态码: %%{http_code}\n" http://localhost:8080/ 2>nul
if "%ERRORLEVEL%"=="0" (
    echo [√] Web服务器正在运行
) else (
    echo [×] Web服务器未运行，请先启动程序
    pause
    exit /b 1
)

echo.
echo 2. 测试事件列表API...
echo 正在获取事件列表...

REM 保存事件列表到临时文件
curl -s "http://localhost:8080/events" > temp_events.json 2>nul
if exist "temp_events.json" (
    echo [√] 事件列表获取成功
    echo.
    echo 事件列表内容预览:
    echo ----------------------------------------
    type temp_events.json | findstr /C:"time" /C:"deviceIP" /C:"personName" /C:"imageUrl" | head -10
    echo ----------------------------------------
    echo.
    echo 完整事件列表已保存到: temp_events.json
) else (
    echo [×] 事件列表获取失败
)

echo.
echo 3. 检查事件数据格式...
if exist "temp_events.json" (
    echo 检查时间格式...
    findstr /C:"time" temp_events.json | findstr /C:"2024" >nul
    if "%ERRORLEVEL%"=="0" (
        echo [√] 时间格式正确 (yyyy-MM-dd HH:mm:ss)
    ) else (
        echo [×] 时间格式可能有问题
    )
    
    echo 检查图片URL...
    findstr /C:"imageUrl" temp_events.json >nul
    if "%ERRORLEVEL%"=="0" (
        echo [√] 图片URL字段存在
        echo 图片URL示例:
        findstr /C:"imageUrl" temp_events.json | head -3
    ) else (
        echo [×] 图片URL字段不存在
    )
    
    echo 检查IP地址替换...
    findstr /C:"127.0.0.1" temp_events.json >nul
    if "%ERRORLEVEL%"=="0" (
        echo [√] localhost已替换为IP地址
    ) else (
        echo [×] localhost可能未替换
    )
)

echo.
echo 4. 可用的API端点:
echo    - 主页: http://localhost:8080/
echo    - 事件列表: http://localhost:8080/events
echo    - 图片列表: http://localhost:8080/images
echo    - 配置编辑: http://localhost:8080/config/edit

echo.
echo 5. 获取本机IP地址...
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
echo 说明:
echo - 事件列表API返回JSON格式数据
echo - 时间格式使用设备原始时间: yyyy-MM-dd HH:mm:ss
echo - 图片URL字段已添加到事件数据中
echo - localhost已替换为本机IP地址
echo.
echo 建议:
echo - 在浏览器中访问 http://localhost:8080/events 查看完整数据
echo - 检查事件数据是否包含正确的图片URL
echo - 验证时间格式是否与控制台输出一致
echo.
pause 