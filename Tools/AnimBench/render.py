"""#352 AnimBench renderer: track.json + render.json -> sheet.png, curves.png, clip.mp4/.gif at 1x and 0.25x.

Usage: /c/Python311/python Tools/AnimBench/render.py <out/clip dir> [--no-video] [--ffmpeg PATH] [--width N]

Peg dolls are drawn the way the style kit builds them (PegDoll: tapered tunic, round head and hair, two stub arms, one prop)
in the palette's flat colours (parsed from View/Palette.cs), through a fake orthographic camera at BoxCamera's default
yaw 28 / pitch 34 (and a level side view). An approximation for timing and staging: the Editor is the authority for the look.
"""
import json
import math
import os
import re
import shutil
import subprocess
import sys
import time

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
PALETTE_CS = os.path.join(REPO, "AquariumCiv", "Assets", "Game", "View", "Palette.cs")

# PegDoll proportions (adult units; times the doll's scale)
BODY_H, SHOULDER_X, ARM_LEN, HEAD_R, BODY_R, BODY_RT = 0.13, 0.046, 0.075, 0.043, 0.055, 0.038
HEAD_Y = BODY_H + 0.04


# ----------------------------------------------------------------------------------------------- palette
def hexrgb(h):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16))


def load_palette():
    pal = {}
    try:
        src = open(PALETTE_CS, encoding="utf-8").read()
    except OSError:
        src = ""
    for name, hx in re.findall(r"(\w+) = H\(\"#([0-9A-Fa-f]{6})\"\)", src):
        pal[name] = hexrgb(hx)
    for name, body in re.findall(r"(\w+) = \{([^}]*)\}", src):
        cols = re.findall(r"H\(\"#([0-9A-Fa-f]{6})\"\)", body)
        if cols:
            pal[name] = [hexrgb(c) for c in cols]
    defaults = {
        "GrassA": (141, 170, 91), "GrassB": (122, 156, 78), "Dirt": (176, 141, 98), "Water": (111, 166, 168),
        "LeafA": (110, 154, 75), "LeafB": (92, 138, 69), "Bark": (107, 78, 58), "WoodLight": (164, 123, 85),
        "RockLight": (189, 183, 173), "Rock": (162, 156, 147), "Iron": (74, 68, 64), "Hide": (185, 140, 98),
        "Thatch": (207, 166, 94), "Ember": (196, 67, 43), "Fire": (255, 154, 60), "FireCore": (255, 224, 122),
        "Straw": (224, 194, 122), "Canvas": (226, 211, 179), "Plaster": (239, 227, 204), "Door": (90, 61, 43),
        "Tunics": [(181, 82, 59), (79, 124, 168), (217, 164, 65)], "Skins": [(241, 201, 165)], "Hair": [(59, 42, 32)],
        "Accents": [(224, 64, 42), (242, 182, 48), (46, 134, 200)],
    }
    for k, v in defaults.items():
        pal.setdefault(k, v)
    return pal


PAL = load_palette()


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def darker(c, k=0.75):
    return tuple(int(v * k) for v in c)


