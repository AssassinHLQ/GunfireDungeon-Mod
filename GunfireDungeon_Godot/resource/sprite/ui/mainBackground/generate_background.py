"""
主菜单视差背景 —— 拱窗版

构图: 地牢石墙大厅, 左右各一扇拱窗, 窗外是黄昏天空与远山
  天空/远山在墙后缓慢漂移 -> 透过拱窗可见, 形成视差
  墙体(砌石)遮住大部分画面 => 白字与按钮边框天然清晰, 不需要遮罩

产出:
  bg_sky.png    3840x1080 黄昏天空+云   (CC0 ansimuz / Sunny Land, 已调色)
  bg_ridge.png  1920x1080 远山剪影      (程序化)
  bg_wall.png   1920x1080 砌石墙+拱窗   (程序化, 窗内透明)
"""
import os
import numpy as np
from PIL import Image, ImageFilter

OUT = r"C:\Users\WY157\Desktop\GODOT\_bg_build\out"
SKY_SRC = r"C:\Users\WY157\Desktop\GODOT\UI素材\_背景候选\sunny-land\Sunny-land-assets-files\PNG\environment\layers\back.png"

W, H = 1920, 1080
SKY_CROP = 112
SKY_H = SKY_CROP * 5
rng = np.random.default_rng(90210)

MORTAR = np.array((19, 17, 26), np.float32)
BRICK = np.array((44, 40, 56), np.float32)
BRICK2 = np.array((35, 32, 46), np.float32)
HI = np.array((60, 55, 76), np.float32)
TRIM = np.array((88, 82, 110), np.float32)

# (x0, x1, 拱顶y, 起拱y, 窗底y)
# 窗底刻意保持在 800 以下, 避开左下角社交链接 (y>=828) 与右下角版本号
ARCHES = [(150, 640, 205, 385, 800), (1280, 1770, 252, 425, 780)]


