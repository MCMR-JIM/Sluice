using System.Text.Json;

namespace StoreDownloader.Json;

/// <summary>Small helpers that keep defensive JSON traversal readable.</summary>
public static class JsonElementExtensions
{
    /// <summary>Reads a nested string property, returning <c>null</c> when any hop is missing.</summary>
    /// <param name="element">Root element.</param>
    /// <param name="path">Property names to walk, in order.</param>
    public static string? GetString(this JsonElement element, params string[] path)
    {
        var current = Walk(element, path);
        if (current is null)
        {
            return null;
        }

        var value = current.GetValueOrDefault();
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>Reads a nested array of strings, returning an empty list when absent.</summary>
    /// <param name="element">Root element.</param>
    /// <param name="path">Property names to walk, in order.</param>
    public static IReadOnlyList<string> GetStringArray(this JsonElement element, params string[] path)
    {
        var current = Walk(element, path);
        if (current is null || current.GetValueOrDefault().ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return current.GetValueOrDefault().EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToList();
    }

    /// <summary>Returns the nested element at <paramref name="path"/>, or <c>null</c>.</summary>
    /// <param name="element">Root element.</param>
    /// <param name="path">Property names to walk, in order.</param>
    public static JsonElement? GetElement(this JsonElement element, params string[] path) => Walk(element, path);

    /// <summary>Returns the nested array as a list of elements, or an empty list.</summary>
    /// <param name="element">Root element.</param>
    /// <param name="path">Property names to walk, in order.</param>
    public static IReadOnlyList<JsonElement> GetArray(this JsonElement element, params string[] path)
    {
        var current = Walk(element, path);
        if (current is null || current.GetValueOrDefault().ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return current.GetValueOrDefault().EnumerateArray().ToList();
    }

    /// <summary>Looks up a property using a case-insensitive match.</summary>
    /// <param name="element">Element to inspect.</param>
    /// <param name="name">Property name.</param>
    /// <param name="value">The property value when found.</param>
    public static bool TryGetPropertyIgnoreCase(this JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static JsonElement? Walk(JsonElement element, string[] path)
    {
        var current = element;
        foreach (var name in path)
        {
            if (!current.TryGetPropertyIgnoreCase(name, out var next))
            {
                return null;
            }

            current = next;
        }

        return current;
    }
}
