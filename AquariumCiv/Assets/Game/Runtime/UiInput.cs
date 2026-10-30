using System;
using System.Collections.Generic;
using UnityEngine;

namespace AquariumCiv.Game
{
    /// <summary>
    /// The one seam between the UI Toolkit HUD and the box camera (which reads the touchscreen directly): each UI layer
    /// (HUD shell, feedback sheet, ...) adds a hit test, the camera ignores a press that started on any of them, so
    /// tapping a button never orbits the box.
    /// </summary>
    public static class UiInput
    {
        static readonly List<Func<Vector2, bool>> Layers = new();

        /// <summary>Add a hit test: screen position (pixels, y up) → is this layer's UI under it.</summary>
        public static void Add(Func<Vector2, bool> isOver) { if (isOver != null && !Layers.Contains(isOver)) Layers.Add(isOver); }

        public static void Remove(Func<Vector2, bool> isOver) => Layers.Remove(isOver);

        public static bool Over(Vector2 screenPos)
        {
            for (int i = 0; i < Layers.Count; i++)
                if (Layers[i](screenPos)) return true;
            return false;
        }
    }
}
