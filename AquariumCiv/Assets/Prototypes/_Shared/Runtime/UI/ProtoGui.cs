using UnityEngine;

namespace AquariumCiv.Prototypes
{
    /// <summary>Shared IMGUI scaling for throwaway prototype UI: layouts are authored on a 720x1280 portrait canvas.</summary>
    public static class ProtoGui
    {
        public static float Scale => Mathf.Min(Screen.width / 720f, Screen.height / 1280f);
        public static float Width => Screen.width / Scale;
        public static float Height => Screen.height / Scale;

        static GUIStyle box, wrap;
        public static GUIStyle Box => box;
        public static GUIStyle Wrap => wrap;

        public static void Begin()
        {
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1));
            var s = GUI.skin;
            s.label.fontSize = s.button.fontSize = s.textField.fontSize = s.textArea.fontSize = 22;
            s.box.fontSize = 22;
            s.label.richText = true;
            box ??= new GUIStyle(s.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(14, 14, 12, 12), wordWrap = true };
            box.fontSize = 22;
            wrap ??= new GUIStyle(s.label) { wordWrap = true };
            wrap.fontSize = 22;
        }

        /// <summary>Register a canvas-space rect so world input under it is ignored.</summary>
        public static void Block(Rect canvasRect)
        {
            float k = Scale;
            GuiBlocker.Add(new Rect(canvasRect.x * k, canvasRect.y * k, canvasRect.width * k, canvasRect.height * k));
        }

        public static Vector2 WorldToCanvas(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return new Vector2(-999, -999);
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0) return new Vector2(-999, -999);
            return new Vector2(sp.x / Scale, (Screen.height - sp.y) / Scale);
        }
    }
}
