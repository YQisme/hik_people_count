@echo off
setlocal EnableExtensions

cd /d "%~dp0.."
set "ROOT=%CD%"
set "OUT=%ROOT%\deploy\output"
set "BACKEND_OUT=%OUT%\backend\service"
set "CONFIG_OUT=%OUT%\backend\config"
set "FRONTEND_OUT=%OUT%\frontend"

echo ========================================
echo PersonCount - Production Build
echo ========================================
echo.

if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%BACKEND_OUT%" 2>nul
mkdir "%CONFIG_OUT%" 2>nul
mkdir "%FRONTEND_OUT%" 2>nul

echo [1/3] Publish backend...
dotnet publish "%ROOT%\backend\src\ACSEventConsole\ACSEventConsole.csproj" -c Release -r win-x64 --self-contained false -o "%BACKEND_OUT%"
if errorlevel 1 (
  echo Backend publish failed.
  exit /b 1
)

echo.
echo [2/3] Build frontend...
pushd "%ROOT%\frontend"
where yarn >nul 2>&1
if errorlevel 1 (
  call npm install
  call npm run build
) else (
  call yarn install
  call yarn build
)
if errorlevel 1 (
  popd
  echo Frontend build failed.
  exit /b 1
)
popd

echo.
echo [3/3] Copy deploy files...
xcopy /Y /E /I "%ROOT%\frontend\dist\*" "%FRONTEND_OUT%\"
copy /Y "%ROOT%\deploy\iis\web.config" "%FRONTEND_OUT%\web.config"

if not exist "%CONFIG_OUT%\DeviceConfig.json" (
  if exist "%ROOT%\backend\config\DeviceConfig.json.example" (
    copy /Y "%ROOT%\backend\config\DeviceConfig.json.example" "%CONFIG_OUT%\DeviceConfig.json"
  )
)
if not exist "%CONFIG_OUT%\EmployeeConfig.json" (
  if exist "%ROOT%\backend\config\EmployeeConfig.json.example" (
    copy /Y "%ROOT%\backend\config\EmployeeConfig.json.example" "%CONFIG_OUT%\EmployeeConfig.json"
  )
)

echo.
echo ========================================
echo Done: %OUT%
echo   backend\service\  - backend exe
echo   backend\config\   - config files
echo   frontend\         - IIS site root
echo See deploy\iis\README.md
echo ========================================
exit /b 0
