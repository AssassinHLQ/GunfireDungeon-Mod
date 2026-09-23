# 主菜单随机背景（10 张）—— 本机补齐脚本
#
# 为什么需要它：
#   CraftPix 免费素材条款 §2.2.1 禁止再分发美术源文件，而本仓库是公开仓库，
#   所以这 10 张图被 .gitignore 排除、不在 git 里。发行版打包时它们从本机磁盘进 pck，
#   但换一台机器 clone 下来就没有了 —— 跑这个脚本从素材目录补回来。
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

# 顺序必须和 MainBackground.cs 里的 VariantPaths 完全一致
$map = @(
    @{ File = "bg_v01.png"; Part = 1; Bg = 1 },
    @{ File = "bg_v02.png"; Part = 1; Bg = 2 },
    @{ File = "bg_v03.png"; Part = 1; Bg = 3 },
    @{ File = "bg_v04.png"; Part = 2; Bg = 1 },
    @{ File = "bg_v05.png"; Part = 2; Bg = 2 },
    @{ File = "bg_v06.png"; Part = 2; Bg = 3 },
    @{ File = "bg_v07.png"; Part = 2; Bg = 4 },
    @{ File = "bg_v08.png"; Part = 3; Bg = 2 },
    @{ File = "bg_v09.png"; Part = 4; Bg = 1 },
    @{ File = "bg_v10.png"; Part = 4; Bg = 2 }
)

$ok = 0
$miss = 0
foreach ($item in $map) {
    $src = Join-Path $SourceRoot "New free backgrounds part$($item.Part)\background $($item.Bg)\orig_big.png"
    $dst = Join-Path $dest $item.File

    if (-not (Test-Path $src)) {
        Write-Host ("缺失  {0}  <- {1}" -f $item.File, $src) -ForegroundColor Yellow
        $miss++
        continue
    }

    Copy-Item $src $dst -Force
    $size = [math]::Round((Get-Item $dst).Length / 1KB, 1)
    Write-Host ("复制  {0}  <- part{1}/background {2}  ({3} KB)" -f $item.File, $item.Part, $item.Bg, $size)
    $ok++
}

Write-Host ""
Write-Host ("完成: 复制 {0} 个, 缺失 {1} 个。" -f $ok, $miss)
if ($miss -eq 0) {
    Write-Host "接着让 Godot 重新导入一次（见本文件顶部注释），否则 .import 对不上，运行时会回退到视差大厅。"
}
