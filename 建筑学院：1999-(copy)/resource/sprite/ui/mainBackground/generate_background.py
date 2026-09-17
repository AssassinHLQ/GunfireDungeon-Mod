"""
主菜单视差背景生成器 (自包含, 不依赖其他文件)

产出:
  bg_sky.png    3840x1080  黄昏天空与云  (CC0: ansimuz / Sunny Land, 已调色)
  bg_ridge.png  3840x1080  远山剪影      (程序化)
  bg_wall.png   1920x1080  砖墙 + 三扇拱窗 (程序化, 窗内透明)

砖墙的做法:
  · 每行高度随机、每块砖宽度随机、每块砖的颜色/明暗/色相都不同, 还带缺角与污渍
    —— 真实砖墙上没有两块一样的砖
  · 拱窗由楔形券石沿半径放射排列砌成(真实砖拱的砌法), 不是一条光滑曲线
  · 券石比普通砖亮一档, 块与块之间有可见的放射状灰缝

需要 Pillow 与 NumPy。天空那一步依赖外部 CC0 素材, 见同目录 LICENSE.md。
"""
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
TRIM = np.array((88, 82, 110), np.float32)

# (x0, x1, 拱顶y, 起拱y, 窗底y)
# 三扇拱窗, 尺寸完全一致, 均匀分布。
# 窗底守在 795, 窗台到 821, 避开左下角社交链接(y>=828)
_ARCH_W = 470
_ARCH_GAP = 120

# 窗口两侧壁柱的宽度。
# 原来是 48, 用户反馈"竖砖太宽了不好看"。
# 收窄到 28 —— 拱窗宽 470, 48 的壁柱占了窗口宽度的 10%,
# 看着像两根粗柱子而不是"贴在墙面上的壁柱"。
_PILASTER_W = 28

# 留白【保持原样】, 不要在这里加减任何东西。
#
# 【踩坑记录】我在这里连错两次:
#   第一版写成 ... // 2 + (48 - 28)  -> 留白 155, 三扇窗整体右移 20px
#   第二版写成 ... // 2 - (48 - 28)  -> 留白 115, 三扇窗整体左移 20px
# 正确做法是【什么都不加】:
#   壁柱长在窗口【外侧】, 占用的是"窗口到画布边"那段留白里的一部分。
#   留白不变、壁柱变窄, 结果是壁柱外侧多出一段墙 —— 这正是想要的效果,
#   而且窗口位置和宽度一个像素都不动。
#   一旦去动留白, 整组拱窗就会平移, 背景和 Godot 里的循环对不上。
_ARCH_MARGIN = (W - _ARCH_W * 3 - _ARCH_GAP * 2) // 2
ARCHES = [(_ARCH_MARGIN + i * (_ARCH_W + _ARCH_GAP),) for i in range(3)]
ARCHES = [(x0, x0 + _ARCH_W, 190, 425, 795) for (x0,) in ARCHES]

# 源图天空是一张只有 9 种纯色的像素画, 结构:
#   行   0 ~ 111  天空 + 云   天空(71,243,255)最暗, 云 140->179->214 越来越亮
#   行 112 ~ 126  地平线光带
#   行 128 ~ 216  海面(3 种蓝)
# 做法: 直接把这 9 种源色【逐一替换】成黄昏配色。
# 比"整体染色"好在两点:
#   1. 像素边缘完全不变, 不会糊成一团
#   2. 云始终保持比天空亮(整体染色很容易把这个明暗关系弄反)
# 另外源图自己就编码了"越靠下的云越亮"(最亮色集中在 26~111 行),
# 所以把最亮的云色映射成暖玫瑰色, 夕阳染云的效果会自动出现。
SKY_PALETTE = {
    (71, 243, 255): (38, 34, 62),      # 天空 -> 深靛紫
    (140, 247, 255): (74, 66, 104),    # 云·浅 -> 紫灰
    (179, 239, 252): (110, 92, 122),   # 云·中 -> 亮紫灰
    (214, 243, 255): (176, 138, 132),  # 云·亮 -> 被夕阳染暖
    # 地平线光带 -> 暖橙。它下方 128~156 行是源图用来模拟"水面波光"的点状抖动。
    # 在青色下那是波光, 但换成暖色后就成了刺眼的橙色虚线 —— 试过"排亮度阶梯",
    # 反而因为中间色被提亮、与深色海面落差更大而更明显。
    # 最终解法: 把海面 4 色【合并成同一个暗色】, 抖动两端同色就等于没有抖动。
    # 地平线光带与暗海面之间是一条干净的横线 —— 那正是地平线, 本来就该是硬的。
    (136, 252, 227): (188, 134, 112),  # 地平线光带(唯一的暖色)
    (45, 214, 252): (50, 41, 58),
    (46, 214, 252): (50, 41, 58),
    (46, 189, 255): (44, 37, 56),
    (47, 189, 255): (44, 37, 56),
}


