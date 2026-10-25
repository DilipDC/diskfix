$ErrorActionPreference = 'Stop'

function Invoke-Git {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    & git @Arguments
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE." }
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git was not found. Install Git for Windows, then run this script again.'
}
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) was not found. Install it from https://cli.github.com/, then run this script again.'
}
& gh auth status
if ($LASTEXITCODE -ne 0) { throw 'Sign in first with: gh auth login' }
$owner = (& gh api user --jq .login).Trim()
$userId = (& gh api user --jq .id).Trim()
if ([string]::IsNullOrWhiteSpace($owner) -or [string]::IsNullOrWhiteSpace($userId)) { throw 'Could not determine the authenticated GitHub user.' }

$repoName = Read-Host 'GitHub repository name (default: DriveMedic)'
if ([string]::IsNullOrWhiteSpace($repoName)) { $repoName = 'DriveMedic' }
if ($repoName -notmatch '^[A-Za-z0-9_.-]+$') { throw 'Use only letters, numbers, dots, underscores, and hyphens in the repository name.' }
$visibility = Read-Host 'Visibility: private or public (default: private)'
if ([string]::IsNullOrWhiteSpace($visibility)) { $visibility = 'private' }
$visibility = $visibility.ToLowerInvariant()
if ($visibility -notin @('private', 'public')) { throw 'Visibility must be private or public.' }

Set-Location $PSScriptRoot
if (-not (Test-Path (Join-Path $PSScriptRoot '.git'))) {
    Invoke-Git -Arguments @('init')
}
$gitName = (& git config user.name 2>$null)
if ([string]::IsNullOrWhiteSpace(($gitName -join '').Trim())) { Invoke-Git -Arguments @('config', 'user.name', $owner) }
$gitEmail = (& git config user.email 2>$null)
if ([string]::IsNullOrWhiteSpace(($gitEmail -join '').Trim())) {
    Invoke-Git -Arguments @('config', 'user.email', "$userId+$owner@users.noreply.github.com")
}
Invoke-Git -Arguments @('branch', '-M', 'main')
Invoke-Git -Arguments @('add', '-A')
Write-Host "`nFiles staged for publishing:" -ForegroundColor Cyan
& git status --short
if ($LASTEXITCODE -ne 0) { throw 'Unable to show staged files.' }
$review = Read-Host 'Review the listed files. Continue with this commit/push? (Y/N)'
if ($review -notmatch '^(Y|YES)$') { throw 'Publishing cancelled before commit/push.' }

$hasHead = $true
& git rev-parse --verify HEAD *> $null
if ($LASTEXITCODE -ne 0) { $hasHead = $false }
$hasStagedChanges = $false
& git diff --cached --quiet
if ($LASTEXITCODE -eq 1) { $hasStagedChanges = $true }
elseif ($LASTEXITCODE -gt 1) { throw 'Unable to inspect staged changes.' }

if (-not $hasHead -or $hasStagedChanges) {
    $message = if ($hasHead) { 'Improve DriveMedic recovery safety and validation' } else { 'Initial DriveMedic Windows recovery utility' }
    Invoke-Git -Arguments @('commit', '-m', $message)
}

$target = "$owner/$repoName"
$repoUrl = "https://github.com/$target.git"

$repoJson = & gh repo view $target --json name,isPrivate 2>$null
$repoExists = ($LASTEXITCODE -eq 0)
if ($repoExists) {
    $repoMetadata = $repoJson | ConvertFrom-Json
    $actualVisibility = if ($repoMetadata.isPrivate) { 'private' } else { 'public' }
    if ($actualVisibility -ne $visibility) {
        throw "Repository $target already exists with '$actualVisibility' visibility, but '$visibility' was selected. No push was made; change the repository visibility deliberately or rerun with the matching choice."
    }
} else {
    $visibilityFlag = if ($visibility -eq 'public') { '--public' } else { '--private' }
    & gh repo create $target $visibilityFlag --description 'Windows drive diagnostics and copy-first file recovery utility'
    if ($LASTEXITCODE -ne 0) { throw "Could not create $target. Check repository-name availability and GitHub permissions." }
}

$remotes = @(& git remote)
if ($remotes -contains 'origin') {
    Invoke-Git -Arguments @('remote', 'set-url', 'origin', $repoUrl)
} else {
    Invoke-Git -Arguments @('remote', 'add', 'origin', $repoUrl)
}

# Never force-push. If a remote main branch exists, fast-forward history or stop safely on divergence.
& git ls-remote --exit-code --heads origin main *> $null
$remoteHasMain = ($LASTEXITCODE -eq 0)
if ($remoteHasMain) {
    Invoke-Git -Arguments @('fetch', 'origin', 'main')
    & git merge-base --is-ancestor origin/main HEAD
    $remoteIsAncestor = ($LASTEXITCODE -eq 0)
    if (-not $remoteIsAncestor) {
        & git merge-base --is-ancestor HEAD origin/main
        $localIsAncestor = ($LASTEXITCODE -eq 0)
        if ($localIsAncestor) {
            Invoke-Git -Arguments @('merge', '--ff-only', 'origin/main')
        } else {
            throw 'The remote main branch and local project have diverged. Nothing was force-pushed; reconcile the histories before retrying.'
        }
    }
}
Invoke-Git -Arguments @('push', '-u', 'origin', 'main')

Write-Host "Published to https://github.com/$target" -ForegroundColor Green
Write-Host "The Windows build should start at https://github.com/$target/actions" -ForegroundColor Cyan
Write-Host 'Open the latest Windows build workflow and download the DriveMedic-windows artifact after all checks pass.' -ForegroundColor Cyan