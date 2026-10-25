$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root

if (-not (Get-Command msbuild -ErrorAction SilentlyContinue)) {
    throw 'MSBuild was not found. Open a Visual Studio Developer PowerShell or install the .NET desktop build tools.'
}
if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    throw 'Python 3 was not found. Install Python 3 or use the Windows GitHub Actions workflow.'
}

$tools = Join-Path $root 'tools'
New-Item -ItemType Directory -Force -Path $tools | Out-Null

$bundles = @(
    @{ Name = 'testdisk-7.2.win64.zip'; Url = 'https://www.cgsecurity.org/testdisk-7.2.win64.zip'; Sha256 = 'e97e203ce77b6b1a3a37d01beccf069dc6c4632b579ffbb82ae739cdda229f38' },
    @{ Name = 'testdisk-7.2.win.zip'; Url = 'https://www.cgsecurity.org/testdisk-7.2.win.zip'; Sha256 = '0718a4529611e5f8b6d5fcb873279d23a0b212b18bcf72291f3b47351bfc7126' },
    @{ Name = 'testdisk-7.2.tar.bz2'; Url = 'https://www.cgsecurity.org/testdisk-7.2.tar.bz2'; Sha256 = 'f8343be20cb4001c5d91a2e3bcd918398f00ae6d8310894a5a9f2feb813c283f' }
)
foreach ($bundle in $bundles) {
    $path = Join-Path $tools $bundle.Name
    if (-not (Test-Path $path)) {
        Write-Host "Downloading official upstream archive $($bundle.Name)…" -ForegroundColor Cyan
        Invoke-WebRequest -UseBasicParsing -Uri $bundle.Url -OutFile $path
    }
    $actual = (Get-FileHash -Path $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $bundle.Sha256) {
        Remove-Item -Path $path -Force -ErrorAction SilentlyContinue
        throw "SHA-256 mismatch for $($bundle.Name). Expected $($bundle.Sha256), got $actual."
    }
    Write-Host "Verified $($bundle.Name) SHA-256: $actual" -ForegroundColor Green
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
foreach ($document in @('README.md', 'LICENSE', 'CHANGELOG.md', 'TECHNICIAN_WORKFLOW.md', 'RECOVERY_WORKBENCH.md', 'THIRD_PARTY_NOTICES.md')) {
    Copy-Item (Join-Path $root $document) $stage
}
$thirdParty = Join-Path $stage 'ThirdParty\TestDisk'
New-Item -ItemType Directory -Force -Path $thirdParty | Out-Null
Copy-Item (Join-Path $tools 'testdisk-7.2.tar.bz2') (Join-Path $thirdParty 'testdisk-7.2.tar.bz2')

$config = Join-Path $root 'bin\Release\DriveMedic.exe.config'
if (Test-Path $config) { Copy-Item $config $stage }

$zip = Join-Path $root 'DriveMedic-windows.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path ($zip + '.sha256') -Value "$hash  DriveMedic-windows.zip" -Encoding ascii
Write-Host "Build and tests succeeded: $zip" -ForegroundColor Green
Write-Host "SHA-256: $hash" -ForegroundColor Green