def remap_palette(img, mapping):
    """按精确 RGB 逐色替换。源图只有 9 色, 所以这一步既快又不会产生中间色。"""
    a = np.asarray(img).copy()
    hit = np.zeros(a.shape[:2], bool)
    for src, dst in mapping.items():
        m = (a[:, :, 0] == src[0]) & (a[:, :, 1] == src[1]) & (a[:, :, 2] == src[2])
        a[m] = dst
        hit |= m
    miss = int((~hit).sum())
    if miss:
        print(f"    提示: 有 {miss} 个像素没匹配到调色板 ({miss/a.shape[0]/a.shape[1]*100:.2f}%)")
    return Image.fromarray(a)


def periodic_fbm(w, h, base=5, octaves=6, rng_=None):
    """左右真正无缝的多倍频噪声, 返回 0..1
       做法: 生成一个小块后 2x2 平铺再放大, 从左上角裁出刚好一个周期,
       这样裁剪结果在左右边界上是连续的(之前从中心裁会破坏周期性, 在墙上留下竖线)
       rng_ 可传入独立的随机源, 避免影响主序列"""
    r = rng if rng_ is None else rng_
    out = np.zeros((h, w), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        cx = base * (2 ** o)
        cy = max(2, int(cx * h / w))
        blk = r.random((cy, cx)).astype(np.float32)
        big = np.tile(blk, (2, 2))
        img = Image.fromarray((big * 255).astype(np.uint8)).resize((w * 2, h * 2), Image.BICUBIC)
        a = np.asarray(img, np.float32) / 255.0
        out += amp * a[:h, :w]
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




# ==================== 天空 / 远山 ====================

def build_sky():
    """用整张源图铺满 1080 高, 统一比例缩放, 不做事后拉伸。

    源图 384x240, 裁 216 行按 5 倍统一缩放正好 1920x1080, 一个像素都不用编。
    旧做法只裁上面 112 行放大到 560px 再"延伸"到 1080 ——
    那凭空补出来的 520px 就是"底部拖了很长一截"的来源。
    """
    # 裁剪行数决定地平线落在哪一行:
    #   裁 216 行 = 横向 5 倍的等比缩放, 地平线在 y≈590
    #   裁 180 行 -> 纵向拉到 6 倍, 地平线下移到 y≈708 —— 窗口里露出更多云
    # 用户反馈"要看见更多的云", 所以裁少一点、纵向多拉一点。
    # 云是无定形的东西, 1.2 倍的纵向拉伸肉眼看不出来。
    CROP_ROWS = 180
    src = Image.open(SKY_SRC).convert("RGB").crop((0, 0, 384, CROP_ROWS))

    # 压平地平线光带。
    # 源图行 112~122 是"隔行 + 断续"的抖动图案(原本模拟水面波光), 换成暖色后
    # 就变成一条刺眼的橙色虚线。地平线本来就该是一条干净硬边, 所以整条填平。
    arr = np.asarray(src).copy()
    arr[110:126, :] = (136, 252, 227)          # 源调色板里的地平线光带色
    src = Image.fromarray(arr)

    # 先换色, 再放大 —— 顺序很重要, 反了会把调色板边界插值成脏色
    src = remap_palette(src, SKY_PALETTE)

    # 双三次放大: 源素材是像素画, 最近邻放大 5 倍会让云块变成大直角,
    # 而且 5 倍像素远粗于游戏本体的 1:1 像素, 反而突兀。
    tile = src.resize((W, H), Image.BICUBIC)

    canvas = Image.new("RGB", (W * 2, H))
    canvas.paste(tile, (0, 0))
    canvas.paste(tile.transpose(Image.FLIP_LEFT_RIGHT), (W, 0))

    a = np.asarray(canvas).astype(np.float32) / 255.0

    # 底部再压一点暗, 做空气透视(海面本来就深, 这里只是让远处更退)
    y01 = (np.arange(H, dtype=np.float32) / (H - 1))[:, None, None]
    below = np.clip((y01 - 0.55) / 0.45, 0, 1) ** 0.9
    a = a * (1.0 - 0.18 * below)

    return Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8))


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




