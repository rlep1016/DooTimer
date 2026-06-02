# DooTimer 编译脚本：自动杀掉旧进程再编译
$ErrorActionPreference = "Stop"

Write-Host ">>> 查找并关闭旧的 DooTimer 进程..." -ForegroundColor Cyan
$procs = Get-Process -Name "DooTimer" -ErrorAction SilentlyContinue
if ($procs) {
    $procs | ForEach-Object {
        Write-Host "  关闭 PID $($_.Id)..." -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force
    }
    Start-Sleep -Seconds 1
    Write-Host "  已关闭。" -ForegroundColor Green
} else {
    Write-Host "  没有运行中的 DooTimer。" -ForegroundColor Gray
}

Write-Host ">>> 编译 DooTimer..." -ForegroundColor Cyan
dotnet build DooTimer-cs/DooTimer.csproj

if ($LASTEXITCODE -eq 0) {
    Write-Host ">>> 编译成功！启动程序：" -ForegroundColor Green
    Write-Host "  DooTimer-cs\bin\Debug\net8.0-windows\win-x64\DooTimer.exe" -ForegroundColor White
}
