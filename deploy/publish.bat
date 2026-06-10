@echo off
chcp 65001 >nul
setlocal EnableExtensions

cd /d "%~dp0"
set "OUT=%~dp0output"

echo ========================================
echo 人员计数 - 全量打包（前端 + 后端）
echo ========================================
echo.

if exist "%OUT%" rmdir /s /q "%OUT%"

call "%~dp0publish-backend.bat"
if errorlevel 1 (
  echo.
  echo [失败] 全量打包中止：后端打包失败
  exit /b 1
)

echo.

call "%~dp0publish-frontend.bat"
if errorlevel 1 (
  echo.
  echo [失败] 全量打包中止：前端打包失败
  exit /b 1
)

echo.
echo ========================================
echo 全量打包完成: %OUT%
echo.
echo 部署目录结构:
echo   output/backend/service/  - 运行 ACSEventConsole.exe
echo   output/backend/config/   - 编辑设备与员工配置
echo   output/backend/scripts/  - 员工同步脚本
echo   output/frontend/         - IIS 网站根目录
echo.
echo 建议步骤:
echo   1. 将 output/backend 整目录放到服务器（保持 backend/config 层级）
echo   2. 注册 Windows 服务或计划任务运行 service/ACSEventConsole.exe
echo   3. IIS 建站指向 output/frontend
echo   4. 访问 http://服务器IP/health 与看板页面验证
echo ========================================
exit /b 0
