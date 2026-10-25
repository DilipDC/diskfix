$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root

if (-not (Get-Command msbuild -ErrorAction SilentlyContinue)) {
    throw 'MSBuild was not found. Open a Visual Studio Developer PowerShell or install the .NET desktop build tools.'
}
if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    throw 'Python 3 was not found. Install Python 3 or use the Windows GitHub Actions workflow.'
}

1..3 | ForEach-Object {
    Write-Host "Source validation pass $_ of 3" -ForegroundColor Cyan
    & python scripts/validate_project.py
    if ($LASTEXITCODE -ne 0) { throw "Source validation pass $_ failed." }
}

Write-Host 'Building optimized Release configuration…' -ForegroundColor Cyan
& msbuild DriveMedic.sln /restore /m /p:Configuration=Release /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }

$testExe = Join-Path $root 'DriveMedic.Tests\bin\Release\DriveMedic.Tests.exe'
if (-not (Test-Path $testExe)) { throw "Test executable was not created: $testExe" }
1..3 | ForEach-Object {
    Write-Host "C# safety test pass $_ of 3" -ForegroundColor Cyan
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw "C# test pass $_ failed." }
}

$releaseExe = Join-Path $root 'bin\Release\DriveMedic.exe'
if (-not (Test-Path $releaseExe)) { throw "Release executable was not created: $releaseExe" }
$stage = Join-Path $root 'artifact'
if (Test-Path $stage) { Remove-Item -Path $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item $releaseExe $stage
foreach ($document in @('README.md', 'LICENSE', 'CHANGELOG.md', 'TECHNICIAN_WORKFLOW.md')) {
    Copy-Item (Join-Path $root $document) $stage
}
$config = Join-Path $root 'bin\Release\DriveMedic.exe.config'
if (Test-Path $config) { Copy-Item $config $stage }

$zip = Join-Path $root 'DriveMedic-windows.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path ($zip + '.sha256') -Value "$hash  DriveMedic-windows.zip" -Encoding ascii
Write-Host "Build and tests succeeded: $zip" -ForegroundColor Green
Write-Host "SHA-256: $hash" -ForegroundColor Green