"""#353 AnimLab prep: AnimBench's track (+ render.json, + the clip) -> lab.json, flat enough for Unity's JsonUtility.

Usage:
  python Tools/animlab/prep.py lab <clip.json> <bench out dir> <lab.json>
  python Tools/animlab/prep.py batch <batch.json> <lab.json> [<lab.json> ...]   (out dir = each lab.json's folder)
  python Tools/animlab/prep.py live <clip.json> <out dir>      (a live request: lab.json, and prints AnimLabLive's spec)
  python Tools/animlab/prep.py field <clip.json> <a.b.c> [default]

The poses are AnimBench's: the same clip run through the game's DollMotion / beat kit, frame for frame (same fps, same frame
times). The Editor only puts them on the real dolls, so the timing matches AnimBench by construction; the Editor's own check is
the geometry (real hand and tool tip vs Bench.Place), written to lab-check.tsv.
"""
import json
import os
import sys
import time

DOLL_STRIDE = 26
OBJ_STRIDE = 7


def r4(v):
    return round(float(v), 4)


def slots_for(clip_doll):
    """The prop slot per timeline segment, as AnimBench's DollSpec.Parse resolves it (segment, else doll, else Hand)."""
    tl = clip_doll.get("timeline") or [{}]
    out = []
    for so in tl:
        out.append(so.get("slot", clip_doll.get("slot", "Hand")))
    return out


def fit_camera(track):
    xs, zs = [], []
    for d in track["dolls"]:
        xs += d["ch"]["x"]
        zs += d["ch"]["z"]
    for o in track["objects"]:
        for p in o["ch"]["pos"]:
            xs.append(p[0])
            zs.append(p[2])
    if not xs:
        return 0.0, 0.0, 1.5
    cx, cz = (min(xs) + max(xs)) / 2, (min(zs) + max(zs)) / 2
    span = max(max(xs) - min(xs), (max(zs) - min(zs)) * 1.3) + 0.5
    return cx, cz, max(0.8, span)


def close_camera(track, default_span):
    """Tight on the action: the first hit (the doll and its target), else the dolls over the sheet window."""
    fps = track["fps"]
    # Everyone over the sheet window (and the first hit's target), when that fits a close frame.
    sh = track.get("sheet", {})
    f0 = int(round(sh.get("from", 0) * fps))
    f1 = int(round(min(sh.get("to", track["duration"]), track["duration"]) * fps))
    xs, zs = [], []
    for d in track["dolls"]:
        xs += d["ch"]["x"][f0:f1 + 1]
        zs += d["ch"]["z"][f0:f1 + 1]
    if xs:
        ext = max(max(xs) - min(xs), max(zs) - min(zs))
        if ext + 0.4 <= 0.95:
            cx, cz = (min(xs) + max(xs)) / 2, (min(zs) + max(zs)) / 2
            return cx, cz, max(0.55, min(ext + 0.4, 0.8 * default_span))
    hits = track.get("hits", [])
    dolls = {d["id"]: d for d in track["dolls"]}
    objs = {o["id"]: o for o in track["objects"]}
    if hits:
        h = hits[0]
        d = dolls[h["doll"]]["ch"]
        f = h["f0"]
        px, pz = d["x"][f], d["z"][f]
        tx, tz = px, pz
        tgt = h.get("target")
        if tgt in objs:
            p = objs[tgt]["ch"]["pos"][f]
            tx, tz = p[0], p[2]
        elif tgt in dolls:
            tx, tz = dolls[tgt]["ch"]["x"][f], dolls[tgt]["ch"]["z"][f]
        dist = ((tx - px) ** 2 + (tz - pz) ** 2) ** 0.5
        return (px + tx) / 2, (pz + tz) / 2, max(0.55, dist + 0.4)
    sh = track.get("sheet", {})
    f0 = int(round(sh.get("from", 0) * fps))
    f1 = int(round(min(sh.get("to", track["duration"]), track["duration"]) * fps))
    xs, zs = [], []
    for d in track["dolls"]:
        xs += d["ch"]["x"][f0:f1 + 1]
        zs += d["ch"]["z"][f0:f1 + 1]
    if not xs:
        return 0.0, 0.0, 0.6
    cx, cz = (min(xs) + max(xs)) / 2, (min(zs) + max(zs)) / 2
    ext = max(max(xs) - min(xs), max(zs) - min(zs))
    return cx, cz, max(0.5, min(ext + 0.35, 0.75 * default_span))


