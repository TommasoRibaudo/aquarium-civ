// Stand-in for UnityEngine.JsonUtility so the sim tests compile outside Unity. The tests only compare two
// serialisations of the same type for equality, so any stable field-based serialiser works.
using System.Text.Json;

namespace UnityEngine
{
    public static class JsonUtility
    {
        static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };

        public static string ToJson(object obj) => JsonSerializer.Serialize(obj, obj.GetType(), Options);
        public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
    }
}
