# ====================================================================
# Push to GitHub Script for Red Lion Racing SolidWorks Add-in
# Repository: https://github.com/WiteLEoparD/Tube-Coping-Template-Plugin-for-Solidworks
# ====================================================================

$repoUrl = "https://github.com/WiteLEoparD/Tube-Coping-Template-Plugin-for-Solidworks.git"

Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host " Pushing Project to GitHub Repository" -ForegroundColor Cyan
Write-Host " Target: $repoUrl" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan

# 1. Check if git is available in PATH or common directories
$gitCmd = Get-Command git -ErrorAction SilentlyContinue

if (-not $gitCmd) {
    # Check common locations
    $candidates = @(
        "C:\Program Files\Git\cmd\git.exe",
        "C:\Program Files\Git\bin\git.exe",
        "$env:LOCALAPPDATA\Programs\Git\cmd\git.exe",
        "C:\Program Files (x86)\Git\cmd\git.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) {
            $gitCmd = $c
            break
        }
    }
}

if (-not $gitCmd) {
    Write-Host "`n[!] Git is not currently installed or not found in PATH." -ForegroundColor Yellow
    Write-Host "To push your files to GitHub, please install Git using one of the following methods:" -ForegroundColor Yellow
    Write-Host "  1. Download and run the installer: https://git-scm.com/download/win" -ForegroundColor White
    Write-Host "  2. Or using winget in Command Prompt: winget install --id Git.Git -e --source winget" -ForegroundColor White
    Write-Host "  3. Or use GitHub Desktop (File > Add Local Repository): https://desktop.github.com" -ForegroundColor White
    Write-Host "`nOnce Git is installed, re-run this script to automatically push to your repository.`n" -ForegroundColor Yellow
    pause
    exit 1
}

$git = if ($gitCmd -is [string]) { $gitCmd } else { $gitCmd.Source }
Write-Host "[✓] Using Git: $git" -ForegroundColor Green

# 2. Initialize repository if .git directory does not exist
if (-not (Test-Path ".git")) {
    Write-Host "`n[1/5] Initializing local git repository..." -ForegroundColor Yellow
    & $git init
} else {
    Write-Host "`n[1/5] Existing git repository detected." -ForegroundColor Green
}

# 3. Configure remote origin
Write-Host "`n[2/5] Configuring remote origin..." -ForegroundColor Yellow
$remotes = & $git remote
if ($remotes -contains "origin") {
    & $git remote set-url origin $repoUrl
} else {
    & $git remote add origin $repoUrl
}
Write-Host "[✓] Remote set to: $repoUrl" -ForegroundColor Green

# 4. Stage all project files (respected by .gitignore)
Write-Host "`n[3/5] Staging project files..." -ForegroundColor Yellow
& $git add .

# 5. Commit
Write-Host "`n[4/5] Creating commit..." -ForegroundColor Yellow
$status = & $git status --porcelain
if ($status) {
    & $git commit -m "Initial release: Red Lion Racing 1:1 Tube Cut Template Generator for SolidWorks"
} else {
    Write-Host "No new changes to commit." -ForegroundColor Cyan
}

# 6. Branch and push
Write-Host "`n[5/5] Pushing to main branch on GitHub..." -ForegroundColor Yellow
& $git branch -M main
& $git push -u origin main

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n====================================================================" -ForegroundColor Green
    Write-Host " SUCCESS! Project pushed to: $repoUrl" -ForegroundColor Green
    Write-Host "====================================================================" -ForegroundColor Green
} else {
    Write-Host "`n[!] Push completed with exit code $LASTEXITCODE." -ForegroundColor Yellow
    Write-Host "If authentication was required, please sign in with your GitHub credentials or Personal Access Token (PAT)." -ForegroundColor White
}