def make_lab(clip_path, bench_dir, lab_path):
    clip = json.load(open(clip_path, encoding="utf-8"))
    track = json.load(open(os.path.join(bench_dir, "track.json"), encoding="utf-8"))
    render = json.load(open(os.path.join(bench_dir, "render.json"), encoding="utf-8"))
    n = track["frames"]
    head = clip.get("animlab", {}) or {}
    cam = track.get("camera", {})
    yaw, pitch = cam.get("yaw", 28.0), cam.get("pitch", 34.0)
    if "focus" in cam and cam.get("span"):
        fx, fz, span = cam["focus"][0], cam["focus"][1], cam["span"]
    else:
        fx, fz, span = fit_camera(track)
    cx, cz, cspan = close_camera(track, span)

    clip_dolls = {d.get("id", f"doll{i}"): d for i, d in enumerate(clip.get("dolls", []))}
    hidden = {d["id"]: d["hidden"] for d in render["dolls"]}
    dolls = []
    for d in track["dolls"]:
        ch = d["ch"]
        slots = slots_for(clip_dolls.get(d["id"], {}))
        f = []
        props = []
        for i in range(n):
            off, al, ar = ch["offset"][i], ch["armL"][i], ch["armR"][i]
            tip, hand = ch["tip"][i], ch["handR"][i]
            lying = 1.0 if ch["style"][i] == "Lie" else 0.0
            f += [ch["x"][i], ch["z"][i], ch["bodyYaw"][i], ch["yaw"][i], off[0], off[1], off[2], ch["lean"][i], ch["roll"][i],
                  ch["squash"][i], al[0], al[1], al[2], ar[0], ar[1], ar[2], ch["extend"][i], ch["hit"][i],
                  1.0 if hidden[d["id"]][i] else 0.0, lying, tip[0], tip[1], tip[2], hand[0], hand[1], hand[2]]
            seg = int(ch["seg"][i])
            props.append(f'{ch["prop"][i]}/{slots[min(seg, len(slots) - 1)]}')
        assert len(f) == n * DOLL_STRIDE
        dolls.append({"id": d["id"], "kid": d["kid"], "camp": d["camp"], "seed": d["seed"], "scale": d["scale"],
                      "f": [r4(v) for v in f], "prop": props})
    objects = []
    for o in track["objects"]:
        ch = o["ch"]
        f = []
        for i in range(n):
            p = ch["pos"][i]
            f += [p[0], p[1], p[2], ch["yaw"][i], ch["visible"][i], ch["phase"][i], ch.get("berries", [0] * n)[i]]
        size = o.get("size", [0, 0])
        objects.append({"id": o["id"], "kind": o["kind"], "radius": o["radius"], "sx": size[0], "sz": size[1],
                        "f": [r4(v) for v in f], "act": list(ch["act"])})
    lab = {
        "format": "animlab/1", "clip": os.path.splitext(os.path.basename(clip_path))[0], "request": os.path.abspath(clip_path),
        "issue": str(head.get("issue", "")), "branch": str(head.get("branch", "")), "commit": str(head.get("commit", "")),
        "fps": track["fps"], "clock": clip.get("clock", 0), "frames": n,
        "cam": {"fx": fx, "fz": fz, "span": span, "yaw": yaw, "pitch": pitch},
        "close": {"fx": cx, "fz": cz, "span": cspan, "yaw": yaw, "pitch": pitch},
        "dolls": dolls, "objects": objects,
    }
    os.makedirs(os.path.dirname(os.path.abspath(lab_path)), exist_ok=True)
    with open(lab_path, "w", encoding="utf-8") as fh:
        json.dump(lab, fh, separators=(",", ":"))
    return n


