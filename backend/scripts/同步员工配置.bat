@echo off
chcp 65001 >nul
setlocal

cd /d "%~dp0"

echo ========================================
echo 从门禁设备同步员工配置
echo ========================================
echo.

where python >nul 2>&1
if errorlevel 1 (
    where py >nul 2>&1
    if errorlevel 1 (
        echo [错误] 未找到 Python，请先安装 Python 3
        pause
        exit /b 1
    )
    py -3 "%~dp0sync_employee_config.py" %*
    set EXIT_CODE=%ERRORLEVEL%
) else (
    python "%~dp0sync_employee_config.py" %*
    set EXIT_CODE=%ERRORLEVEL%
)

echo.
if "%EXIT_CODE%"=="0" (
    echo [完成] EmployeeConfig.json 已更新
) else (
    echo [失败] 同步未成功，请检查设备网络与 DeviceConfig.json 中的账号密码
)

pause
exit /b %EXIT_CODE%
