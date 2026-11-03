namespace UnityEngine
{
    /// <summary>The bit of UnityEngine.Debug the pure tests use (ApartLifeTests logs a line): to the console, so dotnet builds them.</summary>
    public static class Debug
    {
        public static void Log(object message) => System.Console.WriteLine(message);
        public static void LogWarning(object message) => System.Console.WriteLine("warning: " + message);
    }
}
