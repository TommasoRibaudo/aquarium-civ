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
        bool extended;
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
            propKind = kind; propSlot = slot;
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
            // Something on the head or shoulder is held there by a hand.
            if (slot == PropSlot.Head) armR.localRotation = Quaternion.Euler(0, 0, 160f);
        }

        /// <summary>Turns a hand tool out along the arm (a strike: the axe head ends up in front, the rod out over the water)
        /// or back upright in the fist (carried).</summary>
        public void SetGrip(bool extend)
        {
            if (extend == extended || prop == null || propSlot != PropSlot.Hand) return;
            extended = extend;
            prop.localRotation = extend ? AlongArm : propRest;
        }

        /// <summary>The arms (and the hand tool's grip) for one frame of a <see cref="DollPose"/>. The whole-doll part of the
        /// pose (lean, squash, offset) goes on this transform; <see cref="PeopleView"/> sets it with the position.</summary>
        public void ApplyArms(in DollPose pose)
        {
            armL.localRotation = Quaternion.Euler(pose.armL);
            armR.localRotation = Quaternion.Euler(pose.armR);
            SetGrip(pose.extend);
        }
    }
}
