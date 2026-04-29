param(
    [string]$RepositoryPath = 'C:\Users\nakashimajunichiro\Documents\VsSolutions\ErrorReport',
    [string]$GitHubOwner = 'JunichiroNakashima',
    [string]$RepositoryName = 'ErrorReport',
    [switch]$Public
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $RepositoryPath)) {
    throw "RepositoryPath was not found: $RepositoryPath"
}

Set-Location -LiteralPath $RepositoryPath

if (-not (Test-Path -LiteralPath '.git')) {
    git init
}

git branch -M main

git add .

$hasHead = $true
try {
    git rev-parse --verify HEAD *> $null
}
catch {
    $hasHead = $false
}

if (-not $hasHead) {
    git commit -m 'Initial import of Naranja.ErrorReport'
}

$remoteUrl = "https://github.com/$GitHubOwner/$RepositoryName.git"
$visibility = if ($Public) { '--public' } else { '--private' }

$originExists = $true
try {
    git remote get-url origin *> $null
}
catch {
    $originExists = $false
}

if (-not $originExists) {
    gh repo create $RepositoryName $visibility --source . --remote origin --push
}
else {
    git remote set-url origin $remoteUrl
    git push -u origin main
}

Write-Host ''
Write-Host 'Git remote status:' -ForegroundColor Cyan
git remote -v
Write-Host ''
Write-Host 'Working tree status:' -ForegroundColor Cyan
git status --short
