using UnityEngine;

namespace AquariumCiv.View
{
    /// <summary>
    /// #355: where a stroke lands, for <see cref="PeopleView.WorkHit"/>: the business end of the hand tool when it is swung out along
    /// the arm (the axe's head, the hoe's blade, the hammer's head, the spear's point), else the right hand. World space, read after
    /// the doll's transforms are set for the frame. View side (Transforms): not part of the pure beat kit.
    /// </summary>
    public static class WorkHitPoint
    {
        /// <summary>How far along a hand prop (its own up axis, from the grip) its working end is (PegDoll.PropMesh's sizes).</summary>
        public static float ToolReach(PropKind kind) => kind switch
        {
            PropKind.Axe => 0.115f,
            PropKind.Hoe => 0.2f,
            PropKind.Hammer => 0.1f,
            PropKind.Spear => 0.18f,
            PropKind.Rod => 0.2f,
            PropKind.Stick => 0.1f,
            _ => 0f,
        };

        public static Vector3 HitPoint(PegDollView v)
        {
            if (v == null) return Vector3.zero;
            if (v.prop != null && v.propSlot == PropSlot.Hand)
            {
                float reach = ToolReach(v.propKind);
                if (reach > 0f) return v.prop.TransformPoint(new Vector3(0f, reach, 0f));
            }
            return v.armR != null ? v.armR.TransformPoint(new Vector3(0f, -PegDoll.ArmLen * v.scale, 0f)) : v.transform.position;
        }
    }
}
