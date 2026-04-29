# ==================================================
# ErrorReport Deploy Script (No Launcher)
# ターミナルで実行: .\_scripts\Deploy.ps1
# ==================================================
#
# 配布元フォルダ構成 (ランチャーなし):
# \ErrorReport
#  ├─ app\(本体 exe + dll)
#  └─ deploy.json (バージョン情報)

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_UI_LANGUAGE = "en-US"

# PowerShell 7+ 必須 (5.1 では XML プロパティ代入が失敗する)
if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "This script requires PowerShell 7+. Run with: pwsh -File $($MyInvocation.MyCommand.Path)"
}

# --- 0. 基本設定 ---
$AppName = "ErrorReport"

$SolutionRoot = Split-Path $PSScriptRoot -Parent
$MainProj = Join-Path $SolutionRoot "$AppName\$AppName.csproj"

$DfsRoot = "\\naranja.local\dfs02"
$DeployTargetDir = Join-Path $DfsRoot "naranja-deploy\NaranjaApp\$AppName"

$AppPublishDir = Join-Path $DeployTargetDir "app"
$FlagFile = Join-Path $DeployTargetDir "DEPLOY_IN_PROGRESS.txt"
$VersionJson = Join-Path $DeployTargetDir "deploy.json"

$PublishOutput = Join-Path $SolutionRoot "$AppName\bin\Release\publish"

$PropsPath = Join-Path $SolutionRoot "Directory.Build.props"

# 自社NuGetパッケージ関連
$PlatformPackagesDir = "C:\Users\nakashimajunichiro\Documents\VsSolutions\PlatformPackages"
$PlatformPackagePrefixes = @("Naranja.Platform.Common", "Naranja.Platform.Data")

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$LogDir = Join-Path $ScriptDir "logs"
if (-not (Test-Path $LogDir)) {
    New-Item -Path $LogDir -ItemType Directory -Force | Out-Null
}

Write-Host "--- [$AppName] Deployment started ---" -ForegroundColor Cyan

