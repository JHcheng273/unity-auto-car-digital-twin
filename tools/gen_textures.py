# -*- coding: utf-8 -*-
"""
为 Unity 工程程序化生成无缝贴图（可平铺，四边接缝对得上）。

原理：在频域（FFT）里对白噪声做低通滤波再反变换 —— 频域构造出来的信号
天然是周期的，所以贴图左右/上下边缘能严丝合缝地接上，平铺不会出现格子缝。

输出目录：E:\\My project\\Assets\\Resources\\Textures\\
运行：<venv>/python gen_textures.py
"""
import os
import numpy as np
from PIL import Image

SIZE = 512
OUT = r"E:\My project\Assets\Resources\Textures"


def tileable_noise(size, beta, seed):
    """生成周期噪声。beta 越大越平滑（低频为主），越小越接近颗粒。"""
    rng = np.random.default_rng(seed)
    n = rng.standard_normal((size, size))
    F = np.fft.fft2(n)

    fy = np.fft.fftfreq(size)[:, None]
    fx = np.fft.fftfreq(size)[None, :]
    f = np.sqrt(fx * fx + fy * fy)
    f[0, 0] = 1.0                      # 免得 0 的负幂炸掉

    F = F * (f ** (-beta))
    F[0, 0] = 0.0                      # 去掉直流分量

    img = np.real(np.fft.ifft2(F))
    img -= img.mean()
    s = img.std()
    if s > 1e-9:
        img /= s
    return img


def norm01(a):
    lo, hi = a.min(), a.max()
    return (a - lo) / (hi - lo + 1e-9)


def make_asphalt():
    """沥青：中频斑块 + 高频砂粒，深灰偏冷"""
    coarse = tileable_noise(SIZE, 1.10, 1101)
    fine = tileable_noise(SIZE, 0.12, 1102)
    v = norm01(0.55 * coarse + 0.45 * fine)

    gray = 74 + v * 54                 # 74 ~ 128
    return np.stack([gray * 0.97, gray, gray * 1.05], axis=-1)


def make_grass():
    """草地：柔和起伏 + 细碎草叶，偏黄绿"""
    coarse = tileable_noise(SIZE, 1.85, 2201)
    fine = tileable_noise(SIZE, 0.45, 2202)
    v = norm01(0.58 * coarse + 0.42 * fine)

    r = 48 + v * 52
    g = 78 + v * 74
    b = 34 + v * 36
    return np.stack([r, g, b], axis=-1)


def make_concrete():
    """混凝土：很平滑的浅灰，带轻微斑点（人行道/路缘石）"""
    coarse = tileable_noise(SIZE, 2.30, 3301)
    fine = tileable_noise(SIZE, 0.35, 3302)
    v = norm01(0.72 * coarse + 0.28 * fine)

    gray = 146 + v * 44                # 146 ~ 190
    return np.stack([gray * 1.02, gray, gray * 0.95], axis=-1)


def make_facade():
    """建筑外墙：8x8 窗户格子 + 墙面噪声"""
    n = norm01(tileable_noise(SIZE, 1.25, 4401)) - 0.5
    img = np.full((SIZE, SIZE, 3), 178.0)
    img += n[..., None] * 20.0

    cell = SIZE // 8                   # 每格 64 像素
    margin = cell // 5                 # 窗户左右留边
    top = cell // 5                    # 窗户上边留边

    win = np.array([44.0, 56.0, 74.0])
    glow = np.array([128.0, 152.0, 180.0])

    for r in range(8):
        for c in range(8):
            y0 = r * cell + top
            y1 = (r + 1) * cell - margin
            x0 = c * cell + margin
            x1 = (c + 1) * cell - margin
            img[y0:y1, x0:x1] = win
            img[y0:y0 + 3, x0:x1] = glow      # 窗顶一道反光

    return img


def save(rgb, name):
    rgb = np.clip(rgb, 0, 255).astype(np.uint8)
    img = Image.fromarray(rgb, "RGB")
    path = os.path.join(OUT, name)
    img.save(path, optimize=True)
    print("  %-16s %s" % (name, img.size))


def check_seamless(name):
    """验证左右两列 / 上下两行能不能接上（差值越小越无缝）"""
    img = np.asarray(Image.open(os.path.join(OUT, name)).convert("RGB"), dtype=float)
    lr = np.abs(img[:, 0] - img[:, -1]).mean()
    tb = np.abs(img[0, :] - img[-1, :]).mean()
    print("  %-16s 左右缝 %.1f / 上下缝 %.1f  (越小越好，<25 基本看不出)" % (name, lr, tb))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    print("生成贴图 ->", OUT)

    save(make_asphalt(), "asphalt.png")
    save(make_grass(), "grass.png")
    save(make_concrete(), "concrete.png")
    save(make_facade(), "facade.png")

    print("\n无缝性检查：")
    for f in ("asphalt.png", "grass.png", "concrete.png", "facade.png"):
        check_seamless(f)
