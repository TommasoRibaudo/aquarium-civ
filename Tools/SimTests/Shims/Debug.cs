namespace UnityEngine
{
    /// <summary>Just enough of UnityEngine.Debug for Life tests that log (they compile outside Unity here).</summary>
    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }
}
