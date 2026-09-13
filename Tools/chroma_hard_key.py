#!/usr/bin/env python3
"""Force a hard #00FF00 chroma key and optional transparent cutout."""
from __future__ import annotations

from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image


def is_chroma(r: int, g: int, b: int) -> bool:
    mx = max(r, b)
    if g > 78 and g > mx + 16:
        return True
    if g > 150 and g >= r + 8 and g >= b + 8:
        return True
    return False


def flood_background(arr: np.ndarray) -> np.ndarray:
    h, w = arr.shape[:2]
    vis = np.zeros((h, w), dtype=bool)
    q: deque[tuple[int, int]] = deque()

    def consider(y: int, x: int) -> None:
        if y < 0 or x < 0 or y >= h or x >= w or vis[y, x]:
            return
        r, g, b = (int(arr[y, x, 0]), int(arr[y, x, 1]), int(arr[y, x, 2]))
        dark = r + g + b < 72 and g + 18 >= r and g + 18 >= b
        if is_chroma(r, g, b) or dark:
            vis[y, x] = True
            q.append((y, x))

    for x in range(w):
        consider(0, x)
        consider(h - 1, x)
    for y in range(h):
        consider(y, 0)
        consider(y, w - 1)

    while q:
        y, x = q.popleft()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1)):
            consider(y + dy, x + dx)
    return vis


def dilate(mask: np.ndarray, radius: int = 1) -> np.ndarray:
    out = mask.copy()
    h, w = mask.shape
    ys, xs = np.where(mask)
    for y, x in zip(ys, xs):
        y0, y1 = max(0, y - radius), min(h, y + radius + 1)
        x0, x1 = max(0, x - radius), min(w, x + radius + 1)
        out[y0:y1, x0:x1] = True
    return out


def process(src: Path, green_dst: Path, cut_dst: Path | None = None) -> None:
    arr = np.array(Image.open(src).convert("RGB"))
    bg = dilate(flood_background(arr), 1)
    green = arr.copy()
    green[bg] = (0, 255, 0)
    keep = ~bg
    r = green[:, :, 0].astype(np.int16)
    g = green[:, :, 1].astype(np.int16)
    b = green[:, :, 2].astype(np.int16)
    spill = keep & (g > r + 28) & (g > b + 18)
    g2 = np.minimum(g, (r + b) // 2 + 10).clip(0, 255).astype(np.uint8)
    green[:, :, 1] = np.where(spill, g2, green[:, :, 1])
    green_dst.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(green).save(green_dst, "PNG")
    if cut_dst is not None:
        rgba = np.dstack([arr, np.where(bg, 0, 255).astype(np.uint8)])
        cut_dst.parent.mkdir(parents=True, exist_ok=True)
        Image.fromarray(rgba, "RGBA").save(cut_dst, "PNG")
    print(f"{green_dst.name}: background {bg.mean()*100:.1f}%")


if __name__ == "__main__":
    root = Path("/workspace/artifacts/imagine_images")
    out_green = Path("/workspace/artifacts/DATING SIM/Assets/ArtKit")
    out_cut = Path("/workspace/public/artkit")
    jobs = [
        ("80168346-d63b-457b-b219-a6f056637f5e.jpg", "Characters/NPCs/barbarian_chroma.png", "characters/barbarian.png"),
        ("d17d167d-ba97-497f-9cd4-e4226c71bb16.jpg", "Characters/Monsters/minotaur_chroma.png", "characters/minotaur.png"),
        ("5e2448b5-86fe-4e13-a086-d74f7366e8cb.jpg", "Characters/NPCs/merchant_traveler_chroma.png", "characters/merchant_traveler.png"),
    ]
    for src_name, green_rel, cut_rel in jobs:
        process(root / src_name, out_green / green_rel, out_cut / cut_rel)
