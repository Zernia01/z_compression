$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$compilerCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw 'Inno Setup 6이 설치되어 있지 않습니다. winget install --id JRSoftware.InnoSetup 명령으로 설치하세요.'
}

$publishedApp = Join-Path $projectRoot 'artifacts\win-x64\z_compression.exe'
if (-not (Test-Path -LiteralPath $publishedApp)) {
    throw '먼저 artifacts\win-x64에 win-x64 앱을 게시하세요.'
}
$publishedUpdater = Join-Path $projectRoot 'artifacts\win-x64\ZCompression.Updater.exe'
if (-not (Test-Path -LiteralPath $publishedUpdater)) {
    throw 'artifacts\win-x64에 ZCompression.Updater.exe가 없습니다. 업데이터를 먼저 게시하세요.'
}

& $compiler (Join-Path $PSScriptRoot 'z_compression.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup 컴파일 실패: $LASTEXITCODE" }