try {
    # --- 1. Git 事前チェック ---
    Write-Host "[1/8] Checking Git status..."
    $currentBranch = (git rev-parse --abbrev-ref HEAD).Trim()
    if ($currentBranch -ne "develop") {
        throw "Deployment must be executed on the 'develop' branch. Current branch is: $currentBranch"
    }

    if (git status --porcelain) {
        throw "Uncommitted changes found. Please commit or stash them first."
    }

    # --- 2. NuGet パッケージ更新チェック ---
    Write-Host "[2/8] Checking for NuGet package updates (Platform packages only)..."

    [xml]$projXml = Get-Content $MainProj
    $updates = @()

    # --- 2a. 自社パッケージ (Naranja.Platform.*) をローカルフォルダからチェック ---
    $allPkgRefs = $projXml.Project.ItemGroup.PackageReference
    $hasPlatformPackages = $allPkgRefs | Where-Object { $_.Include -like "Naranja.Platform.*" }

    if ($hasPlatformPackages) {
        Write-Host "  Checking Naranja.Platform packages from local source..."
        foreach ($prefix in $PlatformPackagePrefixes) {
            $pkgRef = $allPkgRefs | Where-Object { $_.Include -eq $prefix }
            if (-not $pkgRef) { continue }
            $currentVer = [Version]$pkgRef.Version

            $latestFile = Get-ChildItem $PlatformPackagesDir -Filter "$prefix.*.nupkg" -ErrorAction SilentlyContinue |
                ForEach-Object {
                    $verStr = $_.BaseName.Substring($prefix.Length + 1)
                    try   { [PSCustomObject]@{ File = $_; Version = [Version]$verStr } }
                    catch { $null }
                } |
                Where-Object { $_ -ne $null } |
                Sort-Object Version -Descending |
                Select-Object -First 1

            if ($latestFile -and $latestFile.Version -gt $currentVer) {
                $updates += [PSCustomObject]@{
                    Package        = $prefix
                    CurrentVersion = $currentVer.ToString()
                    LatestVersion  = $latestFile.Version.ToString()
                    Source         = "Platform"
                }
            }
        }
    } else {
        Write-Host "  No Naranja.Platform packages found. Skipping local source check."
    }

    # --- 2b. 公開パッケージチェック (現在無効: 自社パッケージのみチェックする方針) ---
    # 有効にする場合はコメントを外してください。
    # Write-Host "  Checking public packages from nuget.org..."
    # $outdatedOutput = dotnet list $MainProj package --outdated --format json 2>$null
    # if ($LASTEXITCODE -eq 0 -and $outdatedOutput) {
    # $outdatedJson = $outdatedOutput | ConvertFrom-Json
    # foreach ($project in $outdatedJson.projects) {
    # foreach ($fw in $project.frameworks) {
    # foreach ($pkg in $fw.topLevelPackages) {
    # # 自社パッケージは 2a で処理済みなのでスキップ
    # if ($pkg.id -like "Ndc.*") { continue }
    # if ($pkg.latestVersion -and $pkg.latestVersion -ne $pkg.resolvedVersion) {
    # $updates += [PSCustomObject]@{
    # Package        = $pkg.id
    # CurrentVersion = $pkg.resolvedVersion
    # LatestVersion  = $pkg.latestVersion
    # Source         = "NuGet"
    # }
    # }
    # }
    # }
    # }
    # }

    # --- 2c. 更新があれば確認して適用 ---
    if ($updates.Count -gt 0) {
        Write-Host ""
        Write-Host "  Updates available:" -ForegroundColor Yellow
        foreach ($u in $updates) {
            Write-Host "    [$($u.Source)] $($u.Package): $($u.CurrentVersion) -> " -NoNewline -ForegroundColor Gray
            Write-Host "$($u.LatestVersion)" -ForegroundColor Green
        }
        Write-Host ""
        $choice = Read-Host "  Update packages before deploying? (Y/n)"
        if ($choice -eq "" -or $choice -match "^[Yy]") {
            foreach ($u in $updates) {
                Write-Host "  Updating $($u.Package) to $($u.LatestVersion)..." -ForegroundColor Cyan
                dotnet add $MainProj package $u.Package --version $u.LatestVersion --no-restore
                if ($LASTEXITCODE -ne 0) { throw "Failed to update $($u.Package)" }
            }
            dotnet restore $MainProj --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed after package update." }

            git add $MainProj
            git commit -m "Update NuGet packages: $($updates | ForEach-Object { "$($_.Package) $($_.LatestVersion)" } | Join-String -Separator ', ')"
            Write-Host "  OK Packages updated and committed" -ForegroundColor Green
        } else {
            Write-Host "  Skipped. Deploying with current versions." -ForegroundColor Gray
        }
    } else {
        Write-Host "  All Naranja.Platform packages are up to date." -ForegroundColor Green
    }

    # --- 3. バージョン生成 & 更新 ---
    if (-not (Test-Path $PropsPath)) {
        throw "Directory.Build.props not found at: $PropsPath"
    }
    $newVer = Get-Date -Format "1.yyyy.MMdd.HHmm"
    Write-Host "[3/8] Updating version to: $newVer" -ForegroundColor Yellow

    [xml]$xml = Get-Content $PropsPath
    $xml.Project.PropertyGroup.Version = $newVer
    $xml.Save($PropsPath)

    git add $PropsPath
    git commit -m "Bump version to $newVer"

    # --- 4. ブランチ操作 & GitHub 同期 ---
    Write-Host "[4/8] Syncing with GitHub & merging into main..."
    git push origin develop
    git checkout -q main
    git merge develop --no-ff -m "Release $newVer"

    # --- 5. ビルド実行 ---
    Write-Host "[5/8] Running dotnet publish..." -ForegroundColor Cyan

    if (Test-Path $PublishOutput) { Remove-Item $PublishOutput -Recurse -Force }
    dotnet publish $MainProj -c Release -o $PublishOutput -v:q --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }
    Write-Host "  OK Published" -ForegroundColor Green

    # --- 6. 配信 ---
    Write-Host "[6/8] Deploying to the target folder..."
    if (-not (Test-Path $DeployTargetDir)) {
        New-Item -Path $DeployTargetDir -ItemType Directory -Force | Out-Null
    }

    "Deployment in progress ($newVer)" | Out-File $FlagFile -Encoding ascii

    # 旧版退避 (app -> app_old)
    $appOldDir = Join-Path $DeployTargetDir "app_old"
    if (Test-Path $appOldDir) {
        Write-Host "  Cleaning old backup folder..." -ForegroundColor Gray
        $emptyDir = Join-Path $env:TEMP "naranja_empty_$(Get-Random)"
        New-Item -Path $emptyDir -ItemType Directory -Force | Out-Null
        robocopy $emptyDir $appOldDir /MIR /R:0 /W:0 /NFL /NDL /NJH /NJS | Out-Null
        Remove-Item $emptyDir -Force
        Remove-Item $appOldDir -Recurse -Force
    }

    if (Test-Path $AppPublishDir) { Rename-Item $AppPublishDir "app_old" }
    New-Item -Path $AppPublishDir -ItemType Directory | Out-Null

    # Robocopy 実行
    Write-Host "  Copying application files..." -ForegroundColor Gray
    $robocopyArgs = @("/R:0", "/W:0", "/MT:32", "/NFL", "/NDL", "/NJH", "/NJS", "/NP", "/XF", "*.pdb", "*.xml", "*.vshost.*")
    robocopy $PublishOutput $AppPublishDir $robocopyArgs | Out-Null
    Write-Host "  OK Files deployed" -ForegroundColor Green

    # deploy.json 書き出し
    $deployInfo = @{
        Version    = $newVer
        DeployDate = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    } | ConvertTo-Json
    $deployInfo | Out-File $VersionJson -Encoding utf8 -Force

    # --- 7. Git タグ & 完了処理 ---
    Write-Host "[7/8] Final push to GitHub & Tagging..."
    git push origin main
    git tag -a "v$newVer" -m "Release $newVer"
    git push origin "v$newVer"

    git checkout -q develop

    # --- 8. 後処理 ---
    if (Test-Path $FlagFile) { Remove-Item $FlagFile -Force }
    Write-Host "[8/8] Deployment completed successfully." -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    if (Test-Path $FlagFile) { Remove-Item $FlagFile -Force }
    throw
}
