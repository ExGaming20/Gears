using System;
using System.Collections.Generic;
using System.Text.Json;
using Gears.Utilities;

public static class JsonParser
{
    public static string? FindData(string json, string keyName)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(keyName))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            string[] keys = keyName.Split('.');

            JsonElement current = doc.RootElement;

            foreach (string key in keys)
            {
                if (current.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                if (!current.TryGetProperty(key, out JsonElement next))
                {
                    return null;
                }

                current = next;
            }

            return current.ValueKind switch
            {
                JsonValueKind.String => current.GetString(),
                JsonValueKind.Number => current.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => current.GetRawText()
            };
        }
        catch (JsonException ex)
        {
            Logger.Instance.LogError($"Invalid JSON: {ex.Message}, {nameof(json)}, {ex}");

            return null;
        }
    }

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string?> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    string key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    Flatten(property.Value, key, result);
                }
                break;

            case JsonValueKind.String:
                result[prefix] = element.GetString();
                break;

            case JsonValueKind.Number:
                result[prefix] = element.GetRawText();
                break;

            case JsonValueKind.True:
                result[prefix] = "true";
                break;

            case JsonValueKind.False:
                result[prefix] = "false";
                break;

            case JsonValueKind.Null:
                result[prefix] = null;
                break;

            default:
                result[prefix] = element.GetRawText();
                break;
        }
    }
}