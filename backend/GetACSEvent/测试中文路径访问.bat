@echo off
chcp 65001 >nul
echo ========================================
echo 测试中文路径访问
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
echo 2. 测试图片列表页面...
curl -s -o nul -w "图片列表页面状态码: %%{http_code}\n" http://localhost:8080/images 2>nul

echo.
echo 3. 测试中文路径访问...
echo 正在测试中文设备名称和员工姓名的访问...

REM 测试前门门禁/张三的图片
echo 测试: 前门门禁/张三
curl -s -o nul -w "前门门禁/张三 状态码: %%{http_code}\n" "http://localhost:8080/images/前门门禁/张三/张三_20240115_143025.jpg" 2>nul

REM 测试后门门禁/王五的图片
echo 测试: 后门门禁/王五
curl -s -o nul -w "后门门禁/王五 状态码: %%{http_code}\n" "http://localhost:8080/images/后门门禁/王五/王五_20240115_143025.jpg" 2>nul

echo.
echo 4. 测试URL编码的路径访问...
echo 正在测试URL编码后的路径访问...

REM 测试URL编码后的路径
echo 测试: URL编码后的前门门禁/张三
curl -s -o nul -w "编码后前门门禁/张三 状态码: %%{http_code}\n" "http://localhost:8080/images/%E5%89%8D%E9%97%A8%E9%97%A8%E7%A6%81/%E5%BC%A0%E4%B8%89/%E5%BC%A0%E4%B8%89_20240115_143025.jpg" 2>nul

echo.
echo 5. 可用的测试URL:
echo.
echo 原始中文路径:
echo - http://localhost:8080/images/前门门禁/张三/张三_20240115_143025.jpg
echo - http://localhost:8080/images/前门门禁/李四/李四_20240115_162033.jpg
echo - http://localhost:8080/images/后门门禁/王五/王五_20240115_143025.jpg
echo.
echo URL编码后的路径:
echo - http://localhost:8080/images/%E5%89%8D%E9%97%A8%E9%97%A8%E7%A6%81/%E5%BC%A0%E4%B8%89/%E5%BC%A0%E4%B8%89_20240115_143025.jpg
echo - http://localhost:8080/images/%E5%89%8D%E9%97%A8%E9%97%A8%E7%A6%81/%E6%9D%8E%E5%9B%9B/%E6%9D%8E%E5%9B%9B_20240115_162033.jpg
echo - http://localhost:8080/images/%E5%90%8E%E9%97%A8%E9%97%A8%E7%A6%81/%E7%8E%8B%E4%BA%94/%E7%8E%8B%E4%BA%94_20240115_143025.jpg

echo.
echo ========================================
echo 测试完成！
echo ========================================
echo.
echo 说明:
echo - 如果状态码是200，说明中文路径访问正常
echo - 如果状态码是404，说明文件不存在或路径有问题
echo - 如果状态码是其他，说明有其他问题
echo.
echo 建议:
echo - 在浏览器中直接访问上述URL进行测试
echo - 检查图片文件是否确实存在于对应目录
echo - 确保文件名和路径完全匹配
echo.
pause 