def make_batch(batch_path, labs):
    total = 0
    for p in labs:
        with open(p, encoding="utf-8") as fh:
            total = max(total, json.load(fh)["frames"])
    batch = {"id": time.strftime("%Y%m%d-%H%M%S"), "labs": [os.path.abspath(p) for p in labs],
             "outs": [os.path.dirname(os.path.abspath(p)) for p in labs], "next": 0, "total": total,
             "dw": 480, "dh": 360, "cw": 360, "ch": 360, "prevScene": "", "renderSeconds": 0, "rendered": 0, "calls": 0, "done": False}
    with open(batch_path, "w", encoding="utf-8") as fh:
        json.dump(batch, fh, indent=1)
    return batch


def make_live(clip_path, out_dir):
    """A live request (clip JSON with a "live" section): writes out_dir/lab.json and prints the LiveSpec JSON for AnimLabLive.Begin."""
    clip = json.load(open(clip_path, encoding="utf-8"))
    live = clip["live"]
    head = clip.get("animlab", {}) or {}
    name = os.path.splitext(os.path.basename(clip_path))[0]
    fps = float(live.get("fps", 30))
    seconds = float(live.get("seconds", 6))
    focus = live.get("focus", [0.0, 0.0])
    spec = {"clip": name, "outDir": os.path.abspath(out_dir), "warm": float(live.get("warm", 1)), "seconds": seconds, "fps": fps,
            "follow": live.get("follow", ""), "fx": focus[0], "fz": focus[1], "span": float(live.get("span", 1.5)),
            "closeSpan": float(live.get("closeSpan", 0.6)), "yaw": float(live.get("yaw", 28)), "pitch": float(live.get("pitch", 34))}
    os.makedirs(out_dir, exist_ok=True)
    lab = {"format": "animlab-live/1", "clip": name, "request": os.path.abspath(clip_path), "issue": str(head.get("issue", "")),
           "branch": str(head.get("branch", "")), "commit": str(head.get("commit", "")), "fps": fps, "frames": int(round(seconds * fps)) + 1}
    with open(os.path.join(out_dir, "lab.json"), "w", encoding="utf-8") as fh:
        json.dump(lab, fh)
    print(json.dumps(spec))


def field(clip_path, path, default=""):
    v = json.load(open(clip_path, encoding="utf-8"))
    for k in path.split("."):
        if not isinstance(v, dict) or k not in v:
            print(default)
            return
        v = v[k]
    print(v if not isinstance(v, (dict, list)) else json.dumps(v))


def setup_steps(clip_path):
    """live.setup: one C# snippet or a list of them (the driver lets the game run ~2 s between steps). Printed one per line, JSON-quoted."""
    live = json.load(open(clip_path, encoding="utf-8")).get("live", {})
    steps = live.get("setup", [])
    if isinstance(steps, str):
        steps = [steps]
    for st in steps:
        print(json.dumps(st))


def main(argv):
    if len(argv) >= 2 and argv[0] == "steps":
        setup_steps(argv[1])
        return 0
    if len(argv) >= 3 and argv[0] == "live":
        make_live(argv[1], argv[2])
        return 0
    if len(argv) >= 3 and argv[0] == "field":
        field(argv[1], argv[2], argv[3] if len(argv) > 3 else "")
        return 0
    if len(argv) >= 4 and argv[0] == "lab":
        n = make_lab(argv[1], argv[2], argv[3])
        print(f"lab {argv[3]}: {n} frames")
        return 0
    if len(argv) >= 3 and argv[0] == "batch":
        b = make_batch(argv[1], argv[2:])
        print(f"batch {b['id']}: {len(b['labs'])} clips, {b['total']} frames")
        return 0
    print(__doc__, file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
