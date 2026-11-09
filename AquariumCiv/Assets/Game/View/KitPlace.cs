using AquariumCiv.Game;
using AquariumCiv.GameUI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AquariumCiv.View
{
    /// <summary>
    /// #518 one finger's press for a kit tool on the land (playtest #513: a two-finger pan with the seed in hand set one down).
    /// A press is a tap when it lifts near where it landed and no second finger ever joined it: a press that becomes a pan or a
    /// pinch stays spoiled until every finger is up, so the finger left behind when the other lifts never acts either. A fresh
    /// one-finger tap still answers on the frame it lifts (no throttle). Tools that act while the finger is held (the mister,
    /// the tweezers, the cloth) wait <see cref="SecondFingerGrace"/> first, or act on the lift if the tap is quicker.
    /// </summary>
    public sealed class KitPress
    {
        /// <summary>How long a held tool waits for a second finger before it acts (seconds). Two fingers of one gesture rarely land
        /// further apart than this; a quicker tap acts on its lift instead, so no tap is lost.</summary>
        public const float SecondFingerGrace = 0.1f;

        bool pressing, valid, multi;
        Vector2 downAt, lastAt;
        float downT;

        /// <summary>A press is under way (any number of fingers).</summary>
        public bool Pressing => pressing;
        /// <summary>A second finger joined this press: it can't be a tap.</summary>
        public bool Multi => multi;
        public Vector2 DownAt => downAt;
        public Vector2 LastAt => lastAt;

        /// <summary>Feeds this frame's fingers (<paramref name="n"/>, the first at <paramref name="pos"/>). True on the frame a clean
        /// one-finger tap lifts, with where it lifted. A press that starts on the UI is the UI's.</summary>
        public bool Tap(int n, Vector2 pos, float slopShare, out Vector2 at)
        {
            at = default;
            if (n > 0)
            {
                if (!pressing) { pressing = true; valid = !UiInput.Over(pos); multi = false; downAt = pos; downT = Time.unscaledTime; }
                if (n > 1) multi = true;
                lastAt = pos;
                return false;
            }
            if (!pressing) return false;
            pressing = false;
            if (!valid || multi) return false; // two fingers: the camera's
            if ((lastAt - downAt).magnitude > slopShare * 2f * Mathf.Max(1, Screen.height)) return false; // a drag isn't a tap
            at = lastAt;
            return true;
        }

        public void Reset() { pressing = false; multi = false; }

        /// <summary>The fingers down now (the first one's position), or the mouse (left = one finger, right/middle = two).</summary>
        public static int ReadPointer(out Vector2 a)
        {
            a = default;
            int n = 0;
            var ts = Touchscreen.current;
            if (ts != null)
            {
                foreach (var t in ts.touches)
                {
                    if (!t.isInProgress) continue;
                    if (n == 0) a = t.position.ReadValue();
                    n++;
                }
                if (n > 0) return n;
            }
            var m = Mouse.current;
            if (m == null) return 0;
            a = m.position.ReadValue();
            return m.leftButton.isPressed ? 1 : (m.rightButton.isPressed || m.middleButton.isPressed) ? 2 : 0;
        }
    }

    /// <summary>
    /// #518 the "place with a ghost" flow the life slot (#423) and the seed share: the first tap on good ground shows a ghost there and
    /// a hint that stays while it waits; a tap on the ghost sets it down; a tap elsewhere on good ground slides the ghost there. Holds
    /// where the ghost is and how it moves (pop in, breathe, slide, wobble on a "no"); each tool draws its own look
    /// (<see cref="GhostLook"/>) and decides what good ground is.
    /// </summary>
    public sealed class GhostTap
    {
        /// <summary>How long the tap-again hint may stay up while the ghost waits (it goes with the ghost before that).</summary>
        public const float HintSeconds = 30f;

        /// <summary>A tap this near the ghost's middle (box units) sets it down.</summary>
        public float reach;
        /// <summary>The hint while the ghost waits for its second tap.</summary>
        public readonly string hint;

        public GhostTap(string hint, float reach) { this.hint = hint; this.reach = reach; }

        public bool Shown => Cell >= 0;
        public int Cell { get; private set; } = -1;
        /// <summary>The ghost's middle (box-local; y unused).</summary>
        public Vector3 At { get; private set; }
        float shownT, slideT = -9f, wobbleT = -9f;
        Vector3 slideFrom;

        /// <summary>The tap at a land point (box-local) is on the ghost.</summary>
        public bool OnGhost(Vector3 at) => Shown && new Vector2(at.x - At.x, at.z - At.z).magnitude <= reach;

        /// <summary>Shows the ghost on a cell (sliding there if it was up) and puts its hint up, taking <paramref name="withdraw"/> (the
        /// tool's own hint) down. True if it moved.</summary>
        public bool Show(int cell, Vector3 target, GameHud hud, string withdraw)
        {
            float now = Time.unscaledTime;
            bool moved = Shown;
            slideFrom = moved ? At : target; slideT = moved ? now : -9f;
            if (!moved) shownT = now;
            At = target; Cell = cell;
            // tester 2026-11-08: the hint stays while the ghost waits (a 3 s toast ran out during the camera's lean, so nobody saw it)
            if (withdraw != null) hud?.WithdrawToast(withdraw);
            hud?.Toast(hint, HintSeconds);
            return moved;
        }

        /// <summary>The ghost goes, and its hint with it.</summary>
        public void Hide(GameHud hud)
        {
            if (Cell >= 0) hud?.WithdrawToast(hint);
            Cell = -1;
        }

        /// <summary>The ghost shakes its head (a tap on ground it can't have).</summary>
        public void Wobble() => wobbleT = Time.unscaledTime;

        /// <summary>Where to draw the ghost now: its slide and its wobble.</summary>
        public Vector3 Centre()
        {
            float now = Time.unscaledTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - slideT) / 0.2f));
            var c = Vector3.Lerp(slideFrom, At, k);
            float wob = now - wobbleT < 0.3f ? Mathf.Sin((now - wobbleT) * 50f) * 0.025f * (1f - (now - wobbleT) / 0.3f) : 0f;
            c.x += wob;
            return c;
        }

        /// <summary>The breath, 1 -> 1.04 over 1.2 s.</summary>
        public float Breath() => 1f + 0.02f * (1f - Mathf.Cos((Time.unscaledTime - shownT) * Mathf.PI * 2f / 1.2f));
        /// <summary>The pop-in, 0 -> 1 over 0.15 s.</summary>
        public float Pop() => Mathf.Clamp01((Time.unscaledTime - shownT) / 0.15f);
    }

    /// <summary>
    /// #518 a ghost's look (B12.0): pale silhouettes over a thin darker edge, side-on to the camera, on the land. The life slot draws its
    /// founding group with it and the seed its mound; #516 restyles it here for both.
    /// </summary>
    public sealed class GhostLook
    {
        readonly Transform parent;
        readonly string name;
        GameObject fillGo, edgeGo;
        Material fillMat, edgeMat;

        public GhostLook(Transform parent, string name) { this.parent = parent; this.name = name; }

        public bool Visible => fillGo != null && fillGo.activeSelf;

        /// <summary>Draws the silhouettes (<paramref name="fill"/> in front, <paramref name="edge"/> a little larger behind) at a box-local
        /// point, turned side-on to the camera, scaled.</summary>
        public void Draw(Mesh fill, Mesh edge, Vector3 local, float scale, Color fillColor, Color edgeColor)
        {
            if (fillGo == null)
            {
                fillGo = Child(name + "Fill"); edgeGo = Child(name + "Edge");
                fillMat = DimMaterial(3005); edgeMat = DimMaterial(3004);
                fillGo.GetComponent<MeshRenderer>().sharedMaterial = fillMat;
                edgeGo.GetComponent<MeshRenderer>().sharedMaterial = edgeMat;
            }
            fillMat.SetColor("_Color", fillColor);
            edgeMat.SetColor("_Color", edgeColor);
            fillGo.GetComponent<MeshFilter>().sharedMesh = fill;
            edgeGo.GetComponent<MeshFilter>().sharedMesh = edge;
            // side-on to the camera: the shape's +x runs across the screen
            float yaw = 0f;
            var cam = Camera.main;
            if (cam != null)
            {
                var f = parent.InverseTransformDirection(cam.transform.forward);
                yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            }
            foreach (var go in new[] { fillGo, edgeGo })
            {
                go.SetActive(true);
                go.transform.localPosition = local;
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);
            }
        }

        public void Hide()
        {
            if (fillGo != null && fillGo.activeSelf) fillGo.SetActive(false);
            if (edgeGo != null && edgeGo.activeSelf) edgeGo.SetActive(false);
        }

        public void Destroy()
        {
            foreach (var go in new[] { fillGo, edgeGo }) if (go != null) Object.Destroy(go);
            foreach (var m in new[] { fillMat, edgeMat }) if (m != null) Object.Destroy(m);
            fillGo = edgeGo = null; fillMat = edgeMat = null;
        }

        GameObject Child(string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>A copy of the soft see-through material the life tool's dim, glow restore and ghosts draw with.</summary>
        public static Material DimMaterial(int queue)
        {
            var res = Resources.Load<Material>("LifeDim");
            var m = res != null ? new Material(res) : new Material(Shader.Find("AquariumCiv/View/LifeDim"));
            m.hideFlags = HideFlags.DontSave;
            m.renderQueue = queue;
            return m;
        }
    }
}
