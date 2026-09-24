<#
.SYNOPSIS
    还原「不死刽子手」+「刽子手的幽灵」的精灵图与图标。

.DESCRIPTION
    为什么要这个脚本：
      素材来自 itch 页面 https://darkpixel-kronovi.itch.io/undead-executioner
      （作者 Kronovi-）。页面授权允许商用、允许修改、不要求署名，但明确写了
      "redistributing and reselling the sprite are restricted" —— **禁止再分发**。
      本仓库是公开仓库，所以所有 PNG 被 .gitignore 排除，只随打包发行进 pck。

    这个脚本做两件事：
      1. 把原作图集**复制 + 改名**（本项目对素材像素没有任何改动）；
      2. 用第 0 帧生成 64x64 的图册图标（裁掉透明边、居中放到 64x64 画布上）。

    只用 .NET 的 System.Drawing，**不依赖 Python / Pillow / uv**。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File restore_sprites.ps1
#>
param(
    [string]$SourceDir = "C:\Users\WY157\Desktop\GODOT\可用素材_待接入\✅_明确可用_itch与爱给网\Undead executioner\Undead executioner puppet\png"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$RoleDir = Split-Path $PSScriptRoot -Parent
$ExecDir = Join-Path $RoleDir "executioner0001"
$SpiritDir = Join-Path $RoleDir "spirit0001"

if (-not (Test-Path -LiteralPath $SourceDir)) {
    Write-Host "找不到素材原始目录:" -ForegroundColor Red
    Write-Host "  $SourceDir"
    Write-Host "请用 -SourceDir 指定 itch 下载包里那个 png 目录的实际路径。"
    exit 1
}

New-Item -ItemType Directory -Force -Path $ExecDir, $SpiritDir | Out-Null

# 原作文件名 -> (目标目录, 目标文件名, 格子边长)
$map = [ordered]@{
    "idle.png"         = @($ExecDir,   "ExecutionerIdle.png",   100)
    "idle2.png"        = @($ExecDir,   "ExecutionerMove.png",   100)
    "attacking.png"    = @($ExecDir,   "ExecutionerAttack.png", 100)
    "skill1.png"       = @($ExecDir,   "ExecutionerSweep.png",  100)
    "summon.png"       = @($ExecDir,   "ExecutionerSummon.png", 100)
    "death.png"        = @($ExecDir,   "ExecutionerDeath.png",  100)
    "summonIdle.png"   = @($SpiritDir, "SpiritIdle.png",         50)
    "summonAppear.png" = @($SpiritDir, "SpiritAppear.png",       50)
    "summonDeath.png"  = @($SpiritDir, "SpiritDeath.png",        50)
}

$n = 0
foreach ($src in $map.Keys) {
    $from = Join-Path $SourceDir $src
    if (-not (Test-Path -LiteralPath $from)) {
        Write-Host "  跳过（源文件不存在）: $src" -ForegroundColor Yellow
        continue
    }
    $to = Join-Path $map[$src][0] $map[$src][1]
    Copy-Item -LiteralPath $from -Destination $to -Force
    Write-Host ("  {0,-20} -> {1}" -f $src, (Split-Path $to -Leaf)) -ForegroundColor Green
    $n++
}

<#
    用第 0 帧（左上角那个 cell x cell 的格子）生成 64x64 图标：
    先按 alpha 裁掉透明边, 再居中贴到 64x64 的透明画布上。
#>
function New-Icon {
    param([string]$SrcPng, [int]$Cell, [string]$OutPng)

    if (-not (Test-Path -LiteralPath $SrcPng)) { return $false }

    $src = [System.Drawing.Bitmap]::FromFile($SrcPng)
    try {
        if ($Cell -gt $src.Width -or $Cell -gt $src.Height) {
            Write-Host "  ${OutPng}: 源图比格子还小, 跳过" -ForegroundColor Yellow
            return $false
        }

        # 第 0 帧的不透明包围盒
        $minX = $Cell; $minY = $Cell; $maxX = -1; $maxY = -1
        for ($y = 0; $y -lt $Cell; $y++) {
            for ($x = 0; $x -lt $Cell; $x++) {
                if ($src.GetPixel($x, $y).A -gt 8) {
                    if ($x -lt $minX) { $minX = $x }
                    if ($x -gt $maxX) { $maxX = $x }
                    if ($y -lt $minY) { $minY = $y }
                    if ($y -gt $maxY) { $maxY = $y }
                }
            }
        }
        if ($maxX -lt 0) {
            Write-Host "  ${OutPng}: 第 0 帧是空白, 跳过" -ForegroundColor Yellow
            return $false
        }

        $w = $maxX - $minX + 1
        $h = $maxY - $minY + 1
        $canvas = New-Object 'System.Drawing.Bitmap' 64, 64
        try {
            $g = [System.Drawing.Graphics]::FromImage($canvas)
            try {
                $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                $dstRect = New-Object 'System.Drawing.Rectangle' ([int]([Math]::Floor((64 - $w) / 2))), ([int]([Math]::Floor((64 - $h) / 2))), $w, $h
                $srcRect = New-Object 'System.Drawing.Rectangle' $minX, $minY, $w, $h
                $g.DrawImage($src, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally { $g.Dispose() }

            $canvas.Save($OutPng, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $canvas.Dispose() }

        Write-Host ("  {0,-20} <- 第0帧 {1}x{2} 居中" -f (Split-Path $OutPng -Leaf), $w, $h) -ForegroundColor Green
        return $true
    }
    finally { $src.Dispose() }
}

Write-Host ""
Write-Host "生成图标..." -ForegroundColor Cyan
$okExec = New-Icon (Join-Path $ExecDir "ExecutionerIdle.png") 100 (Join-Path $ExecDir "Executioner_Icon.png")
$okSpirit = New-Icon (Join-Path $SpiritDir "SpiritIdle.png") 50 (Join-Path $SpiritDir "Spirit_Icon.png")

Write-Host ""
Write-Host "已还原 $n 个精灵图（图标: 刽子手 $(if($okExec){'OK'}else{'失败'}), 幽灵 $(if($okSpirit){'OK'}else{'失败'})）。" -ForegroundColor Cyan
Write-Host ""
Write-Host "还原后让 Godot 重新导入一次："
Write-Host "  Godot_v4.7.1-stable_mono_win64_console.exe --headless --import --path <项目目录>"
