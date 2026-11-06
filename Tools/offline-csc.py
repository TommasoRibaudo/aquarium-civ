"""Compile-check Unity assemblies offline (no Editor), from a worktree's sources.

Usage:  /c/Python311/python Tools/offline-csc.py <worktree> <Assembly> [<Assembly> ...]
  e.g.  /c/Python311/python Tools/offline-csc.py F:/GameDev-wt/368 AquariumCiv.Sim AquariumCiv.View AquariumCiv.View.Tests

Each named assembly (asmdef name) is compiled from <worktree>'s sources, in the order given, against Unity's
engine DLLs, the project's ScriptAssemblies (F:/GameDev/AquariumCiv/Library, from the Editor's last compile) and the
assemblies compiled earlier in this run (they replace their ScriptAssemblies copy). Name every assembly you changed
AND the ones that depend on what you changed (e.g. a Sim change: Sim, Life, View, View.Tests).
Outputs go to %TEMP%/offline-csc/<hash>/; nothing is written to the repo. Exit 0 = zero errors.
tooling.md 2026-10-29 has the recipe this automates.
"""
import hashlib, json, os, subprocess, sys, tempfile

UNITY = "C:/Program Files/Unity/Hub/Editor/6000.3.27f1/Editor"
LIB = "F:/GameDev/AquariumCiv/Library/ScriptAssemblies"
PKG = "F:/GameDev/AquariumCiv/Library/PackageCache"


def asmdefs(root):
    out = {}
    for d, _, files in os.walk(os.path.join(root, "AquariumCiv", "Assets")):
        for f in files:
            if f.endswith(".asmdef"):
                with open(os.path.join(d, f), encoding="utf-8-sig") as fh:
                    out[json.load(fh)["name"]] = d.replace("\\", "/")
    return out


def sources(folder, all_dirs):
    own = set(all_dirs) - {folder}
    srcs = []
    for d, subdirs, files in os.walk(folder):
        d = d.replace("\\", "/")
        subdirs[:] = [s for s in subdirs if (d + "/" + s) not in own and not s.endswith("~")]
        srcs += [d + "/" + f for f in files if f.endswith(".cs")]
    return sorted(srcs)


def main():
    if len(sys.argv) < 3:
        print(__doc__); return 2
    wt = sys.argv[1].replace("\\", "/").rstrip("/")
    names = sys.argv[2:]
    defs = asmdefs(wt)
    out = os.path.join(tempfile.gettempdir(), "offline-csc",
                       hashlib.md5((wt + "|".join(names)).encode()).hexdigest()[:10]).replace("\\", "/")
    os.makedirs(out, exist_ok=True)
    eng = [f"{UNITY}/Data/Managed/UnityEngine/{f}" for f in os.listdir(f"{UNITY}/Data/Managed/UnityEngine") if f.endswith(".dll")]
    nunit = [f"{PKG}/{p}/net40/unity-custom/nunit.framework.dll" for p in os.listdir(PKG) if p.startswith("com.unity.ext.nunit")]
    built = {}
    worst = 0
    for name in names:
        if name not in defs:
            print(f"!! no asmdef named {name}"); return 2
        srcs = sources(defs[name], defs.values())
        refs = [f"{UNITY}/Data/NetStandard/ref/2.1.0/netstandard.dll"] + eng
        refs += [f"{LIB}/{f}" for f in os.listdir(LIB) if f.endswith(".dll") and f[:-4] not in built and f[:-4] != name]
        refs += list(built.values())
        if name.endswith("Tests"):
            refs += nunit + [f"{UNITY}/Data/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll"]
        dll = f"{out}/{name}.dll"
        rsp = f"{out}/{name}.rsp"
        with open(rsp, "w", encoding="utf-8") as fh:
            fh.write("-target:library -nologo -nostdlib -nowarn:0649,0414,0169,0219,0168,0618 -langversion:9 "
                     f"-define:UNITY_EDITOR;UNITY_2021_3_OR_NEWER;UNITY_6000_0_OR_NEWER -unsafe -out:\"{dll}\"\n")
            fh.writelines(f"-r:\"{r}\"\n" for r in refs)
            fh.writelines(f"\"{s}\"\n" for s in srcs)
        r = subprocess.run([f"{UNITY}/Data/NetCoreRuntime/dotnet.exe", f"{UNITY}/Data/DotNetSdkRoslyn/csc.dll",
                            "-noconfig", f"@{rsp}"], capture_output=True, text=True, encoding="utf-8", errors="replace")
        errs = [l for l in r.stdout.splitlines() if ": error " in l]
        print(f"{name}: {len(srcs)} files, {len(errs)} errors")
        for l in errs[:40]:
            print("   ", l.replace(wt + "/", ""))
        if errs or r.returncode != 0:
            if not errs:
                print(r.stdout[-2000:], r.stderr[-2000:])
            worst = 1
        else:
            built[name] = dll
    return worst


if __name__ == "__main__":
    sys.exit(main())
