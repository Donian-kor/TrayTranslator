# 자동 발행: 실행 중이면 강제 종료 → 고아 서버 정리 → publish → 재실행
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSCommandPath

Get-Process -Name 'TrayTranslator' -ErrorAction SilentlyContinue | ForEach-Object {
    try { $_ | Stop-Process -Force; Write-Output "killed TrayTranslator pid=$($_.Id)" } catch { }
}
Start-Sleep -Milliseconds 800

$serverDirs = @(
    (Join-Path $root 'publish\data\llama-server'),
    (Join-Path $env:APPDATA 'TrayTranslator\llama-server')
)
Get-Process -Name 'llama-server' -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $f = $_.MainModule.FileName
        foreach ($d in $serverDirs) {
            if ($f -like ($d + '*')) { $_ | Stop-Process -Force; Write-Output "killed orphan server pid=$($_.Id)"; break }
        }
    } catch { }
}
Start-Sleep -Milliseconds 500

dotnet publish -c Release -r win-x64 --self-contained false -o publish --nologo -v minimal
if ($LASTEXITCODE -eq 0) {
    Start-Process (Join-Path $root 'publish\TrayTranslator.exe')
    Write-Output 'published+relaunched'
} else {
    Write-Output 'PUBLISH_FAILED'
    exit 1
}
