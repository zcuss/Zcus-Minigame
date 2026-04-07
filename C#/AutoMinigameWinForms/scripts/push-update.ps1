param(
    [string]$Message = "update: auto sync"
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$RepoRoot = (git -C $ProjectRoot rev-parse --show-toplevel).Trim()
Set-Location $RepoRoot

if (-not (git rev-parse --is-inside-work-tree 2>$null)) {
    throw "Folder ini bukan git repository: $RepoRoot"
}

$repoUri = [Uri]((Resolve-Path $RepoRoot).Path.TrimEnd('\') + '\')
$projectUri = [Uri]((Resolve-Path $ProjectRoot).Path.TrimEnd('\') + '\')
$projectRelative = [Uri]::UnescapeDataString($repoUri.MakeRelativeUri($projectUri).ToString()).TrimEnd('/')

# Hanya stage file dalam folder project ini.
$null = git add -A -- "$projectRelative/**" `
    ":(exclude)$projectRelative/**/bin/**" `
    ":(exclude)$projectRelative/**/obj/**" `
    ":(exclude)$projectRelative/**/publish/**"

$hasChanges = git diff --cached --name-only
if ([string]::IsNullOrWhiteSpace($hasChanges)) {
    Write-Host "Tidak ada perubahan untuk di-push."
    exit 0
}

try {
    git commit -m $Message
}
catch {
    throw "Gagal commit. Cek git user.name/user.email atau konflik commit."
}

git push -u origin Main
Write-Host "Push selesai ke origin/Main"