# ==================== 砖墙 / 拱窗 ====================

brng = np.random.default_rng(20260214)

# 砖块配色: 以游戏原版 TileSet1 的紫灰为主, 混入少量偏暖/偏冷的砖
BRICK_PALETTE = [
    np.array((38, 34, 48), np.float32),   # 深紫灰
    np.array((46, 42, 58), np.float32),   # 中紫灰
    np.array((54, 49, 66), np.float32),   # 亮紫灰
    np.array((52, 44, 53), np.float32),   # 偏暖
    np.array((41, 41, 57), np.float32),   # 偏冷
]
# 券石: 比普通砖亮一档, 并且本身也有深浅差别
VOUSSOIR_PALETTE = [
    np.array((62, 56, 80), np.float32),
    np.array((76, 70, 96), np.float32),
    np.array((88, 82, 110), np.float32),
    np.array((56, 51, 72), np.float32),
]
MORTAR = np.array((20, 18, 26), np.float32)

GAP = 3             # 灰缝宽度
ROW_H = (22, 34)    # 每行砖高度范围
BRICK_W = (44, 92)  # 每块砖宽度范围
RING = 40           # 券石厚度
VOUSSOIR_COUNT = 22 # 每扇拱的券石块数


# ---------------- 砖块布局 ----------------
def brick_layout(w, h):
    rows, y = [], 0
    while y < h:
        rh = int(brng.integers(ROW_H[0], ROW_H[1]))
        row = []
        x = -int(brng.integers(0, BRICK_W[1]))
        while x < w:
            bwid = int(brng.integers(BRICK_W[0], BRICK_W[1]))
            row.append((x, bwid))
            x += bwid + GAP
        rows.append((y, rh, row))
        y += rh + GAP
    return rows


def blit_x(canvas, y0, x0, patch):
    hh, ww = canvas.shape[:2]
    ph, pw = patch.shape[:2]
    if y0 >= hh or y0 + ph <= 0:
        return
    y0c, y1c = max(0, y0), min(hh, y0 + ph)
    sy = y0c - y0
    hgt = y1c - y0c
    xm = x0 % ww
    if xm + pw <= ww:
        canvas[y0c:y1c, xm:xm + pw] = patch[sy:sy + hgt]
    else:
        cut = ww - xm
        canvas[y0c:y1c, xm:ww] = patch[sy:sy + hgt, :cut]
        canvas[y0c:y1c, 0:pw - cut] = patch[sy:sy + hgt, cut:]


def brick_color():
    """每块砖一个颜色: 在调色板里随机插值, 再叠整体明暗与色相漂移"""
    t = brng.random()
    i = int(t * (len(BRICK_PALETTE) - 1))
    f = t * (len(BRICK_PALETTE) - 1) - i
    base = BRICK_PALETTE[i] * (1 - f) + BRICK_PALETTE[min(i + 1, len(BRICK_PALETTE) - 1)] * f
    base = base * float(brng.uniform(0.78, 1.22))
    base = base * (1.0 + (brng.random(3).astype(np.float32) - 0.5) * 0.20)
    return base


