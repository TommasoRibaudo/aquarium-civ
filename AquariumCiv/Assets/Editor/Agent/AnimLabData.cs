// #353 AnimLab: the data the Editor reads. Tools/animlab/prep.py writes lab.json per clip from AnimBench's track (the same clip
// JSON, the same DollMotion/beat-kit code, the same frame times), flat enough for JsonUtility. The batch file is the queue's state
// on disk, so a render resumes after a domain reload or a timeout.
using System;
using System.IO;
using UnityEngine;

namespace AquariumCiv.EditorTools
{
    [Serializable]
    public sealed class LabCam
    {
        public float fx, fz, span = 1.5f, yaw = 28f, pitch = 34f;
    }

    [Serializable]
    public sealed class LabDoll
    {
        public const int Stride = 26;
        // per frame: x z bodyYaw yaw | offset xyz | lean roll squash | armL xyz | armR xyz | extend hit hidden lying | tip xyz | handR xyz
        public string id;
        public bool kid, camp;
        public int seed;
        public float scale = 1f;
        public float[] f;
        /// <summary>Per frame: "Kind/Slot" (e.g. "Spear/Hand", "None/Hand").</summary>
        public string[] prop;
    }

    [Serializable]
    public sealed class LabObject
    {
        public const int Stride = 7;
        // per frame: pos xyz | yaw | visible | phase | berries
        public string id, kind;
        public float radius, sx, sz;
        public float[] f;
        public string[] act;
    }

    [Serializable]
    public sealed class LabClip
    {
        public string format, clip, issue, branch, commit, request;
        public float fps = 30f, clock;
        public int frames;
        public LabCam cam, close;
        public LabDoll[] dolls;
        public LabObject[] objects;

        public static LabClip Load(string path) => JsonUtility.FromJson<LabClip>(File.ReadAllText(path));
    }

    [Serializable]
    public sealed class LabBatch
    {
        public string id;
        /// <summary>lab.json per cell, and the clip's out dir (frames go to out/frames/).</summary>
        public string[] labs, outs;
        public int next, total;
        public int dw = 480, dh = 360, cw = 360, ch = 360;
        public string prevScene;
        public float renderSeconds;
        public int rendered, calls;
        public bool done;

        public static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "agent", "animlab"));
        public static string StatePath => Path.Combine(Root, "batch.json");

        public static LabBatch Load() => File.Exists(StatePath) ? JsonUtility.FromJson<LabBatch>(File.ReadAllText(StatePath)) : null;

        public void Save()
        {
            var tmp = StatePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(this, true));
            if (File.Exists(StatePath)) File.Delete(StatePath);
            File.Move(tmp, StatePath);
        }
    }
}
