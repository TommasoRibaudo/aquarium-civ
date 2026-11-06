"""#353 AnimLab post: the Editor's frames -> videos, gifs and contact sheets (no Unity).

Usage: python Tools/animlab/post.py <batch.json> [--ffmpeg PATH]

Per clip (out/<clip>/):
  clip.mp4, clip-0.25x.mp4   default 3/4 camera | close camera, side by side (840x360), at 1x and quarter speed
  clip.gif, clip-0.25x.gif   the same, 480 px wide (12 fps; the 0.25x gif only the sheet window, at 7.5 fps)
  sheet.png                  AnimBench's frame pick (sheet.from/to/frames, every hit's first frame pulled on), each cell the
                             default view over the close view; red frame + red ring at the tool tip on hit frames
  check.txt                  hit frames and the geometry check (real doll's hand / tool tip vs AnimBench's Bench.Place)
Per batch (out/_batch/<id>/): grid.mp4, grid-0.25x.mp4, grid.gif: every clip's default view side by side, labelled
(<= 1080 px wide; a short clip holds its last frame), and grid.png (each clip at its first hit, or its middle).
"""
import json
import os
import shutil
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "AnimBench"))


def find_ffmpeg(explicit=None):
    try:
        import render  # AnimBench's finder (PATH, ANIMBENCH_FFMPEG, winget Gyan.FFmpeg)
        return render.find_ffmpeg(explicit)
    except Exception:
        return explicit or shutil.which("ffmpeg")


def font(size):
    for f in ("C:/Windows/Fonts/segoeui.ttf", "C:/Windows/Fonts/arial.ttf"):
        if os.path.exists(f):
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


def read_check(out):
    """lab-check.tsv rows by (frame, doll), the last write wins (a resumed chunk may repeat frames)."""
    rows = {}
    p = os.path.join(out, "lab-check.tsv")
    if not os.path.exists(p):
        return rows
    for line in open(p, encoding="utf-8"):
        c = line.rstrip("\n").split("\t")
        if len(c) < 9:
            continue
        rows[(int(c[0]), c[1])] = {"hit": float(c[2]), "hand": float(c[3]), "tip": float(c[4]),
                                    "d": (float(c[5]), float(c[6])), "c": (float(c[7]), float(c[8]))}
    return rows


def pick_frames(track):
    """AnimBench render.py's pick_frames (same frames on both sheets)."""
    sh = track.get("sheet", {})
    n, fps = track["frames"], track["fps"]
    t0, t1 = sh.get("from", 0.0), sh.get("to", track["duration"])
    count = max(1, int(sh.get("frames", 24)))
    t1 = min(t1, (n - 1) / fps)
    if count == 1:
        return [int(round(t0 * fps))]
    frames = [min(n - 1, int(round((t0 + (t1 - t0) * k / (count - 1)) * fps))) for k in range(count)]
    for h in track.get("hits", []):
        if not (t0 - 1e-6 <= h["t0"] <= t1 + 1e-6):
            continue
        if any(h["f0"] <= f <= h["f1"] for f in frames):
            continue
        k = min(range(count), key=lambda i: abs(frames[i] - h["f0"]))
        frames[k] = h["f0"]
    return sorted(set(frames))


def ring(draw, x, y, r, col, w=2):
    draw.ellipse([x - r, y - r, x + r, y + r], outline=col, width=w)


