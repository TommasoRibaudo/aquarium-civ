using UnityEngine;
using UnityEngine.Rendering;

namespace AquariumCiv.View
{
    /// <summary>
    /// One peg doll in the scene: a Body renderer (body + head), two Arm transforms that can swing, and a prop slot.
    /// The parts are separate transforms so the activity view (#30) can bob, lean, swing an arm or carry a prop with
    /// plain transforms (life-in-the-box §9). Generated at runtime; nothing here is saved with the scene.
    /// </summary>
    public class PegDollView : MonoBehaviour
    {
        public Transform body, armL, armR, prop;
        /// <summary>Extra body part that goes with a prop (the chest strap of a back bundle); scaled like the body, not the prop.</summary>
        Transform propExtra;
        static readonly Mesh[] strapMeshes = new Mesh[2];
        /// <summary>How far hand tools lean out from the body, so they break the doll's outline (spec: 20-25 degrees).</summary>
        public const float ToolTilt = 22f;
        public PropKind propKind;
        public PropSlot propSlot;
        public float scale = 1f;
        public PegDoll.Look look;

        Material material;
        /// <summary>The hand prop's turn in the fist as built (upright), and whether it's now turned out along the arm.</summary>
        Quaternion propRest = Quaternion.identity;
        /// <summary>#192: the baby this doll carries: a part of the doll, not a walker, shown in the arms or on the back.</summary>
        Transform baby, babyStraps;
        bool babyOnBack;
        float babyLift; // #370: 0 in the arms, 1 held up over the head
        bool extended;
        /// <summary>#361: the basket rides in the left hand (the right hand picks): see <see cref="HoldInLeft"/>.</summary>
        bool inLeft;
        /// <summary>#361: the basket is in the left hand now (upright whatever the arm does).</summary>
        public bool BasketIsInLeft => inLeft;
        static readonly Quaternion AlongArm = Quaternion.Euler(180f, 0f, 0f) * Quaternion.Euler(0f, 180f, 0f);

        public static PegDollView Create(Transform parent, Material material, PegDoll.Look look, bool kid, int seed)
        {
            float s = kid ? PegDoll.KidScale : 1f;
            var go = new GameObject(kid ? "Kid" : "Villager") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<PegDollView>();
            v.material = material; v.look = look; v.scale = s;

            var bodyMb = new MeshBuilder(seed * 100 + 1) { jitter = 0.02f };
            PegDoll.Body(bodyMb, look, s);
            v.body = Part(go.transform, "Body", bodyMb.ToMesh("DollBody"), material, Vector3.zero, Quaternion.identity);

            var armMb = new MeshBuilder(seed * 100 + 2) { jitter = 0.02f };
            PegDoll.Arm(armMb, look, s);
            var armMesh = armMb.ToMesh("DollArm");
            float sy = PegDoll.BodyH * 0.92f * s, sx = PegDoll.ShoulderX * s;
            v.armL = Part(go.transform, "ArmL", armMesh, material, new Vector3(-sx, sy, 0), Quaternion.Euler(0, 0, -14f));
            v.armR = Part(go.transform, "ArmR", armMesh, material, new Vector3(sx, sy, 0), Quaternion.Euler(0, 0, 14f));
            return v;
        }

        static Transform Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            return go.transform;
        }

        static readonly Mesh[] babyStrapMeshes = new Mesh[2];

        static Mesh BabyStrapMesh(bool kid)
        {
            int i = kid ? 1 : 0;
            if (babyStrapMeshes[i] != null) return babyStrapMeshes[i];
            var mb = new MeshBuilder(192 + i) { jitter = 0.01f };
            PegDoll.BabyStraps(mb, kid ? PegDoll.KidScale : 1f);
            var m = mb.ToMesh("DollBabyStraps");
            m.hideFlags = HideFlags.DontSave;
            return babyStrapMeshes[i] = m;
        }

        static Mesh StrapMesh(bool kid)
        {
            int i = kid ? 1 : 0;
            if (strapMeshes[i] != null) return strapMeshes[i];
            var mb = new MeshBuilder(77 + i) { jitter = 0.02f };
            PegDoll.ChestStrap(mb, kid ? PegDoll.KidScale : 1f);
            var m = mb.ToMesh("DollStrap");
            m.hideFlags = HideFlags.DontSave;
            return strapMeshes[i] = m;
        }

        /// <summary>Puts a prop in a slot (or clears it with <see cref="PropKind.None"/>). Hand props ride on the right arm,
        /// so they swing with it; the arm is raised a little to hold the prop out.</summary>
        public void SetProp(PropKind kind, PropSlot slot)
        {
            propKind = kind; propSlot = slot; inLeft = false;
            if (prop != null) { if (Application.isPlaying) Destroy(prop.gameObject); else DestroyImmediate(prop.gameObject); prop = null; }
            if (propExtra != null) { if (Application.isPlaying) Destroy(propExtra.gameObject); else DestroyImmediate(propExtra.gameObject); propExtra = null; }
            armR.localRotation = Quaternion.Euler(0, 0, 14f);
            if (kind == PropKind.None) return;
            var (pos, rot) = PegDoll.SlotPose(slot);
            var parent = slot == PropSlot.Hand ? armR : transform;
            if (slot == PropSlot.Hand) armR.localRotation = Quaternion.Euler(-35f, 0, 10f);
            prop = Part(parent, "Prop_" + kind, PegDoll.PropMesh(kind), material, pos * scale, rot);
            prop.localScale = Vector3.one * Mathf.Lerp(1f, scale, 0.5f);
            if (slot == PropSlot.Hand)
            {
                // Upright in the raised hand, then leaned out from the body for tools (they break the outline).
                bool tool = kind is PropKind.Axe or PropKind.Hoe or PropKind.Spear or PropKind.Fish;
                prop.localRotation = Quaternion.Inverse(armR.localRotation) * Quaternion.Euler(0, 0, tool ? -ToolTilt : 0f) * rot;
            }
            propRest = prop.localRotation;
            extended = false;
            if (kind == PropKind.Bundle && slot == PropSlot.Back) propExtra = Part(transform, "ChestStrap", StrapMesh(scale < 1f), material, Vector3.zero, Quaternion.identity);
            // Something on the head or shoulder is held there by a hand (a mask is worn, not held).
            if (slot == PropSlot.Head && kind != PropKind.Mask) armR.localRotation = Quaternion.Euler(0, 0, 160f);
        }

