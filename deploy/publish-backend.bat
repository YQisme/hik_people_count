@echo off
chcp 65001 >nul
setlocal EnableExtensions

cd /d "%~dp0.."
set "ROOT=%CD%"
set "OUT=%ROOT%\deploy\output"
set "BACKEND_OUT=%OUT%\backend\service"
set "CONFIG_OUT=%OUT%\backend\config"
set "SCRIPTS_OUT=%OUT%\backend\scripts"

echo ========================================
echo 人员计数 - 后端打包
echo ========================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
  echo [错误] 未找到 dotnet，请先安装 .NET SDK 8+
  exit /b 1
)

if exist "%BACKEND_OUT%" rmdir /s /q "%BACKEND_OUT%"
mkdir "%BACKEND_OUT%" 2>nul
mkdir "%CONFIG_OUT%" 2>nul
mkdir "%SCRIPTS_OUT%" 2>nul

echo [1/2] 发布后端 (Release / win-x64)...
dotnet publish "%ROOT%\backend\src\ACSEventConsole\ACSEventConsole.csproj" -c Release -r win-x64 --self-contained false -o "%BACKEND_OUT%"
if errorlevel 1 (
  echo [失败] 后端发布失败
  exit /b 1
)

echo   复制海康 SDK 运行时依赖...
xcopy /Y /E /I /Q "%ROOT%\backend\src\ACSEventConsole\Runtime\x64\*" "%BACKEND_OUT%\" >nul
if errorlevel 1 (
  echo [失败] 未找到 Runtime\x64，请确认 SDK 文件完整
  exit /b 1
)

echo.
echo [2/2] 复制配置模板与运维脚本...
if not exist "%CONFIG_OUT%\DeviceConfig.json" (
  if exist "%ROOT%\backend\config\DeviceConfig.json.example" (
    copy /Y "%ROOT%\backend\config\DeviceConfig.json.example" "%CONFIG_OUT%\DeviceConfig.json" >nul
    echo   已复制 DeviceConfig.json.example
  ) else (
    echo [警告] 未找到 DeviceConfig.json.example
  )
)
if not exist "%CONFIG_OUT%\EmployeeConfig.json" (
  if exist "%ROOT%\backend\config\EmployeeConfig.json.example" (
    copy /Y "%ROOT%\backend\config\EmployeeConfig.json.example" "%CONFIG_OUT%\EmployeeConfig.json" >nul
    echo   已复制 EmployeeConfig.json.example
  ) else (
    echo [警告] 未找到 EmployeeConfig.json.example
  )
)

if exist "%ROOT%\backend\config\DeviceConfig.json" (
  copy /Y "%ROOT%\backend\config\DeviceConfig.json" "%CONFIG_OUT%\DeviceConfig.json" >nul
  echo   已使用本地 DeviceConfig.json 覆盖模板
)
if exist "%ROOT%\backend\config\EmployeeConfig.json" (
  copy /Y "%ROOT%\backend\config\EmployeeConfig.json" "%CONFIG_OUT%\EmployeeConfig.json" >nul
  echo   已使用本地 EmployeeConfig.json 覆盖模板
)

xcopy /Y /E /I /Q "%ROOT%\backend\scripts\*" "%SCRIPTS_OUT%\" >nul

echo.
echo ========================================
echo 后端打包完成: %OUT%\backend
echo   service\  - ACSEventConsole.exe 及 SDK 依赖
echo   config\   - DeviceConfig.json / EmployeeConfig.json
echo   scripts\  - 员工同步等运维脚本
echo ========================================
exit /b 0
