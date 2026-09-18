@echo off
cd /d "%~dp0"
dotnet publish src\Mochi.Desktop\MochiDuo.csproj -c Release -o app --self-contained false
if errorlevel 1 (pause & exit /b 1)
echo Build ready. Open Launch Desktop Companion.cmd to start.
pause