        /// <summary>#192: a small baby on this doll, or none. Cheap to call every frame.</summary>
        public void SetBaby(bool on, bool onBack)
        {
            if (!on)
            {
                if (baby != null && baby.gameObject.activeSelf) baby.gameObject.SetActive(false);
                if (babyLift != 0f) LiftBaby(0f); // #370
                if (babyStraps != null && babyStraps.gameObject.activeSelf) babyStraps.gameObject.SetActive(false);
                return;
            }
            if (baby == null) { baby = Part(transform, "Baby", PegDoll.BabyMesh(), material, Vector3.zero, Quaternion.identity); babyOnBack = !onBack; }
            if (!baby.gameObject.activeSelf) baby.gameObject.SetActive(true);
            if (onBack && babyStraps == null) babyStraps = Part(transform, "BabyStraps", BabyStrapMesh(scale < 1f), material, Vector3.zero, Quaternion.identity);
            if (babyStraps != null && babyStraps.gameObject.activeSelf != onBack) babyStraps.gameObject.SetActive(onBack);
            if (babyOnBack == onBack) return;
            babyOnBack = onBack;
            babyLift = 0f; // #370: placed back in the arms below
            var (pos, _) = PegDoll.SlotPose(onBack ? PropSlot.Back : PropSlot.Arms);
            // Arms: held across the chest, the head at the crook. Back: upright in its wrap, looking back over the shoulder.
            var nudge = onBack ? new Vector3(0f, 0.03f, 0.022f) : new Vector3(0f, 0.008f, -0.018f); // the head over the shoulder; out in front of the chest
            baby.SetLocalPositionAndRotation((pos + nudge) * scale, onBack ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.Euler(0f, 0f, -68f));
            baby.localScale = Vector3.one * Mathf.Lerp(1f, scale, 0.5f);
        }

        /// <summary>#370: the newborn held up to the glass: 0 in the arms (as <see cref="SetBaby"/> holds it), 1 high over the head,
        /// upright and facing out (the arms at x 165). Cheap to call every frame.</summary>
        public void LiftBaby(float k)
        {
            k = Mathf.Max(0f, k);
            if (baby == null || babyOnBack || Mathf.Abs(k - babyLift) < 1e-4f) return;
            babyLift = k;
            var (pos, _) = PegDoll.SlotPose(PropSlot.Arms);
            var held = (pos + new Vector3(0f, 0.008f, -0.018f)) * scale;
            var high = new Vector3(0f, PegDoll.HeadY + PegDoll.HeadR + 0.04f, -0.03f) * scale;
            baby.SetLocalPositionAndRotation(Vector3.LerpUnclamped(held, high, k), Quaternion.SlerpUnclamped(Quaternion.Euler(0f, 0f, -68f), Quaternion.identity, Mathf.Min(1f, k)));
        }

        /// <summary>Turns a hand tool out along the arm (a strike: the axe head ends up in front, the rod out over the water)
        /// or back upright in the fist (carried).</summary>
        public void SetGrip(bool extend)
        {
            if (extend == extended || prop == null || propSlot != PropSlot.Hand) return;
            extended = extend;
            prop.localRotation = extend ? AlongArm : propRest;
        }

        /// <summary>#361: a hand basket in the left hand while the right hand picks or digs (it stays upright whatever the arm does), else back in the right.
        /// Cheap to call every frame.</summary>
        public void HoldInLeft(bool on)
        {
            on &= prop != null && propSlot == PropSlot.Hand && propKind == PropKind.Basket;
            if (on == inLeft) return;
            inLeft = on;
            var (pos, rot) = PegDoll.SlotPose(PropSlot.Hand);
            if (on) { prop.SetParent(armL, false); prop.localPosition = new Vector3(-pos.x, pos.y, pos.z) * scale; prop.localRotation = Quaternion.Inverse(armL.localRotation) * rot; }
            else { prop.SetParent(armR, false); prop.localPosition = pos * scale; prop.localRotation = propRest; }
        }

        /// <summary>The arms (and the hand tool's grip) for one frame of a <see cref="DollPose"/>. The whole-doll part of the
        /// pose (lean, squash, offset) goes on this transform; <see cref="PeopleView"/> sets it with the position.</summary>
        public void ApplyArms(in DollPose pose)
        {
            armL.localRotation = Quaternion.Euler(pose.armL);
            armR.localRotation = Quaternion.Euler(pose.armR);
            SetGrip(pose.extend);
            if (inLeft && prop != null) prop.localRotation = Quaternion.Inverse(armL.localRotation); // #361: the basket stays upright
        }
    }
}
