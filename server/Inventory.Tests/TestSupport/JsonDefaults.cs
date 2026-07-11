using System.Text.Json;
using System.Text.Json.Serialization;

namespace Inventory.Tests.TestSupport;

/// <summary>JSON options matching the API (string enums, camelCase, case-insensitive) for reading test responses.</summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