def periodic_fbm(w, h, base=5, octaves=6):
    out = np.zeros((h, w), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        cx = base * (2 ** o)
        cy = max(2, int(cx * h / w))
        blk = rng.random((cy, cx)).astype(np.float32)
        big = np.tile(blk, (2, 2))
        img = Image.fromarray((big * 255).astype(np.uint8)).resize((w * 2, h * 2), Image.BICUBIC)
        a = np.asarray(img, np.float32) / 255.0
        out += amp * a[h // 2:h // 2 + h, w // 2:w // 2 + w]
        tot += amp
        amp *= 0.52
    out /= tot
    lo, hi = out.min(), out.max()
    return (out - lo) / max(hi - lo, 1e-6)


def rand_bumps(count, lo, hi, w_range, d_range):
    return [(float(rng.uniform(lo, hi)), float(rng.uniform(*w_range)), float(rng.uniform(*d_range)))
            for _ in range(count)]


def bump_stack(n, items, period):
    x = np.arange(n, dtype=np.float32)
    L = np.stack([dep * 0.5 * (1.0 + np.cos(np.pi * np.clip(
        ((x - x0 + period / 2) % period - period / 2) / max(wid, 1.0), -1, 1)))
        for x0, wid, dep in items])
    return L.max(axis=0)


def soft_glow_alpha(mask, grow=6, blur=8, strength=0.5, inside=False):
    m = Image.fromarray((mask * 255).astype(np.uint8))
    g = m
    for _ in range(grow):
        g = g.filter(ImageFilter.MaxFilter(3))
    g = np.asarray(g.filter(ImageFilter.GaussianBlur(blur)), np.float32) / 255.0
    base = mask.astype(np.float32) if inside else 1.0 - mask.astype(np.float32)
    return np.clip(np.clip(g, 0, 1) * base * strength, 0, 1)


def masonry(w, h, bw=64, bh=27):
    xs, ys = np.arange(w)[None, :], np.arange(h)[:, None]
    row = ys // bh
    off = (row % 2) * (bw // 2)
    bx, by = (xs + off) % bw, ys % bh
    col = (xs + off) // bw
    hsh = ((col * 73856093) ^ (row * 19349663)) % 1000 / 1000.0
    face = BRICK[None, None, :] * (1 + (hsh - 0.5)[..., None] * 0.40) + \
           BRICK2[None, None, :] * (0.5 - (hsh - 0.5)[..., None] * 0.40)
    out = np.tile(MORTAR[None, None, :], (h, w, 1))
    out = np.where(((bx > 1) & (by > 1))[..., None], face, out)
    out = np.where(((by == 2) & (bx > 1))[..., None], HI[None, None, :], out)
    return out


def build_sky():
    src = Image.open(SKY_SRC).convert("RGB").crop((0, 0, 384, SKY_CROP))
    tile = src.resize((1920, SKY_H), Image.NEAREST)
    canvas = Image.new("RGB", (3840, SKY_H))
    canvas.paste(tile, (0, 0))
    canvas.paste(tile.transpose(Image.FLIP_LEFT_RIGHT), (1920, 0))

    a = np.asarray(canvas).astype(np.float32) / 255.0
    lum = a.mean(axis=2, keepdims=True)
    # 黄昏: 保留一点饱和度, 偏紫蓝
    a = (lum * 0.20 + a * 0.80) * np.array([0.74, 0.64, 1.05]) * 0.96
    vg = np.linspace(0.60, 1.06, SKY_H)
    a = np.clip(a * vg[:, None, None], 0, 1)

    warm = np.clip((np.arange(SKY_H) - SKY_H * 0.58) / (SKY_H * 0.42), 0, 1) ** 1.8
    a = a * (1 - 0.38 * warm[:, None, None]) + \
        np.array([0.62, 0.40, 0.46], np.float32)[None, None, :] * (0.38 * warm[:, None, None])

    ext = np.repeat(a[-1:], H - SKY_H, axis=0) * np.linspace(1.0, 0.5, H - SKY_H)[:, None, None]
    return Image.fromarray(np.clip(np.concatenate([a, ext], 0) * 255, 0, 255).astype(np.uint8))


def build_ridge():
    rgba = np.zeros((H, W, 4), np.float32)
    tex = periodic_fbm(W, H, base=6, octaves=6)
    yy = np.arange(H)[:, None]
    wob = (tex.mean(axis=0) - 0.5) * 24.0
    for base_y, cfg, c_top, c_bot, glow, fall in (
        (620, dict(count=8, w_range=(260, 540), d_range=(34, 100)), (78, 74, 106), (44, 42, 66), (104, 104, 140), 320),
        (690, dict(count=10, w_range=(210, 470), d_range=(30, 96)), (48, 45, 70), (24, 23, 36), (70, 68, 96), 300),
    ):
        prof = base_y + bump_stack(W, rand_bumps(lo=0, hi=W, **cfg), W) + wob
        mask = yy >= prof[None, :]
        t = np.clip((yy - prof[None, :]) / fall, 0, 1)
        grad = (1 - t)[..., None] * np.array(c_top, np.float32) + t[..., None] * np.array(c_bot, np.float32)
        grad = grad * (1.0 + (tex - 0.5) * 0.28)[..., None]
        rgba[mask, :3] = np.clip(grad[mask], 0, 255)
        rgba[mask, 3] = 255
        g = soft_glow_alpha(mask, 4, 6, 0.0)
        ga = g[..., None] * np.array(glow, np.float32)[None, None, :]
        aa = np.clip(ga.max(axis=2), 0, 255)
        sel = (aa > 1) & ~mask
        rgba[sel, :3], rgba[sel, 3] = ga[sel], aa[sel]
    return Image.fromarray(np.clip(rgba, 0, 255).astype(np.uint8))


def build_wall():
    rgba = np.zeros((H, W, 4), np.float32)
    yy, xx = np.arange(H)[:, None], np.arange(W)[None, :]

    # 拱窗开口
    opening = np.zeros((H, W), bool)
    for x0, x1, ytop, yspring, ybot in ARCHES:
        cx, hw = (x0 + x1) / 2.0, (x1 - x0) / 2.0
        ah = yspring - ytop
        dx = np.clip((xx - cx) / hw, -1, 1)
        yarc = yspring - ah * np.sqrt(np.clip(1 - dx ** 2, 0, 1))
        opening |= (xx >= x0) & (xx <= x1) & (yy >= yarc) & (yy <= ybot)

    tex = periodic_fbm(W, H, base=8, octaves=6)
    stone = masonry(W, H) * (0.80 + tex[..., None] * 0.40)

    # 墙面受窗光影响: 越靠窗口越亮
    d = np.full((H, W), 1e6, np.float32)
    for x0, x1, ytop, yspring, ybot in ARCHES:
        cx, hw = (x0 + x1) / 2.0, (x1 - x0) / 2.0
        dx = np.clip((xx - cx) / hw, -1, 1)
        yarc = yspring - (yspring - ytop) * np.sqrt(np.clip(1 - dx ** 2, 0, 1))
        inside_x = (xx >= x0) & (xx <= x1)
        dy = np.where(yy < yarc, yarc - yy, np.where(yy > ybot, yy - ybot, 0.0))
        dxo = np.maximum(np.maximum(x0 - xx, xx - x1), 0.0)
        dist = np.where(inside_x, dy, np.sqrt(dxo ** 2 + np.maximum(dy, 0) ** 2))
        d = np.minimum(d, dist)
    shade = np.clip(0.62 + np.clip(d, 0, 600) / 600.0 * -0.30 + 0.34, 0.62, 1.0)
    rgb = stone * shade[..., None]

    rgba[~opening, :3] = np.clip(rgb[~opening], 0, 255)
    rgba[~opening, 3] = 255

    # 窗光洒在墙上 (墙内侧柔光)
    g = soft_glow_alpha(opening, grow=14, blur=24, strength=0.26, inside=True)
    ga = g[..., None] * np.array((120, 118, 160), np.float32)[None, None, :]
    aa = np.clip(ga.max(axis=2), 0, 255)
    sel = (aa > 1) & ~opening
    rgba[sel, :3] = np.clip(rgba[sel, :3] + ga[sel] * 0.45, 0, 255)

    # 拱券石框
    om = Image.fromarray((opening * 255).astype(np.uint8))
    grown = om
    for _ in range(9):
        grown = grown.filter(ImageFilter.MaxFilter(3))
    trim = (np.asarray(grown) > 127) & ~opening
    shade_t = 0.90 + tex * 0.50
    rgba[trim, :3] = np.clip(TRIM[None, :] * shade_t[trim][:, None], 0, 255)
    rgba[trim, 3] = 255

    # 窗台石 (比窗略宽的凸出石台)
    for x0, x1, ytop, yspring, ybot in ARCHES:
        sy0, sy1 = ybot, ybot + 26
        sx0, sx1 = x0 - 34, x1 + 34
        sm = (yy >= sy0) & (yy <= sy1) & (xx >= sx0) & (xx <= sx1)
        rgba[sm, :3] = np.clip(TRIM[None, :] * (0.92 + tex[sm][:, None] * 0.5), 0, 255)
        rgba[sm, 3] = 255
        cap = (yy >= sy0) & (yy <= sy0 + 3) & (xx >= sx0) & (xx <= sx1)   # 台面高光
        rgba[cap, :3] = np.clip(TRIM[None, :] * 1.30, 0, 255)

    # 底部地面 / 基座
    floor_y = 1006 + (tex.mean(axis=0) - 0.5) * 8.0
    fl = yy[:, 0][:, None] >= floor_y[None, :]
    rgba[fl, :3] = np.clip(stone[fl] * 0.80, 0, 255)
    rgba[fl, 3] = 255
    lip = (yy[:, 0][:, None] >= floor_y[None, :] - 5) & (yy[:, 0][:, None] <= floor_y[None, :] + 3)
    rgba[lip, :3] = np.clip(TRIM[None, :] * 1.18, 0, 255)
    rgba[lip, 3] = 255

    # 顶部檐口 (让石墙读作建筑而非壁纸)
    full = np.ones((H, W), bool)
    cor = ((yy >= 74) & (yy <= 96)) & full
    rgba[cor, :3] = np.clip(TRIM[None, :] * (0.95 + tex[cor][:, None] * 0.45), 0, 255)
    rgba[cor, 3] = 255
    lip2 = ((yy >= 97) & (yy <= 102)) & full
    rgba[lip2, :3] = np.clip(stone[lip2] * 0.42, 0, 255)
    rgba[lip2, 3] = 255
    up = (yy <= 20) & full
    rgba[up, :3] = np.clip(stone[up] * 0.62, 0, 255)
    rgba[up, 3] = 255

    # 四角压暗
    vig_x = np.clip(np.abs(xx - W / 2) / (W / 2), 0, 1) ** 3
    vig_y = np.clip(np.abs(yy - H / 2) / (H / 2), 0, 1) ** 3
    v = np.clip(1.0 - 0.38 * (vig_x + vig_y), 0.55, 1.0)
    m = rgba[..., 3] > 0
    rgba[m, :3] = np.clip(rgba[m, :3] * v[m][:, None], 0, 255)

    # 边缘受光 (拱窗内沿, 极轻的一道勾边)
    inner = soft_glow_alpha(opening, grow=2, blur=2, strength=0.16, inside=False)
    ii = inner > 0.06
    rgba[ii, :3] = np.clip(rgba[ii, :3] + inner[ii][:, None] * np.array((150, 150, 190), np.float32), 0, 255)
    return Image.fromarray(np.clip(rgba, 0, 255).astype(np.uint8))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    sky, ridge, wall = build_sky(), build_ridge(), build_wall()
    sky.save(os.path.join(OUT, "bg_sky.png"))

    # 远山镜像拼成 3840 宽, 与天空周期一致, 便于在 Godot 里统一循环
    r2 = Image.new("RGBA", (3840, H))
    r2.paste(ridge, (0, 0))
    r2.paste(ridge.transpose(Image.FLIP_LEFT_RIGHT), (1920, 0))
    r2.save(os.path.join(OUT, "bg_ridge.png"))
    wall.save(os.path.join(OUT, "bg_wall.png"))

    mock = Image.new("RGB", (W, H), (8, 8, 12))
    mock.paste(sky, (0, 0))
    mock.paste(ridge, (0, 0), ridge)
    mock.paste(wall, (0, 0), wall)
    mock.save(os.path.join(OUT, "mock_menu.png"))

    for f in ("bg_sky.png", "bg_ridge.png", "bg_wall.png", "mock_menu.png"):
        p = os.path.join(OUT, f)
        print(f"{f:16} {str(Image.open(p).size):14} {os.path.getsize(p)/1024:6.0f} KB")
