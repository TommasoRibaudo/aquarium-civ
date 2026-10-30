using System;
using UnityEngine;

namespace AquariumCiv.Game
{
    /// <summary>
    /// The one seam between the UI Toolkit HUD and the box camera (which reads the touchscreen directly): the HUD sets
    /// <see cref="IsOverUi"/>, the camera ignores a press that started on it, so tapping a button never orbits the box.
    /// </summary>
    public static class UiInput
    {
        /// <summary>Screen position (pixels, y up) → is there UI under it. Null while no HUD is up.</summary>
        public static Func<Vector2, bool> IsOverUi;

        public static bool Over(Vector2 screenPos) => IsOverUi != null && IsOverUi(screenPos);
    }
}