def make_sheet(out, lab, track, check):
    frames = pick_frames(track)
    cols = max(1, int(track.get("sheet", {}).get("columns", 6)))
    tw = min(240, 1080 // cols)
    dh, ch = int(tw * 360 / 480), tw
    rows = (len(frames) + cols - 1) // cols
    head = 74
    W, H = cols * tw, head + rows * (dh + ch + 18)
    sheet = Image.new("RGB", (W, H), (246, 242, 234))
    dr = ImageDraw.Draw(sheet)
    f14, f11 = font(15), font(11)
    hits = track.get("hits", [])
    hitset = {f for h in hits for f in range(h["f0"], h["f1"] + 1)}
    tag = " · ".join(x for x in (lab.get("issue"), lab.get("branch"), (lab.get("commit") or "")[:9]) if x)
    dr.text((8, 4), f"AnimLab {lab['clip']}  ({track['duration']:g} s @ {track['fps']:g} fps, {track['frames']} frames)  {tag}", fill=(30, 30, 30), font=f14)
    hs = ", ".join(f"{h['doll']} f{h['f0']}-{h['f1']}" for h in hits) or "none"
    dr.text((8, 24), f"hits (AnimBench track): {hs}", fill=(170, 30, 30), font=f11)
    hand_err = max((r["hand"] for r in check.values()), default=0.0)
    tip_err = max((r["tip"] for r in check.values() if r["hit"] > 0.5), default=0.0)
    dr.text((8, 40), f"real doll vs AnimBench: hand within {hand_err * 1000:.1f} mu, tool tip on hits within {tip_err * 1000:.1f} mu (1 mu = 0.001 box u; doll width 110)", fill=(60, 60, 60), font=f11)
    dr.text((8, 55), "each cell: default 3/4 camera over the close camera. Red frame + ring at the tool tip = DollPose.hit", fill=(90, 90, 90), font=f11)
    for i, f in enumerate(frames):
        x, y = (i % cols) * tw, head + (i // cols) * (dh + ch + 18)
        d = Image.open(os.path.join(out, "frames", f"d_{f:05d}.jpg")).resize((tw, dh), Image.LANCZOS)
        c = Image.open(os.path.join(out, "frames", f"c_{f:05d}.jpg")).resize((tw, ch), Image.LANCZOS)
        sheet.paste(d, (x, y + 16))
        sheet.paste(c, (x, y + 16 + dh))
        hit = f in hitset
        dr.text((x + 4, y + 1), f"f{f}  {f / track['fps']:.2f}s" + ("  HIT" if hit else ""), fill=(190, 20, 20) if hit else (40, 40, 40), font=f11)
        if hit:
            dr.rectangle([x + 1, y + 16, x + tw - 2, y + 16 + dh + ch - 1], outline=(220, 30, 30), width=3)
            for (fr, doll), r in check.items():
                if fr == f and r["hit"] > 0.5:
                    ring(dr, x + r["d"][0] * tw, y + 16 + (1 - r["d"][1]) * dh, 6, (230, 20, 20))
                    ring(dr, x + r["c"][0] * tw, y + 16 + dh + (1 - r["c"][1]) * ch, 8, (230, 20, 20))
    sheet.save(os.path.join(out, "sheet.png"))
    with open(os.path.join(out, "check.txt"), "w", encoding="utf-8") as fh:
        fh.write(f"clip {lab['clip']}: {track['frames']} frames at {track['fps']:g} fps (AnimBench's frame times)\n")
        fh.write(f"hits: {hs}\n")
        fh.write(f"max |real hand - bench hand| over all frames: {hand_err:.4f} u\n")
        fh.write(f"max |real tool tip - bench tip| on hit frames: {tip_err:.4f} u\n")
    return hand_err, tip_err


def run(jobs):
    procs = [subprocess.Popen(j, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE) for j in jobs]
    errs = []
    for p, j in zip(procs, jobs):
        try:
            _, err = p.communicate(timeout=180)
        except subprocess.TimeoutExpired:
            p.kill()
            errs.append(f"ffmpeg timed out: {j[-1]}")
            continue
        if p.returncode != 0:
            errs.append(f"ffmpeg failed for {os.path.basename(j[-1])}: {err.decode(errors='replace')[-300:]}")
    return errs


GIF = "scale={w}:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=64:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=4"


def clip_videos(out, track, ff):
    """mp4s: the whole clip. gifs (for a phone): 480 px wide; the 0.25x gif covers only the sheet window, so it stays small."""
    fps = track["fps"]
    d = os.path.join(out, "frames", "d_%05d.jpg")
    c = os.path.join(out, "frames", "c_%05d.jpg")
    base = [ff, "-y", "-loglevel", "error"]
    sh = track.get("sheet", {})
    f0 = int(round(sh.get("from", 0) * fps))
    f1 = min(track["frames"] - 1, int(round(min(sh.get("to", track["duration"]), track["duration"]) * fps)))
    jobs = []
    for rate, suffix in ((fps, ""), (fps / 4, "-0.25x")):
        inp = ["-framerate", f"{rate:g}", "-i", d, "-framerate", f"{rate:g}", "-i", c]
        jobs.append(base + inp + ["-filter_complex", "[0:v][1:v]hstack=inputs=2,format=yuv420p", "-c:v", "libx264", "-preset", "veryfast", "-crf", "22",
                                  os.path.join(out, f"clip{suffix}.mp4")])
        win = ["-start_number", str(f0), "-frames:v", str(f1 - f0 + 1)] if suffix else []
        ginp = ["-framerate", f"{rate:g}"] + win[:2] + ["-i", d, "-framerate", f"{rate:g}"] + win[:2] + ["-i", c]
        gfps = "fps=12," if suffix == "" else ""
        jobs.append(base + ginp + ["-filter_complex", "[0:v][1:v]hstack=inputs=2," + gfps + GIF.format(w=480)] + win[2:] + [os.path.join(out, f"clip{suffix}.gif")])
    return run(jobs)


def grid(batch, labs, tracks, ff):
    outdir = os.path.join(os.path.dirname(batch["outs"][0]), "_batch", batch["id"])
    fdir = os.path.join(outdir, "frames")
    if os.path.isdir(fdir):
        shutil.rmtree(fdir)
    os.makedirs(fdir)
    k = len(labs)
    cols = min(3, k)
    rows = (k + cols - 1) // cols
    tw, th, lab_h = 360, 270, 22
    W, H = cols * tw, rows * (th + lab_h)
    f13 = font(14)
    n = max(t["frames"] for t in tracks)
    fps = tracks[0]["fps"]
    hitsets = [{f for h in t.get("hits", []) for f in range(h["f0"], h["f1"] + 1)} for t in tracks]
    for f in range(n):
        im = Image.new("RGB", (W, H), (246, 242, 234))
        dr = ImageDraw.Draw(im)
        for i, (lab, t, out) in enumerate(zip(labs, tracks, batch["outs"])):
            x, y = (i % cols) * tw, (i // cols) * (th + lab_h)
            ff_ = min(f, t["frames"] - 1)
            tile = Image.open(os.path.join(out, "frames", f"d_{ff_:05d}.jpg")).resize((tw, th), Image.LANCZOS)
            im.paste(tile, (x, y + lab_h))
            hit = ff_ in hitsets[i]
            dr.text((x + 5, y + 2), f"{lab['clip']}  f{ff_}" + ("  HIT" if hit else ""), fill=(190, 20, 20) if hit else (30, 30, 30), font=f13)
            if hit:
                dr.rectangle([x + 1, y + lab_h, x + tw - 2, y + lab_h + th - 1], outline=(220, 30, 30), width=3)
        im.save(os.path.join(fdir, f"{f:05d}.jpg"), quality=90)
    still = Image.new("RGB", (W, H), (246, 242, 234))
    dr = ImageDraw.Draw(still)
    for i, (lab, t, out) in enumerate(zip(labs, tracks, batch["outs"])):
        hits = t.get("hits", [])
        f = hits[0]["f0"] if hits else t["frames"] // 2
        x, y = (i % cols) * tw, (i // cols) * (th + lab_h)
        still.paste(Image.open(os.path.join(out, "frames", f"d_{f:05d}.jpg")).resize((tw, th), Image.LANCZOS), (x, y + lab_h))
        dr.text((x + 5, y + 2), f"{lab['clip']}  f{f}" + ("  first hit" if hits else "  middle"), fill=(30, 30, 30), font=f13)
    still.save(os.path.join(outdir, "grid.png"))
    pat = os.path.join(fdir, "%05d.jpg")
    base = [ff, "-y", "-loglevel", "error"]
    errs = run([
        base + ["-framerate", f"{fps:g}", "-i", pat, "-c:v", "libx264", "-preset", "veryfast", "-crf", "22", "-pix_fmt", "yuv420p", os.path.join(outdir, "grid.mp4")],
        base + ["-framerate", f"{fps / 4:g}", "-i", pat, "-c:v", "libx264", "-preset", "veryfast", "-crf", "22", "-pix_fmt", "yuv420p", os.path.join(outdir, "grid-0.25x.mp4")],
        base + ["-framerate", f"{fps:g}", "-i", pat, "-vf", "fps=10," + GIF.format(w=min(W, 720)), os.path.join(outdir, "grid.gif")],
    ])
    shutil.rmtree(fdir, ignore_errors=True)
    return outdir, errs


def main(argv):
    if not argv:
        print(__doc__, file=sys.stderr)
        return 2
    ff = None
    if "--ffmpeg" in argv:
        ff = argv[argv.index("--ffmpeg") + 1]
    batch = json.load(open(argv[0], encoding="utf-8"))
    ff = find_ffmpeg(ff)
    if not ff:
        print("AnimLab post: no ffmpeg (set ANIMBENCH_FFMPEG)", file=sys.stderr)
        return 2
    labs, tracks, errs = [], [], []
    for lab_path, out in zip(batch["labs"], batch["outs"]):
        lab = json.load(open(lab_path, encoding="utf-8"))
        track = json.load(open(os.path.join(out, "bench", "track.json"), encoding="utf-8"))
        labs.append(lab)
        tracks.append(track)
        hand, tip = make_sheet(out, lab, track, read_check(out))
        errs += clip_videos(out, track, ff)
        print(f"{lab['clip']}: {out}/sheet.png, clip.mp4 (hand err {hand:.4f} u, tip err on hits {tip:.4f} u)")
    gdir, gerrs = grid(batch, labs, tracks, ff)
    errs += gerrs
    print(f"grid: {gdir}/grid.mp4, grid.png")
    for e in errs:
        print("  " + e, file=sys.stderr)
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
