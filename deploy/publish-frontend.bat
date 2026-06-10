@echo off
chcp 65001 >nul
setlocal EnableExtensions

cd /d "%~dp0.."
set "ROOT=%CD%"
set "OUT=%ROOT%\deploy\output"
set "FRONTEND_OUT=%OUT%\frontend"
set "IIS_CONFIG=%ROOT%\deploy\iis\web.config"

echo ========================================
echo 人员计数 - 前端打包
echo ========================================
echo.

if not exist "%ROOT%\frontend\package.json" (
  echo [错误] 未找到 frontend\package.json
  exit /b 1
)

where yarn >nul 2>&1
if errorlevel 1 (
  where npm >nul 2>&1
  if errorlevel 1 (
    echo [错误] 未找到 yarn 或 npm，请先安装 Node.js
    exit /b 1
  )
  set "PKG_MGR=npm"
) else (
  set "PKG_MGR=yarn"
)

if exist "%FRONTEND_OUT%" rmdir /s /q "%FRONTEND_OUT%"
mkdir "%FRONTEND_OUT%" 2>nul

echo [1/2] 构建前端 (%PKG_MGR%)...
pushd "%ROOT%\frontend"
if /I "%PKG_MGR%"=="yarn" (
  call yarn install
  if errorlevel 1 goto :build_failed
  call yarn build
) else (
  call npm install
  if errorlevel 1 goto :build_failed
  call npm run build
)
if errorlevel 1 goto :build_failed
popd
goto :build_ok

:build_failed
popd
echo [失败] 前端构建失败
exit /b 1

:build_ok
echo.
echo [2/2] 复制静态资源到部署目录...
if not exist "%ROOT%\frontend\dist\index.html" (
  echo [失败] 未找到 frontend\dist，构建可能未成功
  exit /b 1
)

xcopy /Y /E /I /Q "%ROOT%\frontend\dist\*" "%FRONTEND_OUT%\" >nul

if exist "%IIS_CONFIG%" (
  copy /Y "%IIS_CONFIG%" "%FRONTEND_OUT%\web.config" >nul
  echo   已复制 IIS web.config
) else (
  echo [警告] 未找到 deploy\iis\web.config，请手动配置 IIS SPA 路由
)

echo.
echo ========================================
echo 前端打包完成: %FRONTEND_OUT%
echo   将此目录设为 IIS 网站物理路径
echo.
echo 提示: 构建前可在 frontend\.env.production 中设置 VITE_API_BASE_URL
echo   例如 http://服务器IP:8081；留空则默认访问同主机 8081 端口
echo ========================================
exit /b 0
