<#
.SYNOPSIS
    重建 AK47 的武器贴图 resource/sprite/weapon/weapon0047/AK47.png

.DESCRIPTION
    为什么需要这个脚本:
      AK47 的贴图来自 CraftPix 免费素材包「Free Guns Icon 32x32 Pixel Pack」里的
      Icon29_37.png。CraftPix 免费素材条款(https://craftpix.net/file-licenses/) §2.2.1
      禁止【再分发美术源文件或其修改版】, 而本仓库是公开仓库, 所以 AK47.png 被
      .gitignore 排除, 只随打包发行进 pck。

    这个脚本做两件事:
      1. 把 3/4 俯视的 32x32 图标顺时针旋转 39.4 度, 转成本游戏武器通用的
         "侧视 + 枪口朝右" 朝向;
      2. 裁掉四周透明边, 输出 38x15 的 AK47.png。

    旋转公式用逆映射 + 最近邻取样, 和 Python PIL
    `Image.rotate(-39.4, resample=NEAREST, expand=True)` 的结果一致。
    只用 .NET 的 System.Drawing, 不依赖 Python / Pillow / uv。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File restore_ak47.ps1
#>
param(
    [string]$SourceIcon = "C:\Users\WY157\Desktop\GODOT\可用素材_待接入\✅_明确可用_itch与爱给网\Free-Guns-Icon-32x32-Pixel-Pack\1 Icons\Icon29_37.png"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$Deg = 39.4
$OutPath = Join-Path $PSScriptRoot "AK47.png"

if (-not (Test-Path -LiteralPath $SourceIcon)) {
    Write-Host "找不到源图标:" -ForegroundColor Red
    Write-Host "  $SourceIcon"
    Write-Host "请用 -SourceIcon 指定 CraftPix 包里 1 Icons\Icon29_37.png 的实际路径。"
    exit 1
}

$src = [System.Drawing.Bitmap]::FromFile($SourceIcon)
try {
    $w = $src.Width
    $h = $src.Height

    # 先把源图读进内存, 避免下面反复调用 GetPixel
    $srcPix = New-Object 'System.Drawing.Color[,]' $w, $h
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $srcPix[$x, $y] = $src.GetPixel($x, $y)
        }
    }

    $rad = $Deg * [Math]::PI / 180.0
    $cos = [Math]::Cos($rad)
    $sin = [Math]::Sin($rad)

    # 画布留够余量, 旋转后再按不透明像素裁边
    $dw = $w * 2
    $dh = $h * 2
    $cx = $dw / 2.0
    $cy = $dh / 2.0
    $scx = $w / 2.0
    $scy = $h / 2.0

    $dst = New-Object 'System.Drawing.Bitmap' $dw, $dh
    try {
        for ($y = 0; $y -lt $dh; $y++) {
            for ($x = 0; $x -lt $dw; $x++) {
                # 目标点相对画布中心的偏移
                $ox = $x - $cx
                $oy = $y - $cy
                # 上式是"顺时针旋转"的正向映射, 这里做逆映射回到源图坐标
                $sx = $ox * $cos + $oy * $sin + $scx
                $sy = -$ox * $sin + $oy * $cos + $scy

                $px = [int][Math]::Floor($sx + 0.5)
                $py = [int][Math]::Floor($sy + 0.5)
                if ($px -ge 0 -and $px -lt $w -and $py -ge 0 -and $py -lt $h) {
                    $c = $srcPix[$px, $py]
                    if ($c.A -gt 0) { $dst.SetPixel($x, $y, $c) }
                }
            }
        }

        # 按 alpha > 0 裁边
        $minX = $dw; $minY = $dh; $maxX = -1; $maxY = -1
        for ($y = 0; $y -lt $dh; $y++) {
            for ($x = 0; $x -lt $dw; $x++) {
                if ($dst.GetPixel($x, $y).A -gt 0) {
                    if ($x -lt $minX) { $minX = $x }
                    if ($x -gt $maxX) { $maxX = $x }
                    if ($y -lt $minY) { $minY = $y }
                    if ($y -gt $maxY) { $maxY = $y }
                }
            }
        }
        if ($maxX -lt 0) { throw "旋转后没有任何不透明像素, 源图可能读错了。" }

        $rw = $maxX - $minX + 1
        $rh = $maxY - $minY + 1
        $rect = New-Object 'System.Drawing.Rectangle' $minX, $minY, $rw, $rh
        $out = $dst.Clone($rect, $dst.PixelFormat)
        try {
            $out.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $out.Dispose() }

        Write-Host "已生成 $OutPath  ($rw x $rh)" -ForegroundColor Green
        Write-Host "源图标: $SourceIcon"
    }
    finally { $dst.Dispose() }
}
finally { $src.Dispose() }
