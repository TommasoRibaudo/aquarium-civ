using AquariumCiv.Sim;
using UnityEngine;

namespace AquariumCiv.View.Demo
{
    /// <summary>
    /// TEMPORARY (#31): a hand-placed demo layout so the slice scene shows the ported kit before the sim places anything.
    /// Delete this component (and this file) when #33 drives <see cref="BoxView"/> from SimState. Nothing here is design:
    /// positions are picked by eye on the style kit's default land, one of each piece kind the slice needs (a camp round a
    /// fire, a couple of first houses, a well, a field, trails and a path, lanterns, people with props).
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("AquariumCiv/TEMPORARY Demo Layout (remove at #33)")]
    public class TemporaryDemoLayout : MonoBehaviour, IBoxLayoutSource
    {
        static float Face(Vector2 from, Vector2 to)
        {
            var d = to - from;
            return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + 180f; // local -z (the front) faces the target
        }

        public void FillLayout(BoxLayout L)
        {
            L.Clear();
            var fire = new Vector2(-0.35f, -0.25f);
            L.Clearing(fire, 0.62f, ClearingKind.Dirt);
            L.Add(PieceKind.Fire, fire.x, fire.y, 0f, 0.4f, 0.4f, Era.Camp, 1);

            // The camp: tents round the fire, a lean-to, a drying rack, a woodpile.
            var tents = new[] { new Vector2(-1.25f, -0.7f), new Vector2(0.4f, 0.55f), new Vector2(-0.4f, 0.75f) };
            for (int i = 0; i < tents.Length; i++)
                L.House(tents[i].x, tents[i].y, Face(tents[i], fire), 0.8f, 0.8f, Era.Camp, seed: 10 + i);
            var lean = new Vector2(-1.3f, 0.3f);
            L.Add(PieceKind.LeanTo, lean.x, lean.y, Face(lean, fire), 0.6f, 0.5f, Era.Camp, 20);
            L.Add(PieceKind.DryingRack, 0.55f, -0.75f, 25f, 0.35f, 0.1f, Era.Camp, 21);
            L.Add(PieceKind.Woodpile, -1.85f, -0.15f, 80f, 0.3f, 0.3f, Era.Camp, 22);

            // The first village bit, on the rise to the right: two houses, a well, a bench, lanterns.
            var well = new Vector2(1.35f, 0.25f);
            L.Add(PieceKind.Well, well.x, well.y, 0f, 0.3f, 0.3f, Era.Village, 30);
            var h1 = new Vector2(0.95f, 1.0f); var h2 = new Vector2(1.95f, 0.85f);
            L.House(h1.x, h1.y, Face(h1, well), 0.62f, 0.46f, Era.Village, RoofStyle.Thatch, chimney: false, seed: 31);
            L.House(h2.x, h2.y, Face(h2, well) + 8f, 0.6f, 0.44f, Era.Village, RoofStyle.Terracotta, chimney: true, seed: 32);
            L.Add(PieceKind.Bench, 1.7f, 0.05f, 70f, 0.25f, 0.1f, Era.Village, 33);
            L.Add(PieceKind.Lantern, 1.08f, 0.15f, 0f, 0.1f, 0.1f, Era.Village, 34);
            L.Add(PieceKind.Lantern, 0.45f, -0.05f, 30f, 0.1f, 0.1f, Era.Village, 35);

            // A fenced wheat field on the flat by the pond, gate towards the camp.
            L.Field(new[] { new Vector2(-0.35f, -1.95f), new Vector2(0.5f, -1.95f), new Vector2(0.6f, -1.2f), new Vector2(-0.3f, -1.15f) }, CropKind.Wheat, 4f, true, 2);

            L.Path(PathTier.Trail, fire, new Vector2(-0.05f, -0.75f), new Vector2(0.15f, -1.12f));
            L.Path(PathTier.Path, fire, new Vector2(0.5f, 0.05f), well, h1 + new Vector2(0.05f, -0.3f));
            L.Path(PathTier.Trail, well, h2 + new Vector2(-0.1f, -0.3f));
            L.Path(PathTier.Trail, new Vector2(-1.25f, -0.7f), fire);

            // Camp people round the fire (hide tones, bright headbands), facing it.
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + 0.9f;
                var p = fire + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.27f;
                L.Villager(p, Face(p, fire), Era.Camp, 100 + i, kid: i == 3);
            }
            L.Villager(new Vector2(-1.65f, -0.3f), 110f, Era.Camp, 106, prop: PropKind.Axe, slot: PropSlot.Hand);
            L.Villager(new Vector2(0.35f, -0.55f), 200f, Era.Camp, 107, prop: PropKind.Basket, slot: PropSlot.Hand);
            L.Villager(new Vector2(-0.8f, 0.35f), 150f, Era.Camp, 108, prop: PropKind.Bundle, slot: PropSlot.Back);

            // Village people: at the well, in the field, carrying.
            L.Villager(well + new Vector2(-0.2f, -0.1f), Face(well + new Vector2(-0.2f, -0.1f), well), Era.Village, 120, prop: PropKind.Pot, slot: PropSlot.Head);
            L.Villager(new Vector2(0.15f, -1.55f), 20f, Era.Village, 121, prop: PropKind.Hoe, slot: PropSlot.Hand);
            L.Villager(new Vector2(0.95f, 0.3f), 250f, Era.Village, 122, prop: PropKind.Sheaf, slot: PropSlot.Head, accent: 2);
            L.Villager(new Vector2(1.6f, 0.55f), 210f, Era.Village, 123, prop: PropKind.Log, slot: PropSlot.Shoulder);
            L.Villager(new Vector2(1.15f, 0.55f), 130f, Era.Village, 124, kid: true);
        }
    }
}
