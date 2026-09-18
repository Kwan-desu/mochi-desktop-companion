@echo off
cd /d "%~dp0"
if not exist "app\MochiDuo.exe" (
 dotnet publish src\Mochi.Desktop\MochiDuo.csproj -c Release -o app --self-contained false
 if errorlevel 1 (pause & exit /b 1)
)
start "" "app\MochiDuo.exe"