def doll_look(seed, camp):
    tunics, skins, hair, acc = PAL["Tunics"], PAL["Skins"], PAL["Hair"], PAL["Accents"]
    tunic = tunics[seed % len(tunics)]
    if camp:
        tunic = mix(tunic, PAL["Hide"], 0.4)
    return {
        "tunic": tunic,
        "skin": skins[(seed // 7) % len(skins)],
        "hair": hair[(seed // 35) % len(hair)],
        "accent": acc[(seed // 3) % len(acc)] if camp else None,
    }


# ----------------------------------------------------------------------------------------------- fonts
def font(size, bold=False):
    for name in (("arialbd.ttf" if bold else "arial.ttf"), "DejaVuSans.ttf"):
        for d in ("C:/Windows/Fonts", "/usr/share/fonts/truetype/dejavu"):
            p = os.path.join(d, name)
            if os.path.exists(p):
                return ImageFont.truetype(p, size)
    return ImageFont.load_default()


# ----------------------------------------------------------------------------------------------- camera
def v_add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def v_mul(a, k):
    return (a[0] * k, a[1] * k, a[2] * k)


def euler_cam(pitch, yaw):
    """Unity: Quaternion.Euler(pitch, yaw, 0) applied to the unit axes (rotate about x, then about y)."""
    p, y = math.radians(pitch), math.radians(yaw)

    def rot(v):
        x, yy, z = v
        y1 = yy * math.cos(p) - z * math.sin(p)
        z1 = yy * math.sin(p) + z * math.cos(p)
        x2 = x * math.cos(y) + z1 * math.sin(y)
        z2 = -x * math.sin(y) + z1 * math.cos(y)
        return (x2, y1, z2)

    return rot((1, 0, 0)), rot((0, 1, 0)), rot((0, 0, 1))


class Cam:
    """Orthographic camera: world point -> pixel (x, y) and depth (larger = farther)."""

    def __init__(self, yaw, pitch, w, h):
        self.r, self.u, self.f = euler_cam(pitch, yaw)
        self.pitch = pitch
        self.w, self.h = w, h
        self.scale, self.cx, self.cy = 100.0, 0.0, 0.0

    def screen(self, p):
        return (p[0] * self.r[0] + p[1] * self.r[1] + p[2] * self.r[2], p[0] * self.u[0] + p[1] * self.u[1] + p[2] * self.u[2])

    def depth(self, p):
        return p[0] * self.f[0] + p[1] * self.f[1] + p[2] * self.f[2]

    def px(self, p):
        sx, sy = self.screen(p)
        return (self.w * 0.5 + (sx - self.cx) * self.scale, self.h * 0.5 - (sy - self.cy) * self.scale)

    def fit(self, pts, margin=0.12, span=None, focus=None):
        if focus is not None:
            self.cx, self.cy = self.screen((focus[0], 0.06, focus[1]))
            span = span or 1.2
            self.scale = self.w / span
            return
        xs, ys = zip(*[self.screen(p) for p in pts]) if pts else ((0,), (0,))
        x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
        self.cx, self.cy = (x0 + x1) / 2, (y0 + y1) / 2
        wx, wy = max(x1 - x0, 0.2) + 2 * margin, max(y1 - y0, 0.2) + 2 * margin
        self.scale = min(self.w / wx, self.h / wy)
        if span:
            self.scale = self.w / span


def view_angles(view, cam):
    yaw, pitch = cam.get("yaw", 28.0), cam.get("pitch", 34.0)
    if view == "side":
        return cam.get("sideYaw", yaw + 90.0), 4.0
    return yaw, pitch


def action_side_yaw(track):
    """A level side view across the action: perpendicular to the first hit's doll -> target line, else to the widest motion."""
    def yaw_across(dx, dz):
        # look along the perpendicular, so the action runs left to right on screen
        return math.degrees(math.atan2(dz, -dx)) if abs(dx) + abs(dz) > 1e-6 else None
    objs = {o["id"]: o for o in track["objects"]}
    dolls = {d["id"]: d for d in track["dolls"]}
    for h in track.get("hits", []):
        tg = h.get("target")
        d = dolls.get(h["doll"])
        if tg is None or d is None:
            continue
        f = h["f0"]
        if tg in objs:
            tp = objs[tg]["ch"]["pos"][f]
            tx_, tz_ = tp[0], tp[2]
        elif tg in dolls:
            tx_, tz_ = dolls[tg]["ch"]["x"][f], dolls[tg]["ch"]["z"][f]
        else:
            continue
        y = yaw_across(tx_ - d["ch"]["x"][f], tz_ - d["ch"]["z"][f])
        if y is not None:
            return y
    best, by = 0.0, None
    for d in track["dolls"]:
        xs, zs = d["ch"]["x"], d["ch"]["z"]
        dx, dz = xs[-1] - xs[0], zs[-1] - zs[0]
        if dx * dx + dz * dz > best and dx * dx + dz * dz > 0.01:
            best, by = dx * dx + dz * dz, yaw_across(dx, dz)
    return by


# ----------------------------------------------------------------------------------------------- frames (affine)
def fpoint(fr, p):
    o = fr
    return (o[0] + o[3] * p[0] + o[6] * p[1] + o[9] * p[2],
            o[1] + o[4] * p[0] + o[7] * p[1] + o[10] * p[2],
            o[2] + o[5] * p[0] + o[8] * p[1] + o[11] * p[2])


def hull(points):
    pts = sorted(set((round(x, 2), round(y, 2)) for x, y in points))
    if len(pts) <= 2:
        return pts

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]


RING = [(math.cos(a * math.pi / 4), math.sin(a * math.pi / 4)) for a in range(8)]


def prism_hull(cam, fr, r0, r1, y0, y1):
    pts = []
    for c, s in RING:
        pts.append(cam.px(fpoint(fr, (c * r0, y0, s * r0))))
        pts.append(cam.px(fpoint(fr, (c * r1, y1, s * r1))))
    return hull(pts)


# Prop shapes in the prop's own frame (PegDoll.BuildProp): ("seg", a, b, colour, width) / ("blob", centre, radius, colour)
def prop_shapes(kind):
    W, R, I = PAL["WoodLight"], PAL["RockLight"], PAL["Iron"]
    k = kind
    if k == "Spear":
        return [("seg", (0, -0.05, 0), (0, 0.17, 0), W, 0.009), ("seg", (0, 0.165, 0), (0, 0.205, 0), R, 0.02)]
    if k == "Axe":
        return [("seg", (0, -0.05, 0), (0, 0.15, 0), W, 0.016), ("seg", (0, 0.115, 0.0), (0, 0.115, -0.056), I, 0.05)]
    if k == "Hoe":
        return [("seg", (0, -0.05, 0), (0, 0.19, 0), W, 0.014), ("seg", (0, 0.185, 0), (0, 0.17, -0.05), I, 0.03)]
    if k in ("Rod", "MeasuringRod"):
        return [("seg", (0, -0.03, 0), (0, 0.29, 0), W if k == "Rod" else PAL["Plaster"], 0.008), ("seg", (0, 0.29, 0), (0, 0.19, -0.004), I, 0.003)]
    if k == "Hammer":
        return [("seg", (0, -0.03, 0), (0, 0.1, 0), W, 0.014), ("seg", (0, 0.1, 0.021), (0, 0.1, -0.037), I, 0.03)]
    if k == "Broom":
        return [("seg", (0, -0.05, 0), (0, 0.15, 0), W, 0.012), ("blob", (0, -0.075, 0), 0.03, PAL["Straw"])]
    if k == "Stick":
        return [("seg", (0, -0.08, 0), (0, 0.18, 0), PAL["Bark"], 0.012)]
    if k == "Fish":
        return [("seg", (0, -0.015, 0), (0, 0.085, 0), W, 0.006), ("blob", (0, 0.0, -0.012), 0.018, PAL["Water"])]
    if k == "Basket":
        return [("blob", (0, -0.01, 0), 0.038, PAL["Thatch"]), ("blob", (0, 0.008, 0), 0.02, PAL["Ember"])]
    if k in ("Bucket", "BucketEmpty", "BucketMuddy"):
        top = {"Bucket": PAL["Water"], "BucketMuddy": (107, 77, 43), "BucketEmpty": PAL["Door"]}[k]
        return [("blob", (0, -0.06, 0), 0.032, W), ("blob", (0, -0.04, 0), 0.018, top)]
    if k == "Rabbit":
        return [("seg", (0, 0, -0.05), (0, 0, 0.05), (150, 135, 118), 0.04)]
    if k in ("Bundle", "Sack"):
        return [("blob", (0, 0, 0), 0.05, PAL["Canvas"])]
    if k == "Log":
        return [("seg", (0, 0, -0.08), (0, 0, 0.08), PAL["Bark"], 0.035)]
    if k == "Lantern":
        return [("blob", (0, -0.035, 0), 0.022, PAL["Lamp"] if "Lamp" in PAL else (255, 200, 107))]
    return [("blob", (0, 0, 0), 0.03, (150, 150, 150))]


# ----------------------------------------------------------------------------------------------- the scene at one frame
class Scene:
    def __init__(self, track, render):
        self.t = track
        self.r = render
        self.dolls = []
        for i, d in enumerate(track["dolls"]):
            rd = render["dolls"][i]
            self.dolls.append({"meta": d, "r": rd, "look": doll_look(d["seed"], d.get("camp", True))})
        self.objects = track["objects"]
        self.n = track["frames"]
        hits = set()
        for h in track.get("hits", []):
            for f in range(h["f0"], h["f1"] + 1):
                hits.add(f)
        self.hit_frames = hits
        self.answers = set()
        for p in track.get("principles", []):
            for m in p.get("marks", []):
                if m["kind"] == "answer":
                    self.answers.add(int(round(m["t0"] * track["fps"])))

    def fit_points(self):
        pts = []
        for d in self.dolls:
            for f in range(0, self.n, max(1, self.n // 60)):
                g = d["r"]["ground"][f]
                pts.append((g[0], 0.0, g[1]))
                pts.append((g[0], 0.24 * d["meta"]["scale"], g[1]))
        for o in self.objects:
            for f in range(0, self.n, max(1, self.n // 30)):
                p = o["ch"]["pos"][f]
                pts.append(tuple(p))
        return pts

    def draw(self, img, cam, f, labels=False, mark_hits=True):
        dr = ImageDraw.Draw(img)
        s = cam.scale
        self.draw_ground(dr, cam)
        prims = []  # (depth, fn)

        for o in self.objects:
            self.object_prims(o, f, cam, prims)
        for d in self.dolls:
            self.doll_prims(d, f, cam, prims)

        # shadows first (on the ground), then everything far to near
        for dep, fn in sorted((p for p in prims if p[0] is None), key=lambda p: 0):
            fn(dr)
        for dep, fn in sorted((p for p in prims if p[0] is not None), key=lambda p: -p[0]):
            fn(dr)

        if mark_hits:
            for d in self.dolls:
                ch = d["meta"]["ch"]
                if ch["hit"][f] > 0.5:
                    x, y = cam.px(ch["tip"][f])
                    rr = max(5, 0.03 * s)
                    dr.ellipse([x - rr, y - rr, x + rr, y + rr], outline=(230, 30, 30), width=max(2, int(s / 180)))
        if labels:
            fnt = font(max(10, int(s * 0.045)))
            for d in self.dolls:
                if d["r"]["hidden"][f]:
                    continue
                hx, hy = cam.px(d["meta"]["ch"]["head"][f])
                dr.text((hx, hy - 0.08 * s * d["meta"]["scale"] - 4), d["meta"]["id"], fill=(30, 30, 30), font=fnt, anchor="ms")
            for o in self.objects:
                if o["kind"] in ("rabbit",):
                    x, y = cam.px(o["ch"]["pos"][f])
                    dr.text((x, y - 0.07 * s), o["id"], fill=(60, 50, 40), font=fnt, anchor="ms")

    def draw_ground(self, dr, cam):
        w, h = cam.w, cam.h
        dr.rectangle([0, 0, w, h], fill=PAL["GrassA"])
        # cell lines every 0.1 u (the site grid): a scale you can count
        line = mix(PAL["GrassA"], PAL["GrassB"], 0.55)
        # find the world range visible: unproject corners on the ground plane roughly
        span = (w / cam.scale) * 1.6 + (h / cam.scale) * 2.5
        cx, cz = self.ground_centre(cam)
        k0 = int(math.floor((cx - span) * 10)); k1 = int(math.ceil((cx + span) * 10))
        j0 = int(math.floor((cz - span) * 10)); j1 = int(math.ceil((cz + span) * 10))
        for k in range(k0, k1 + 1):
            x = k / 10.0
            dr.line([cam.px((x, 0, j0 / 10.0)), cam.px((x, 0, j1 / 10.0))], fill=line, width=1)
        for j in range(j0, j1 + 1):
            z = j / 10.0
            dr.line([cam.px((k0 / 10.0, 0, z)), cam.px((k1 / 10.0, 0, z))], fill=line, width=1)

    def ground_centre(self, cam):
        # the ground point under the screen centre (ortho: walk back along the view direction to y = 0)
        best = None
        for d in self.dolls[:1]:
            g = d["r"]["ground"][0]
            best = (g[0], g[1])
        if best is None and self.objects:
            p = self.objects[0]["ch"]["pos"][0]
            best = (p[0], p[2])
        return best or (0.0, 0.0)

    # ---------------------------------------------------------------- dolls
    def doll_prims(self, d, f, cam, prims):
        r = d["r"]
        if r["hidden"][f]:
            return
        sc = d["meta"]["scale"]
        look = d["look"]
        body = r["body"][f]
        arms = (r["armL"][f], r["armR"][f])
        prop = r["prop"][f]
        s = cam.scale
        g = r["ground"][f]

        # shadow
        def shadow(dr, b=body):
            x, y = cam.px((b[0], 0.0, b[2]))  # under the body as drawn (a hop's offset moves it off the planned spot)
            rx = BODY_R * 1.1 * sc * s
            ry = rx * max(0.15, math.sin(math.radians(cam.pitch)))
            dr.ellipse([x - rx, y - ry, x + rx, y + ry], fill=darker(PAL["GrassA"], 0.78))
        prims.append((None, shadow))

        centre = fpoint(body, (0, BODY_H * 0.5 * sc, 0))
        dep_body = cam.depth(centre)
        tunic = look["tunic"]
        outline = darker(tunic, 0.55)

        def draw_body(dr):
            poly = prism_hull(cam, body, BODY_R * sc, BODY_RT * sc, 0.0, BODY_H * sc)
            dr.polygon(poly, fill=tunic, outline=outline)
            belt = prism_hull(cam, body, 0.0505 * sc, 0.0475 * sc, BODY_H * 0.42 * sc - 0.006 * sc, BODY_H * 0.42 * sc + 0.006 * sc)
            dr.polygon(belt, fill=mix(tunic, PAL["Door"], 0.6))
            if look["accent"]:
                band = prism_hull(cam, body, 0.0445 * sc, 0.040 * sc, BODY_H * 0.8 * sc - 0.012 * sc, BODY_H * 0.8 * sc + 0.012 * sc)
                dr.polygon(band, fill=look["accent"])
        prims.append((dep_body, draw_body))

        head = fpoint(body, (0, HEAD_Y * sc, 0))
        hair_c = fpoint(body, (0, HEAD_Y * sc + 0.012 * sc, 0.008 * sc))
        hr = HEAD_R * sc * s

        def draw_hair(dr):
            x, y = cam.px(hair_c)
            rr = 0.047 * sc * s
            dr.ellipse([x - rr, y - rr * 0.85, x + rr, y + rr * 0.85], fill=look["hair"])

        def draw_head(dr):
            x, y = cam.px(head)
            dr.ellipse([x - hr, y - hr, x + hr, y + hr], fill=look["skin"], outline=darker(look["skin"], 0.7))
            if look["accent"]:
                # the camp headband: a bright strip across the brow
                bw = max(1, int(0.02 * sc * s))
                dr.line([(x - hr * 0.98, y - hr * 0.05), (x + hr * 0.98, y - hr * 0.05)], fill=look["accent"], width=bw)
        dh, dhair = cam.depth(head), cam.depth(hair_c)
        # the head and hair sit just above the body: nudge them nearer so the body never covers its own head
        prims.append((min(dh, dep_body) - 0.0005, draw_head))
        prims.append((min(dhair, dep_body) - (0.001 if dhair < dh else 0.0), draw_hair))

        for i, arm in enumerate(arms):
            sh = (arm[0], arm[1], arm[2])
            hand = fpoint(arm, (0, -ARM_LEN * sc, 0))
            elbow = fpoint(arm, (0, -ARM_LEN * 0.78 * sc, 0))
            dep = cam.depth(fpoint(arm, (0, -ARM_LEN * 0.5 * sc, 0)))

            def draw_arm(dr, sh=sh, hand=hand, elbow=elbow):
                a, b, c = cam.px(sh), cam.px(elbow), cam.px(hand)
                wsl = max(2, int(0.026 * sc * s))
                dr.line([a, b], fill=tunic, width=wsl)
                rr = max(1.5, 0.013 * sc * s)
                dr.ellipse([a[0] - wsl / 2, a[1] - wsl / 2, a[0] + wsl / 2, a[1] + wsl / 2], fill=tunic)
                dr.line([b, c], fill=look["skin"], width=max(2, int(0.022 * sc * s)))
                dr.ellipse([c[0] - rr, c[1] - rr, c[0] + rr, c[1] + rr], fill=look["skin"])
            prims.append((dep - 0.0002, draw_arm))

        if prop:
            fr = prop["f"]
            for shp in prop_shapes(prop["k"]):
                if shp[0] == "seg":
                    a, b = fpoint(fr, shp[1]), fpoint(fr, shp[2])
                    if a[1] < 0 and b[1] < 0:
                        continue
                    if a[1] < 0 or b[1] < 0:  # cut at the ground: in the game the grass hides the rest
                        lo_, hi_ = (a, b) if a[1] < 0 else (b, a)
                        k = hi_[1] / (hi_[1] - lo_[1])
                        a, b = hi_, (hi_[0] + (lo_[0] - hi_[0]) * k, 0.0, hi_[2] + (lo_[2] - hi_[2]) * k)
                    dep = cam.depth(((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2))

                    def draw_seg(dr, a=a, b=b, col=shp[3], wd=shp[4]):
                        dr.line([cam.px(a), cam.px(b)], fill=col, width=max(2, int(wd * s)))
                    prims.append((dep - 0.0003, draw_seg))
                else:
                    c = fpoint(fr, shp[1])

                    def draw_blob(dr, c=c, rad=shp[2], col=shp[3]):
                        x, y = cam.px(c)
                        rr = max(2, rad * s)
                        dr.ellipse([x - rr, y - rr, x + rr, y + rr], fill=col, outline=darker(col, 0.6))
                    prims.append((cam.depth(c) - 0.0003, draw_blob))

    # ---------------------------------------------------------------- objects
    def object_prims(self, o, f, cam, prims):
        ch = o["ch"]
        if ch["visible"][f] < 0.5:
            return
        p = ch["pos"][f]
        kind = o["kind"]
        s = cam.scale
        rad = o.get("radius", 0.05)
        dep = cam.depth(p)
        if kind == "rabbit":
            act = ch["act"][f]
            yaw = math.radians(ch["yaw"][f])
            fx, fz = math.sin(yaw), math.cos(yaw)
            fur, belly = (150, 135, 118), (230, 222, 205)
            up = act == "SitUp"
            down = act == "Fallen"   # #362: on its side
            hop = p[1]
            body_c = (p[0], 0.028 + hop + (0.008 if up else 0), p[2])
            head_c = (p[0] + fx * (0.022 if up else 0.035), (0.068 if up else 0.04) + hop, p[2] + fz * (0.022 if up else 0.035))
            tail_c = (p[0] - fx * 0.035, 0.03 + hop, p[2] - fz * 0.035)

            def shadow(dr):
                x, y = cam.px((p[0], 0, p[2]))
                rx = 0.04 * s
                dr.ellipse([x - rx, y - rx * 0.5, x + rx, y + rx * 0.5], fill=darker(PAL["GrassA"], 0.8))
            prims.append((None, shadow))

            def draw(dr):
                bx, by = cam.px(body_c)
                rx, ry = 0.034 * s, (0.036 if up else 0.024) * s
                if down:
                    rx, ry = 0.036 * s, 0.017 * s
                if act == "Flee":
                    rx *= 1.15
                tx, ty = cam.px(tail_c)
                dr.ellipse([tx - 0.012 * s, ty - 0.012 * s, tx + 0.012 * s, ty + 0.012 * s], fill=belly)
                dr.ellipse([bx - rx, by - ry, bx + rx, by + ry], fill=fur, outline=darker(fur, 0.6))
                hx, hy = cam.px(head_c)
                hr = 0.018 * s
                ear = (0.045 if up else 0.03) * s
                lean = 0.3 if act == "Flee" else 0.0
                for side in (-1, 1):
                    ex = hx + side * hr * 0.45
                    dr.line([(ex, hy - hr * 0.5), (ex + side * ear * 0.15 + lean * ear, hy - ear)], fill=darker(fur, 0.9), width=max(2, int(0.008 * s)))
                dr.ellipse([hx - hr, hy - hr, hx + hr, hy + hr], fill=fur, outline=darker(fur, 0.6))
            prims.append((dep, draw))
        elif kind == "fire":
            def draw(dr):
                x, y = cam.px((p[0], 0, p[2]))
                rx = 0.1 * s
                ry = rx * max(0.2, math.sin(math.radians(cam.pitch)))
                dr.ellipse([x - rx, y - ry, x + rx, y + ry], fill=PAL["Rock"], outline=darker(PAL["Rock"], 0.6))
                tip = cam.px((p[0], 0.13, p[2]))
                dr.polygon([(x - rx * 0.55, y), (x + rx * 0.55, y), tip], fill=PAL["Fire"])
                tip2 = cam.px((p[0], 0.08, p[2]))
                dr.polygon([(x - rx * 0.3, y), (x + rx * 0.3, y), tip2], fill=PAL["FireCore"])
            prims.append((dep, draw))
        elif kind == "bush":
            berries = int(round(ch.get("berries", [0] * (f + 1))[f]))

            def draw(dr):
                x, y = cam.px((p[0], rad * 0.8, p[2]))
                rr = rad * s
                dr.ellipse([x - rr, y - rr * 0.9, x + rr, y + rr * 0.9], fill=PAL["LeafB"], outline=darker(PAL["LeafB"], 0.6))
                for i in range(berries):
                    a = i * 2.39996
                    q = 0.35 + 0.5 * ((i * 0.618) % 1.0)
                    bx, by = x + math.cos(a) * rr * q, y + math.sin(a) * rr * q * 0.8
                    br = max(2, 0.014 * s)
                    dr.ellipse([bx - br, by - br, bx + br, by + br], fill=(181, 40, 60))
            prims.append((dep, draw))
        elif kind == "tree":
            def draw(dr):
                a, b = cam.px((p[0], 0, p[2])), cam.px((p[0], 0.3, p[2]))
                dr.line([a, b], fill=PAL["Bark"], width=max(3, int(0.03 * s)))
                rr = rad * 1.4 * s
                dr.ellipse([b[0] - rr, b[1] - rr, b[0] + rr, b[1] + rr], fill=PAL["LeafA"], outline=darker(PAL["LeafA"], 0.6))
            prims.append((dep, draw))
        elif kind in ("water", "pond", "puddle"):
            sz = o.get("size") or [rad * 2, rad * 2]

            def draw(dr):
                pts = []
                for k in range(16):
                    a = k * math.pi / 8
                    pts.append(cam.px((p[0] + math.cos(a) * sz[0] / 2, 0.0, p[2] + math.sin(a) * sz[1] / 2)))
                dr.polygon(pts, fill=PAL["Water"], outline=darker(PAL["Water"], 0.7))
            prims.append((None, draw))
        elif kind == "spear":
            yaw = math.radians(ch["yaw"][f])
            d = (math.sin(yaw) * 0.11, 0.0, math.cos(yaw) * 0.11)

            def draw(dr):
                a, b = cam.px(v_add(p, v_mul(d, -1))), cam.px(v_add(p, d))
                dr.line([a, b], fill=PAL["WoodLight"], width=max(2, int(0.009 * s)))
                dr.ellipse([b[0] - 3, b[1] - 3, b[0] + 3, b[1] + 3], fill=PAL["RockLight"])
            prims.append((dep, draw))
        else:
            col = {"ball": PAL["Accents"][0], "berry": (181, 40, 60), "fish": PAL["Water"], "basket": PAL["Thatch"],
                   "stash": PAL["Hide"], "tent": PAL["Canvas"]}.get(kind, (140, 140, 140))

            def shadow(dr):
                x, y = cam.px((p[0], 0, p[2]))
                rx = rad * s
                dr.ellipse([x - rx, y - rx * 0.45, x + rx, y + rx * 0.45], fill=darker(PAL["GrassA"], 0.82))
            prims.append((None, shadow))

            def draw(dr):
                x, y = cam.px((p[0], p[1] + rad, p[2]))
                rr = max(2, rad * s)
                dr.ellipse([x - rr, y - rr, x + rr, y + rr], fill=col, outline=darker(col, 0.6))
            prims.append((dep, draw))


# ----------------------------------------------------------------------------------------------- sheet
def pick_frames(track, scene):
    sh = track.get("sheet", {})
    n = track["frames"]
    fps = track["fps"]
    t0, t1 = sh.get("from", 0.0), sh.get("to", track["duration"])
    count = max(1, int(sh.get("frames", 24)))
    t1 = min(t1, (n - 1) / fps)
    if count == 1:
        return [int(round(t0 * fps))]
    frames = [min(n - 1, int(round((t0 + (t1 - t0) * k / (count - 1)) * fps))) for k in range(count)]
    # make sure every hit's first frame is on the sheet: the nearest sample moves onto it
    for h in track.get("hits", []):
        if not (t0 - 1e-6 <= h["t0"] <= t1 + 1e-6):
            continue
        if any(h["f0"] <= f <= h["f1"] for f in frames):
            continue
        k = min(range(count), key=lambda i: abs(frames[i] - h["f0"]))
        frames[k] = h["f0"]
    return sorted(set(frames))


def make_sheet(track, scene, out, width=1560):
    frames = pick_frames(track, scene)
    cols = max(1, int(track.get("sheet", {}).get("columns", 6)))
    rows = (len(frames) + cols - 1) // cols
    pad = 6
    cw = (width - pad * (cols + 1)) // cols
    main_h = int(cw * 0.78)
    side_h = int(cw * 0.36)
    cap_h = 30
    ch_ = main_h + side_h + cap_h + 4
    header_h = 118
    H = header_h + rows * (ch_ + pad) + pad
    sheet = Image.new("RGB", (width, H), (246, 243, 236))
    dr = ImageDraw.Draw(sheet)
    camspec = track.get("camera", {})
    ss = 2  # supersample the cells
    main = Cam(*view_angles(camspec.get("view", "default"), camspec), cw * ss, main_h * ss)
    side = Cam(*view_angles("side", camspec), cw * ss, side_h * ss)
    pts = scene.fit_points()
    focus = camspec.get("focus")
    span = camspec.get("span")
    main.fit(pts, focus=focus, span=span)
    side.fit(pts, span=span, focus=focus)

    f_title, f_small, f_cap = font(20, True), font(13), font(13)
    fps = track["fps"]
    step = (frames[-1] - frames[0]) / max(1, len(frames) - 1) / fps if len(frames) > 1 else 0
    dr.text((pad + 2, 6), f"{track['clip']}  |  {track['duration']:.2f}s @ {fps:g} fps  |  sheet {frames[0] / fps:.2f}-{frames[-1] / fps:.2f}s, ~{step:.3f}s apart  |  AnimBench (approximation; the Editor is the authority)", fill=(30, 30, 30), font=f_title)
    px_doll = BODY_H * 1.7 * main.scale / ss
    dr.text((pad + 2, 34), f"top: default camera (yaw {camspec.get('yaw', 28):g}, pitch {camspec.get('pitch', 34):g}); strip: side view (level). An adult here is ~{px_doll:.0f} px tall; at full-box zoom on a phone it is ~30 px.", fill=(60, 60, 60), font=f_small)
    dr.text((pad + 2, 52), "red frame + red ring at the tool = DollPose.hit; blue frame = the target answers (principle 2); grid = 0.1 u cells", fill=(60, 60, 60), font=f_small)
    y = 70
    for p in track.get("principles", []):
        if y > header_h - 14:
            break
        col = (40, 120, 40) if p["status"] == "pass" else (120, 120, 120) if p["status"] == "n/a" else (190, 40, 40)
        gap = "  [known gap]" if p["knownGap"] and p["status"] == "FAIL" else ""
        first = p["findings"][0] if p["findings"] else ""
        dr.text((pad + 2, y), f"{p['status']:<4} {p['id']}: {first}{gap}"[:200], fill=col, font=f_small)
        y += 15 if len(track.get("principles", [])) <= 3 else 9
    # principle lines: compact into two columns when there are many
    if len(track.get("principles", [])) > 3:
        dr.rectangle([0, 68, width, header_h - 2], fill=(246, 243, 236))
        for i, p in enumerate(track["principles"]):
            col = (40, 120, 40) if p["status"] == "pass" else (120, 120, 120) if p["status"] == "n/a" else (190, 40, 40)
            gap = " [known gap]" if p["knownGap"] and p["status"] == "FAIL" else ""
            first = p["findings"][0] if p["findings"] else ""
            txt = f"{p['status']:<4} {p['id']}{gap}: {first}"
            cx = pad + 2 + (i % 2) * (width // 2)
            cy = 70 + (i // 2) * 15
            dr.text((cx, cy), txt[:122], fill=col, font=f_small)

    lead = track["dolls"][0] if track["dolls"] else None
    for i, f in enumerate(frames):
        r, c = divmod(i, cols)
        x0 = pad + c * (cw + pad)
        y0 = header_h + r * (ch_ + pad)
        cell = Image.new("RGB", (cw * ss, main_h * ss))
        scene.draw(cell, main, f, labels=(i == 0))
        cell = cell.resize((cw, main_h), Image.LANCZOS)
        sheet.paste(cell, (x0, y0))
        sc = Image.new("RGB", (cw * ss, side_h * ss))
        scene.draw(sc, side, f, labels=False)
        sc = sc.resize((cw, side_h), Image.LANCZOS)
        sheet.paste(sc, (x0, y0 + main_h + 2))
        hit = f in scene.hit_frames
        ans = any(abs(f - a) <= 0 for a in scene.answers)
        border = (220, 30, 30) if hit else (40, 90, 220) if ans else (180, 175, 165)
        dr.rectangle([x0 - 2, y0 - 2, x0 + cw + 1, y0 + main_h + side_h + 3], outline=border, width=4 if (hit or ans) else 1)
        cap = f"f{f}  {f / fps:.2f}s"
        if lead is not None:
            st = lead["ch"]["style"][f]
            hp = lead["ch"]["hunt"][f]
            cap += f"  {lead['id']}: {st}" + (f"/{hp}" if hp != "None" else "")
        dr.text((x0 + 2, y0 + main_h + side_h + 6), cap, fill=(220, 30, 30) if hit else (40, 40, 40), font=f_cap)
    sheet.save(out)
    return frames


# ----------------------------------------------------------------------------------------------- curves
def deriv(v, fps):
    return [0.0] + [(v[i] - v[i - 1]) * fps for i in range(1, len(v))]


def phases_for_hit(ch, h, fps, n):
    """Wind-up / top hold / strike / contact / hold / follow-through spans (seconds) around one hit, from armR.x and lean.
    A wind-up that happens in one frame is reported as "wind-up POP" (the arm teleports to the top)."""
    a = [v[0] for v in ch["armR"]]
    lean = ch["lean"]
    f0, f1 = h["f0"], h["f1"]
    lo = max(0, f0 - int(1.2 * fps))
    ref = a[f0]
    peak = max(range(lo, f0 + 1), key=lambda i: abs(a[i] - ref)) if f0 > lo else f0
    va, vl = deriv(a, fps), deriv(lean, fps)
    moving = lambda i: abs(va[i]) > 15.0
    still = lambda i: abs(va[i]) < 20.0 and abs(vl[i]) < 20.0
    spans = []
    # wind-up: back from the peak while the arm was moving
    w0 = peak
    while w0 > lo and moving(w0):
        w0 -= 1
    if peak - w0 <= 1 and abs(a[peak] - a[max(0, peak - 1)]) > 30.0:
        spans.append(("wind-up POP", max(0, peak - 1), peak))
    elif peak > w0:
        spans.append(("wind-up", w0, peak))
    # the strike: back from the contact while the arm was moving; between the peak and it, a hold at the top
    s0 = f0
    while s0 > peak and moving(s0 - 1):
        s0 -= 1
    if s0 - peak >= 2:
        spans.append(("top hold", peak, s0))
    if f0 > s0:
        spans.append(("strike", s0, f0))
    spans.append(("contact", f0, f1))
    k = f1 + 1
    e = k
    while e < n and still(e):
        e += 1
    if e - k >= 2:
        spans.append(("hold", k, e))
        k = e
    else:
        spans.append(("no hold", k, k))
    e = k
    lim = min(n - 1, k + int(0.8 * fps))
    while e < lim and not still(e):
        e += 1
    if e > k:
        spans.append(("follow-through", k, e))
    return [(name, s_ / fps, t_ / fps) for name, s_, t_ in spans]


SPAN_COL = {"wind-up": (255, 214, 120), "wind-up POP": (255, 120, 200), "top hold": (255, 236, 170), "strike": (255, 170, 110), "contact": (240, 90, 90), "hold": (170, 200, 255),
            "no hold": (150, 150, 150), "follow-through": (190, 230, 170)}


def make_curves(track, out, width=1500):
    dolls = track["dolls"][:4]
    objs = [o for o in track["objects"] if o["kind"] == "rabbit" or o["ch"].get("berries") or o["kind"] in ("ball", "spear", "berry", "fish")][:3]
    fps = track["fps"]
    n = track["frames"]
    T = track["t"]
    panel_h, pad, left = 210, 10, 70
    H = 40 + (len(dolls) + (1 if objs else 0)) * (panel_h + pad) + pad
    img = Image.new("RGB", (width, H), (250, 248, 243))
    dr = ImageDraw.Draw(img)
    f_t, f_s = font(18, True), font(12)
    dr.text((pad, 8), f"{track['clip']}: key channels over time (degrees unless noted); shaded: wind-up / strike / contact / hold / follow-through; dashed: segment changes", fill=(30, 30, 30), font=f_t)
    pw = width - left - pad - 215
    dur = max(T[-1], 1e-3)

    def tx(t):
        return left + t / dur * pw

    series_cols = [("armR.x", (200, 60, 40)), ("armL.x", (60, 110, 200)), ("lean", (40, 140, 60)), ("offset.y x1000", (150, 80, 170)),
                   ("yaw-yaw0", (120, 120, 120)), ("squash x100", (210, 150, 30)), ("speed x500", (40, 170, 170))]
    for k, d in enumerate(dolls):
        ch = d["ch"]
        y0 = 40 + k * (panel_h + pad)
        dr.rectangle([left, y0, left + pw, y0 + panel_h], fill=(255, 255, 255), outline=(200, 200, 200))
        for h in track.get("hits", []):
            if h["doll"] != d["id"]:
                continue
            spans = phases_for_hit(ch, h, fps, n)
            for name, s_, e_ in spans:  # shading first, markers and labels on top
                if name not in ("no hold", "wind-up POP"):
                    dr.rectangle([tx(s_), y0 + 1, max(tx(s_) + 1, tx(e_)), y0 + panel_h - 1], fill=SPAN_COL[name])
            for j, (name, s_, e_) in enumerate(spans):
                marker = name in ("no hold", "wind-up POP")
                x = tx(e_) if marker else tx(s_)
                if marker:
                    dr.line([x, y0, x, y0 + panel_h], fill=(200, 40, 140) if name != "no hold" else (110, 110, 110), width=2)
                col = (200, 40, 140) if name == "wind-up POP" else (110, 110, 110) if name == "no hold" else (70, 50, 30)
                dr.text((x + 2, y0 + 2 + (j % 4) * 12), name, fill=col, font=f_s)
        for sg in d["segments"]:
            if 0 < sg["t0"] < dur:
                x = tx(sg["t0"])
                for yy in range(int(y0), int(y0 + panel_h), 6):
                    dr.line([x, yy, x, yy + 3], fill=(120, 120, 120))
                dr.text((x + 2, y0 + panel_h - 28), (sg.get("label") or sg.get("hunt") or sg["style"])[:18], fill=(90, 90, 90), font=f_s)
        yaw0 = ch["yaw"][0]
        data = {
            "armR.x": [v[0] for v in ch["armR"]], "armL.x": [v[0] for v in ch["armL"]], "lean": ch["lean"],
            "offset.y x1000": [v[1] * 1000 for v in ch["offset"]],
            "yaw-yaw0": [((y - yaw0 + 180) % 360) - 180 for y in ch["yaw"]],
            "squash x100": [v * 100 for v in ch["squash"]], "speed x500": [v * 500 for v in ch["speed"]],
        }
        lo = min(min(v) for v in data.values())
        hi = max(max(v) for v in data.values())
        lo, hi = min(lo, -10), max(hi, 10)

        def ty(v):
            return y0 + panel_h - 6 - (v - lo) / (hi - lo) * (panel_h - 12)
        dr.line([left, ty(0), left + pw, ty(0)], fill=(215, 215, 215))
        for g in range(int(lo // 45) * 45, int(hi) + 1, 45):
            dr.text((left - 34, ty(g) - 7), f"{g:>4}", fill=(140, 140, 140), font=f_s)
        for name, col in series_cols:
            v = data[name]
            pts = [(tx(T[i]), ty(v[i])) for i in range(n)]
            dr.line(pts, fill=col, width=2)
        hits = ch["hit"]
        for i in range(n):
            if hits[i] > 0.5:
                dr.line([tx(T[i]), y0 + panel_h - 4, tx(T[i]), y0 + panel_h], fill=(230, 30, 30), width=3)
        dr.text((6, y0 + 4), d["id"], fill=(30, 30, 30), font=font(14, True))
        lx = left + pw + 10
        for j, (name, col) in enumerate(series_cols):
            dr.line([lx, y0 + 12 + j * 16, lx + 18, y0 + 12 + j * 16], fill=col, width=3)
            dr.text((lx + 22, y0 + 5 + j * 16), name, fill=(50, 50, 50), font=f_s)
        dr.text((lx, y0 + 12 + len(series_cols) * 16), "red ticks: hit", fill=(200, 30, 30), font=f_s)

    if objs:
        y0 = 40 + len(dolls) * (panel_h + pad)
        dr.rectangle([left, y0, left + pw, y0 + panel_h], fill=(255, 255, 255), outline=(200, 200, 200))
        dr.text((6, y0 + 4), "objects", fill=(30, 30, 30), font=font(14, True))
        cols = [(150, 110, 70), (90, 90, 200), (60, 150, 90)]
        top = 0.0
        speeds = []
        for o in objs:
            p = o["ch"]["pos"]
            sp = [0.0] + [math.dist(p[i], p[i - 1]) * fps for i in range(1, n)]
            speeds.append(sp)
            top = max(top, max(sp))
        top = max(top, 0.5)
        for j, o in enumerate(objs):
            sp = speeds[j]
            pts = [(tx(T[i]), y0 + panel_h - 8 - sp[i] / top * (panel_h - 40)) for i in range(n)]
            dr.line(pts, fill=cols[j % 3], width=2)
            acts = o["ch"]["act"]
            prev = None
            for i in range(n):
                if acts[i] != prev:
                    x = tx(T[i])
                    dr.line([x, y0 + 18 + j * 14, x, y0 + 30 + j * 14], fill=cols[j % 3], width=2)
                    dr.text((x + 2, y0 + 17 + j * 14), acts[i] or "-", fill=cols[j % 3], font=f_s)
                    prev = acts[i]
            lx = left + pw + 10
            dr.line([lx, y0 + 12 + j * 16, lx + 18, y0 + 12 + j * 16], fill=cols[j % 3], width=3)
            dr.text((lx + 22, y0 + 5 + j * 16), f"{o['id']} speed (top {top:.2f} u/s)", fill=(50, 50, 50), font=f_s)
        for h in track.get("hits", []):
            x = tx(h["t0"])
            dr.line([x, y0, x, y0 + panel_h], fill=(230, 30, 30), width=1)
    # time axis
    ya = H - 14
    for s in range(0, int(dur) + 1):
        dr.text((tx(s) - 6, ya - 2), f"{s}s", fill=(90, 90, 90), font=f_s)
    img.save(out)


# ----------------------------------------------------------------------------------------------- video
def find_ffmpeg(explicit=None):
    cands = [explicit, os.environ.get("ANIMBENCH_FFMPEG"), shutil.which("ffmpeg")]
    la = os.environ.get("LOCALAPPDATA")
    if la:
        pk = os.path.join(la, "Microsoft", "WinGet", "Packages")
        if os.path.isdir(pk):
            for d in sorted(os.listdir(pk)):
                if d.startswith("Gyan.FFmpeg"):
                    for root, _, files in os.walk(os.path.join(pk, d)):
                        if "ffmpeg.exe" in files:
                            cands.append(os.path.join(root, "ffmpeg.exe"))
    for c in cands:
        if c and os.path.exists(c):
            return c
    return None


def make_video(track, scene, outdir, ffmpeg, vw=480, vh=330):
    fdir = os.path.join(outdir, "frames")
    if os.path.isdir(fdir):
        shutil.rmtree(fdir)
    os.makedirs(fdir)
    sw = int(vw * 0.5)
    main, side = _video_cams(track, scene, vw, sw, vh)
    fps = track["fps"]
    W = vw + sw + 2
    n = track["frames"]
    jobs_env = int(os.environ.get("ANIMBENCH_JOBS", "0") or 0)
    workers = max(1, min(jobs_env or max(1, (os.cpu_count() or 2) - 1), 8, n // 40 + 1))
    chunks = [list(range(k, n, workers)) for k in range(workers)]
    if workers == 1:
        _render_frames((scene, main, side, vw, sw, vh, fps, fdir), chunks[0])
    else:
        import multiprocessing as mp
        with mp.get_context("spawn").Pool(workers, initializer=_init_worker, initargs=(outdir, track.get("camera", {}), vw, sw, vh, fdir)) as pool:
            pool.map(_render_chunk, chunks)
    pattern = os.path.join(fdir, "%05d.bmp")
    gif_vf = "scale={w}:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=96:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4"
    jobs = [
        [ffmpeg, "-y", "-loglevel", "error", "-framerate", f"{fps:g}", "-i", pattern, "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23",
         "-pix_fmt", "yuv420p", "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2", os.path.join(outdir, "clip.mp4")],
        [ffmpeg, "-y", "-loglevel", "error", "-framerate", f"{fps / 4:g}", "-i", pattern, "-c:v", "libx264", "-preset", "ultrafast", "-crf", "23",
         "-pix_fmt", "yuv420p", "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2", os.path.join(outdir, "clip-0.25x.mp4")],
        [ffmpeg, "-y", "-loglevel", "error", "-framerate", f"{fps:g}", "-i", pattern, "-vf", "fps=15," + gif_vf.format(w=W), os.path.join(outdir, "clip.gif")],
        [ffmpeg, "-y", "-loglevel", "error", "-framerate", f"{fps / 4:g}", "-i", pattern, "-vf", gif_vf.format(w=W), os.path.join(outdir, "clip-0.25x.gif")],
    ]
    procs = [subprocess.Popen(j, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE) for j in jobs]
    errs = []
    for p, j in zip(procs, jobs):
        try:
            _, err = p.communicate(timeout=120)
        except subprocess.TimeoutExpired:
            p.kill()
            errs.append(f"ffmpeg timed out: {j[-1]}")
            continue
        if p.returncode != 0:
            errs.append(f"ffmpeg failed for {os.path.basename(j[-1])}: {err.decode(errors='replace')[-400:]}")
    shutil.rmtree(fdir, ignore_errors=True)
    return errs


_W = None


def _video_cams(track, scene, vw, sw, vh):
    camspec = track.get("camera", {})
    main = Cam(*view_angles(camspec.get("view", "default"), camspec), vw, vh)
    side = Cam(*view_angles("side", camspec), sw, vh)
    pts = scene.fit_points()
    main.fit(pts, focus=camspec.get("focus"), span=camspec.get("span"))
    side.fit(pts, span=camspec.get("span"), focus=camspec.get("focus"))
    return main, side


def _init_worker(outdir, camspec, vw, sw, vh, fdir):
    global _W
    track = json.load(open(os.path.join(outdir, "track.json"), encoding="utf-8"))
    track["camera"] = camspec
    render = json.load(open(os.path.join(outdir, "render.json"), encoding="utf-8"))
    scene = Scene(track, render)
    main, side = _video_cams(track, scene, vw, sw, vh)
    _W = (scene, main, side, vw, sw, vh, track["fps"], fdir)


def _render_chunk(frames):
    _render_frames(_W, frames)
    return len(frames)


def _render_frames(ctx, frames):
    scene, main, side, vw, sw, vh, fps, fdir = ctx
    fnt = font(13)
    W = vw + sw + 2
    for f in frames:
        img = Image.new("RGB", (W, vh), (40, 40, 40))
        a = Image.new("RGB", (vw, vh))
        scene.draw(a, main, f, labels=True)
        img.paste(a, (0, 0))
        b = Image.new("RGB", (sw, vh))
        scene.draw(b, side, f)
        img.paste(b, (vw + 2, 0))
        d = ImageDraw.Draw(img)
        hit = f in scene.hit_frames
        d.rectangle([0, 0, 150, 18], fill=(255, 255, 255))
        d.text((4, 2), f"f{f}  {f / fps:5.2f}s" + ("  HIT" if hit else ""), fill=(220, 30, 30) if hit else (20, 20, 20), font=fnt)
        d.text((vw + 6, 2), "side", fill=(240, 240, 240), font=fnt)
        if hit:
            d.rectangle([0, 0, W - 1, vh - 1], outline=(220, 30, 30), width=3)
        img.save(os.path.join(fdir, f"{f:05d}.bmp"))


# ----------------------------------------------------------------------------------------------- main
def main(argv):
    args = [a for a in argv if not a.startswith("--")]
    flags = [a for a in argv if a.startswith("--")]
    if not args:
        print(__doc__)
        return 2
    outdir = os.path.abspath(args[0])
    no_video = "--no-video" in flags
    ff = None
    for i, a in enumerate(argv):
        if a == "--ffmpeg" and i + 1 < len(argv):
            ff = argv[i + 1]
    t0 = time.time()
    track = json.load(open(os.path.join(outdir, "track.json"), encoding="utf-8"))
    render = json.load(open(os.path.join(outdir, "render.json"), encoding="utf-8"))
    cam = track.setdefault("camera", {})
    if "sideYaw" not in cam:
        sy = action_side_yaw(track)
        if sy is not None:
            cam["sideYaw"] = sy
    scene = Scene(track, render)
    frames = make_sheet(track, scene, os.path.join(outdir, "sheet.png"))
    t1 = time.time()
    make_curves(track, os.path.join(outdir, "curves.png"))
    t2 = time.time()
    msg = f"render: sheet {len(frames)} frames {t1 - t0:.2f}s, curves {t2 - t1:.2f}s"
    if not no_video:
        ffmpeg = find_ffmpeg(ff)
        if ffmpeg is None:
            msg += ", video skipped (no ffmpeg: set ANIMBENCH_FFMPEG)"
        else:
            errs = make_video(track, scene, outdir, ffmpeg)
            msg += f", video {time.time() - t2:.2f}s"
            for e in errs:
                print("render: " + e, file=sys.stderr)
    print(msg)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
