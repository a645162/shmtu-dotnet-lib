using System.Text.Encodings.Web;
using System.Text.Json;

namespace shmtu.utils;

public static class JsonUtils
{
    public static readonly JsonSerializerOptions
        ProgramJsonSerializerOptions =
            new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };
}