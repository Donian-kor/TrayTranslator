# GitHub Release 발행 (v1.6). 토큰은 출력하지 않는다.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
$owner = 'Donian-kor'
$repo = 'TrayTranslator'
$tag = 'v1.6'

$lines = "protocol=https`nhost=github.com`n" | git credential fill 2>$null
$token = ($lines | Where-Object { $_.StartsWith('password=') } | Select-Object -First 1)
if (-not $token) { Write-Output 'NO_CREDENTIAL'; exit 1 }
$token = $token.Substring(9)
$headers = @{ Authorization = "Bearer $token"; Accept = 'application/vnd.github+json' }

$notes = @(
    '## TrayTranslator v1.6',
    '',
    '드래그한 텍스트를 핫키로 즉시 번역하는 트레이 상주 프로그램.',
    '',
    '### 엔진 10종',
    '- AI 모델: DeepSeek, Gemini, Groq, LM Studio, OpenAI',
    '- 번역 전용: DeepL, Google 번역, Microsoft 번역, Papago',
    '- 내장: 오프라인 번역 Hy-MT2 (키 불필요, 무제한)',
    '',
    '### 사용법',
    '1. TrayTranslator.exe 실행 후 트레이 우클릭, 설정에서 번역 엔진 선택',
    '2. 키 발급 후 연결 테스트, 저장 (API 키는 DPAPI 암호화 저장)',
    '3. 텍스트 드래그, 핫키 입력, 커서 옆 팝업 확인 및 자동 클립보드 복사',
    '',
    '### 필요 환경',
    '- Windows 10/11 64bit, .NET 10 런타임'
) -join "`n"

$body = @{ tag_name = $tag; name = $tag; body = $notes; draft = $false; prerelease = $false } | ConvertTo-Json
try {
    $rel = Invoke-RestMethod -Method Post -Uri "https://api.github.com/repos/$owner/$repo/releases" -Headers $headers -Body $body -ContentType 'application/json'
} catch {
    Write-Output ("CREATE_FAILED:" + $_.Exception.Message.Split([Environment]::NewLine)[0])
    exit 1
}
Write-Output ("RELEASE_ID=" + $rel.id)
Write-Output ("RELEASE_URL=" + $rel.html_url)

$stage = Join-Path ([IO.Path]::GetTempPath()) 'tt-release'
New-Item -ItemType Directory -Force $stage > $null
Copy-Item (Join-Path $root 'publish\TrayTranslator.exe') (Join-Path $stage 'TrayTranslator.exe') -Force
Copy-Item (Join-Path $root 'README.md') (Join-Path $stage 'README.md') -Force
$zip = Join-Path ([IO.Path]::GetTempPath()) 'TrayTranslator-v1.6-win-x64.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
$upHeaders = @{ Authorization = "Bearer $token"; Accept = 'application/vnd.github+json'; 'Content-Type' = 'application/octet-stream' }
try {
    $up = Invoke-RestMethod -Method Post `
        -Uri ("https://uploads.github.com/repos/$owner/$repo/releases/" + $rel.id + '/assets?name=TrayTranslator-v1.6-win-x64.zip') `
        -Headers $upHeaders -InFile $zip
    Write-Output ("ASSET_URL=" + $up.browser_download_url)
} catch {
    Write-Output ("UPLOAD_FAILED:" + $_.Exception.Message.Split([Environment]::NewLine)[0])
    exit 1
}
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
Write-Output 'RELEASE_DONE'