def make_brick_patch(rh, bwid, base, grain=0.16, damage=True):
    grad = np.linspace(1.12, 0.84, rh).astype(np.float32)[:, None, None]
    patch = np.ones((rh, bwid, 3), np.float32) * base[None, None, :] * grad
    patch *= (1.0 + (brng.random((rh, bwid, 1)).astype(np.float32) - 0.5) * grain)
    if not damage:
        return patch
    r = brng.random()
    if r < 0.22:                                   # 缺角
        cw = int(brng.integers(3, max(4, bwid // 4)))
        ch = int(brng.integers(2, max(3, rh // 3)))
        if brng.random() < 0.5:
            patch[:ch, :cw] *= 0.55
        else:
            patch[-ch:, -cw:] *= 0.55
    elif r < 0.38:                                 # 污渍
        sw = int(brng.integers(max(4, bwid // 3), max(5, bwid)))
        sh = int(brng.integers(3, max(4, rh)))
        ox = int(brng.integers(0, max(1, bwid - sw)))
        oy = int(brng.integers(0, max(1, rh - sh)))
        patch[oy:oy + sh, ox:ox + sw] *= float(brng.uniform(0.72, 0.88))
    elif r < 0.50:                                 # 掉色
        sw = int(brng.integers(max(4, bwid // 4), max(5, bwid // 2)))
        sh = int(brng.integers(2, max(3, rh // 2)))
        ox = int(brng.integers(0, max(1, bwid - sw)))
        oy = int(brng.integers(0, max(1, rh - sh)))
        patch[oy:oy + sh, ox:ox + sw] *= float(brng.uniform(1.14, 1.34))
    return patch


# ---------------- 拱窗几何 ----------------
def build_arch_geometry():
    """拱窗参数。ARCHES 里 拱顶到起拱线的距离 正好等于半宽, 所以是标准半圆"""
    geo = []
    for x0, x1, ytop, yspring, ybot in ARCHES:
        cx = (x0 + x1) / 2.0
        r_in = (x1 - x0) / 2.0
        geo.append((cx, float(yspring), r_in, r_in + RING, float(x0), float(x1), float(ybot)))
    return geo


def arch_masks(geo, yy, xx):
    """返回 (开口遮罩, 券石分块编号)
       券石按圆心角等分成 VOUSSOIR_COUNT 块, 沿半径放射 —— 和真实砖拱一致"""
    opening = np.zeros((H, W), bool)
    block = np.full((H, W), -1, np.int32)
    for k, (cx, cy, r_in, r_out, x0, x1, ybot) in enumerate(geo):
        dx = xx - cx
        dy = cy - yy                      # 向上为正
        r = np.sqrt(dx * dx + dy * dy)
        upper = dy >= 0
        # 开口: 上半圆内 + 起拱线以下的矩形窗身
        opening |= (r < r_in) & upper & (xx >= x0) & (xx <= x1)
        opening |= (yy >= cy) & (yy <= ybot) & (xx >= x0) & (xx <= x1)
        # 券石环带: 上半圆
        band = (r >= r_in) & (r < r_out) & upper
        ang = np.arctan2(dy, dx)
        idx = np.clip((ang / np.pi * VOUSSOIR_COUNT).astype(np.int32), 0, VOUSSOIR_COUNT - 1)
        newv = band & (block < 0)
        block = np.where(newv, idx + k * 1000, block)
    return opening, block


def build_wall():
    rgba = np.zeros((H, W, 4), np.float32)
    tex = periodic_fbm(W, H, base=8, octaves=6)
    yy = np.arange(H)[:, None]
    xx = np.arange(W)[None, :]

    geo = build_arch_geometry()
    opening, block = arch_masks(geo, yy, xx)

    # 逐块画砖(先画满整面墙, 之后再挖窗洞)
    stone = np.zeros((H, W, 3), np.float32)
    for y, rh, row in brick_layout(W, H):
        for x, bwid in row:
            blit_x(stone, y, x, make_brick_patch(rh, bwid, brick_color()))

    wall_rgb = np.tile(MORTAR[None, None, :], (H, W, 1))
    drawn = stone.sum(axis=2) > 0
    wall_rgb[drawn] = stone[drawn]
    wall_rgb = wall_rgb * (0.86 + tex[..., None] * 0.30)

    # ---- 券石: 盖在砖墙之上, 每块一个颜色, 块间留出放射状灰缝 ----
    band = block >= 0
    for bid in np.unique(block[band]):
        m = block == bid
        t = ((int(bid) * 2654435761) % 997) / 997.0
        i = int(t * (len(VOUSSOIR_PALETTE) - 1))
        f = t * (len(VOUSSOIR_PALETTE) - 1) - i
        col = VOUSSOIR_PALETTE[i] * (1 - f) + VOUSSOIR_PALETTE[min(i + 1, len(VOUSSOIR_PALETTE) - 1)] * f
        col = col * float(0.86 + ((int(bid) * 40503) % 100) / 100.0 * 0.28)
        # 拱顶石: 正中的那块做得更亮, 是拱券的视觉中心
        if int(bid) % 1000 == VOUSSOIR_COUNT // 2:
            col = col * 1.32
        wall_rgb[m] = col

    # 放射状灰缝 (块编号变化处)
    jv = band.copy()
    jv[1:] &= (block[1:] != block[:-1])
    jh = band.copy()
    jh[:, 1:] &= (block[:, 1:] != block[:, :-1])
    joint = jv | jh
    wall_rgb[joint] = np.clip(wall_rgb[joint] * 0.42, 0, 255)

    # 拱券的内外两道弧线都勾清楚(参考建筑剖面图的做法), 拱腹再往内提亮一道, 有厚度
    extrados = band & ~np.roll(band, 1, axis=0)      # 上边 = 外沿(拱背)
    intrados = band & ~np.roll(band, -1, axis=0)     # 下边 = 内沿(拱腹)
    wall_rgb[extrados] = np.clip(wall_rgb[extrados] * 0.52, 0, 255)
    wall_rgb[intrados] = np.clip(wall_rgb[intrados] * 0.48, 0, 255)
    lit = band & np.roll(band, -4, axis=0) & ~np.roll(band, -1, axis=0)
    wall_rgb[lit] = np.clip(wall_rgb[lit] * 1.24, 0, 255)

    # 窗内透明
    rgba[~opening, :3] = np.clip(wall_rgb[~opening], 0, 255)
    rgba[~opening, 3] = 255

    # 窗光洒在墙上
    g = soft_glow_alpha(opening, grow=14, blur=24, strength=0.26, inside=True)
    ga = g[..., None] * np.array((120, 118, 160), np.float32)[None, None, :]
    aa = np.clip(ga.max(axis=2), 0, 255)
    sel = (aa > 1) & ~opening
    rgba[sel, :3] = np.clip(rgba[sel, :3] + ga[sel] * 0.45, 0, 255)

    # 起拱线脚(柱头): 只在窗口【两侧】各一块, 不横跨开口。
    # 之前是一整条从 x0-46 拉到 x1+46, 把窗外景色切成上下两半,
    # 看上去就是窗中间多了一道横杠 —— 建筑上柱头本来也只是两侧的承托构件。
    for x0, x1, ytop, yspring, ybot in ARCHES:
        my0, my1 = yspring - 6, yspring + 20
        for sx0, sx1 in ((x0 - 46, x0), (x1, x1 + 46)):
            mm = (yy >= my0) & (yy <= my1) & (xx >= sx0) & (xx < sx1)
            rgba[mm, :3] = np.clip(TRIM[None, :] * (1.00 + tex[mm][:, None] * 0.30), 0, 255)
            rgba[mm, 3] = 255
            # 上下各压一道线, 让它读得出是一个凸出的构件
            for ly, k in ((my0, 1.32), (my1 - 3, 0.58)):
                lm = (yy >= ly) & (yy <= ly + 2) & (xx >= sx0) & (xx < sx1)
                rgba[lm, :3] = np.clip(TRIM[None, :] * k, 0, 255)
                rgba[lm, 3] = 255

    # 窗口两侧的壁柱: 灰紫色竖柱。
    # 之前只在起拱线处放了一小块柱头, 整根柱子是缺失的 —— 用户要的是沿窗边
    # 通到窗台的一整根灰紫色长柱(建筑上叫壁柱/pilaster)。
    # 画在窗台石之前, 这样柱脚会被窗台压住, 交接关系才对。
    for x0, x1, ytop, yspring, ybot in ARCHES:
        for px0, px1, inner_left in (
            (x0 - _PILASTER_W, x0, False),      # 左侧柱, 内缘在右边
            (x1, x1 + _PILASTER_W, True),       # 右侧柱, 内缘在左边
        ):
            pm = (yy >= yspring - 6) & (yy <= ybot) & (xx >= px0) & (xx < px1)
            rgba[pm, :3] = np.clip(TRIM[None, :] * (0.90 + tex[pm][:, None] * 0.36), 0, 255)
            rgba[pm, 3] = 255

            # 石块分层: 每 44px 一道横向石缝, 让它读得出是砌起来的柱子而不是一根色条
            drum = ((yy - (yspring - 6)) % 44) < 2
            dj = pm & drum
            rgba[dj, :3] = np.clip(rgba[dj, :3] * 0.72, 0, 255)

            # 内缘(靠窗一侧)提亮、外缘压暗 -> 柱子凸出于墙面
            lit_col = (px1 - 4, px1) if inner_left else (px0, px0 + 4)
            lm = pm & (xx >= lit_col[0]) & (xx < lit_col[1])
            rgba[lm, :3] = np.clip(TRIM[None, :] * 1.34, 0, 255)
            sh_col = (px0, px0 + 3) if inner_left else (px1 - 3, px1)
            sm2 = pm & (xx >= sh_col[0]) & (xx < sh_col[1])
            rgba[sm2, :3] = np.clip(TRIM[None, :] * 0.58, 0, 255)

    # 窗台石
    for x0, x1, ytop, yspring, ybot in ARCHES:
        sy0, sy1 = ybot, ybot + 26
        sx0, sx1 = x0 - 34, x1 + 34
        sm = (yy >= sy0) & (yy <= sy1) & (xx >= sx0) & (xx <= sx1)
        rgba[sm, :3] = np.clip(TRIM[None, :] * (0.92 + tex[sm][:, None] * 0.5), 0, 255)
        rgba[sm, 3] = 255
        cap = (yy >= sy0) & (yy <= sy0 + 3) & (xx >= sx0) & (xx <= sx1)
        rgba[cap, :3] = np.clip(TRIM[None, :] * 1.30, 0, 255)

    # 底部地面 / 基座
    # 注意: floor_y 必须是常量。
    # 之前写成 1006 + (tex.mean(axis=0) - 0.5) * 8.0 —— 按列加噪声,
    # 于是这条地脚线逐列上下抖动 ±4px, 屏幕上看着就是"像素抖动"。
    floor_y = 1006.0
    full_mask = np.ones((H, W), bool)
    fl = (yy >= floor_y) & full_mask
    rgba[fl, :3] = np.clip(wall_rgb[fl] * 0.80, 0, 255)
    rgba[fl, 3] = 255
    lip = (yy >= floor_y - 4) & (yy <= floor_y + 2) & full_mask
    rgba[lip, :3] = np.clip(TRIM[None, :] * 1.18, 0, 255)
    rgba[lip, 3] = 255

    # 顶部檐口
    full = np.ones((H, W), bool)
    cor = ((yy >= 74) & (yy <= 96)) & full
    rgba[cor, :3] = np.clip(TRIM[None, :] * (0.95 + tex[cor][:, None] * 0.45), 0, 255)
    rgba[cor, 3] = 255
    lip2 = ((yy >= 97) & (yy <= 102)) & full
    rgba[lip2, :3] = np.clip(wall_rgb[lip2] * 0.42, 0, 255)
    rgba[lip2, 3] = 255
    up = (yy <= 20) & full
    rgba[up, :3] = np.clip(wall_rgb[up] * 0.62, 0, 255)
    rgba[up, 3] = 255

    # 四角压暗
    vig_x = np.clip(np.abs(xx - W / 2) / (W / 2), 0, 1) ** 3
    vig_y = np.clip(np.abs(yy - H / 2) / (H / 2), 0, 1) ** 3
    v = np.clip(1.0 - 0.38 * (vig_x + vig_y), 0.55, 1.0)
    m = rgba[..., 3] > 0
    rgba[m, :3] = np.clip(rgba[m, :3] * v[m][:, None], 0, 255)

    inner = soft_glow_alpha(opening, grow=2, blur=2, strength=0.16, inside=False)
    ii = inner > 0.06
    rgba[ii, :3] = np.clip(rgba[ii, :3] + inner[ii][:, None] * np.array((150, 150, 190), np.float32), 0, 255)

    return Image.fromarray(np.clip(rgba, 0, 255).astype(np.uint8))



if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)

    sky = build_sky()
    sky.save(os.path.join(OUT, "bg_sky.png"))

    # 远山镜像拼成 3840 宽, 与天空周期一致, 便于在 Godot 里统一循环
    ridge = build_ridge()
    r2 = Image.new("RGBA", (3840, H))
    r2.paste(ridge, (0, 0))
    r2.paste(ridge.transpose(Image.FLIP_LEFT_RIGHT), (1920, 0))
    r2.save(os.path.join(OUT, "bg_ridge.png"))

    wall = build_wall()
    wall.save(os.path.join(OUT, "bg_wall.png"))

    mock = Image.new("RGB", (W, H), (8, 8, 12))
    mock.paste(sky, (0, 0))
    mock.paste(ridge, (0, 0), ridge)
    mock.paste(wall, (0, 0), wall)
    mock.save(os.path.join(OUT, "mock_menu.png"))

    for f in ("bg_sky.png", "bg_ridge.png", "bg_wall.png"):
        p = os.path.join(OUT, f)
        print(f"{f:16} {Image.open(p).size}  {os.path.getsize(p)/1024:.0f} KB")
