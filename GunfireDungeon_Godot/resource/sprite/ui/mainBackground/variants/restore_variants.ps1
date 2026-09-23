# 主菜单随机背景（10 套视差层）—— 本机补齐脚本
#
# 为什么需要它：
#   CraftPix 免费素材条款 §2.2.1 禁止再分发美术源文件，而本仓库是公开仓库，
#   所以这些 PNG 被 .gitignore 排除、不在 git 里。发行版打包时它们从本机磁盘进 pck，
#   但换一台机器 clone 下来就没有了 —— 跑这个脚本从素材目录补回来。
#
# 它做什么：
#   每个素材包里有 3~5 个编号 PNG（1.png / 2.png / ...），那是【视差分层】，
#   不是缩略图。1 = 最远（通常是星空/底色），编号越大越靠前。
#   把这些层复制成 bg_vNN_L1.png / bg_vNN_L2.png ... （10 个包共 42 个文件）。
#
# 用法：
#   pwsh -File restore_variants.ps1
#   pwsh -File restore_variants.ps1 -SourceRoot "D:\某处\可用素材_待接入\✅_明确可用_itch与爱给网"
#
# 补完之后记得让 Godot 重新导入一次：
#   Godot_v4.7.1-stable_mono_win64_console.exe --headless --import --path <项目目录>

param(
    [string]$SourceRoot = "C:\Users\WY157\Desktop\GODOT\可用素材_待接入\✅_明确可用_itch与爱给网"
)

$ErrorActionPreference = "Stop"
$dest = $PSScriptRoot

if (-not (Test-Path $SourceRoot)) {
    Write-Host "找不到素材目录: $SourceRoot" -ForegroundColor Red
    Write-Host "用 -SourceRoot 指定 '可用素材_待接入\✅_明确可用_itch与爱给网' 所在位置。"
    exit 1
}

# 顺序必须和 MainBackground.cs 里的 VariantPacks 完全一致
$map = @(
    @{ V = "01"; Part = 1; Bg = 1 },
    @{ V = "02"; Part = 1; Bg = 2 },
    @{ V = "03"; Part = 1; Bg = 3 },
    @{ V = "04"; Part = 2; Bg = 1 },
    @{ V = "05"; Part = 2; Bg = 2 },
    @{ V = "06"; Part = 2; Bg = 3 },
    @{ V = "07"; Part = 2; Bg = 4 },
    @{ V = "08"; Part = 3; Bg = 2 },
    @{ V = "09"; Part = 4; Bg = 1 },
    @{ V = "10"; Part = 4; Bg = 2 }
)

# 旧版用的是压平图 orig_big.png，现在改成视差层，把它清掉免得混淆
$stale = @("bg_v01.png", "bg_v02.png", "bg_v03.png", "bg_v04.png", "bg_v05.png",
           "bg_v06.png", "bg_v07.png", "bg_v08.png", "bg_v09.png", "bg_v10.png")
foreach ($f in $stale) {
    $p = Join-Path $dest $f
    if (Test-Path $p) { Remove-Item $p -Force; Write-Host "删除旧压平图  $f" -ForegroundColor DarkGray }
}
Get-ChildItem $dest -Filter "*.png.import" -ErrorAction SilentlyContinue | ForEach-Object {
    if ($_.Name -match '^bg_v\d\d\.png\.import$') { Remove-Item $_.FullName -Force }
}

$ok = 0
$miss = 0
foreach ($item in $map) {
    $dir = Join-Path $SourceRoot "New free backgrounds part$($item.Part)\background $($item.Bg)"
    if (-not (Test-Path $dir)) {
        Write-Host "找不到素材目录: $dir" -ForegroundColor Yellow
        $miss++
        continue
    }

    # 编号 PNG = 视差层，按编号顺序复制
    $layers = Get-ChildItem $dir -Filter "*.png" |
        Where-Object { $_.BaseName -match '^\d+$' } |
        Sort-Object { [int]$_.BaseName }

    if ($layers.Count -eq 0) {
        Write-Host "part$($item.Part)/background $($item.Bg) 里没有编号图层" -ForegroundColor Yellow
        $miss++
        continue
    }

    $i = 0
    $names = @()
    foreach ($layer in $layers) {
        $i++
        $out = "bg_v$($item.V)_L$i.png"
        Copy-Item $layer.FullName (Join-Path $dest $out) -Force
        $names += $out
    }
    Write-Host ("v{0}  <- part{1}/background {2}  ({3} 层: {4})" -f `
        $item.V, $item.Part, $item.Bg, $layers.Count, ($names -join ", "))
    $ok += $layers.Count
}

Write-Host ""
Write-Host ("完成: 复制 {0} 个图层文件, 有 {1} 个包缺失。" -f $ok, $miss)
if ($miss -eq 0) {
    Write-Host "接着让 Godot 重新导入一次（见本文件顶部注释），否则 .import 对不上，运行时会回退到视差大厅。"
}
