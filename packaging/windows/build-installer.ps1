$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$iss = Join-Path $PSScriptRoot 'github-accelerator.iss'

$iscc = Get-Command iscc -ErrorAction SilentlyContinue
if (-not $iscc) {
  Write-Host "未找到 Inno Setup 编译器 iscc。"
  Write-Host "请先安装 Inno Setup 6: https://jrsoftware.org/isinfo.php"
  exit 1
}

Push-Location $PSScriptRoot
try {
  & $iscc.Path $iss
  Write-Host "安装包生成完成。输出目录: $root\dist"
} finally {
  Pop-Location
}
