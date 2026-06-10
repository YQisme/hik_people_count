@echo off
chcp 65001 >nul
setlocal

cd /d "%~dp0"

echo ========================================
echo 启动门禁事件服务（先同步员工配置）
echo ========================================
echo.

where python >nul 2>&1
if errorlevel 1 (
    py -3 "%~dp0..\scripts\sync_employee_config.py"
) else (
    python "%~dp0..\scripts\sync_employee_config.py"
)

if errorlevel 1 (
    echo.
    echo [警告] 员工同步失败，将继续使用已有 EmployeeConfig.json 启动服务
    echo.
)

dotnet run --project "%~dp0..\src\ACSEventConsole\ACSEventConsole.csproj"
exit /b %ERRORLEVEL%
