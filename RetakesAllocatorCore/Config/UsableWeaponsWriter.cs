using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API.Modules.Entities.Constants;

namespace RetakesAllocatorCore.Config;

/// <summary>
/// Rewrites only the "UsableWeapons" array inside a config.json text and leaves every other byte
/// alone, so the // comments, ordering and custom values an admin has in the file survive an
/// in-game or console change. System.Text.Json cannot write comments, which is why the config is
/// never re-serialized as a whole (see Configs.Load).
/// </summary>
public static class UsableWeaponsWriter
{
    private const string PropertyName = "UsableWeapons";

    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    public static string Rewrite(string json, IEnumerable<CsItem> usableWeapons)
    {
        var hasBom = json.Length > 0 && json[0] == '﻿';
        if (hasBom)
        {
            json = json[1..];
        }

        var newline = json.Contains("\r\n") ? "\r\n" : "\n";
        var bytes = Encoding.UTF8.GetBytes(json);
        var weapons = WeaponCatalog.SortForConfig(usableWeapons);

        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Allow,
            AllowTrailingCommas = true,
        });

        long objectContentStart = -1;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.StartObject && reader.CurrentDepth == 0)
            {
                objectContentStart = reader.BytesConsumed;
                continue;
            }

            if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1 ||
                !reader.ValueTextEquals(PropertyName))
            {
                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    reader.Skip();
                }

                continue;
            }

            var indent = LineIndent(bytes, (int)reader.TokenStartIndex);

            // The value may be preceded by comments: "UsableWeapons": /* x */ [ ... ]
            do
            {
                if (!reader.Read())
                {
                    throw new FormatException($"'{PropertyName}' has no value.");
                }
            } while (reader.TokenType == JsonTokenType.Comment);

            var valueStart = (int)reader.TokenStartIndex;
            if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
            {
                reader.Skip();
            }

            var valueEnd = (int)reader.BytesConsumed;

            return Splice(json, bytes, valueStart, valueEnd, BuildArray(weapons, indent, newline), hasBom);
        }

        if (objectContentStart < 0)
        {
            throw new FormatException("config.json does not contain a JSON object.");
        }

        // Property not in the file: add it as the first entry of the root object.
        var insertion = $"{newline}  \"{PropertyName}\": {BuildArray(weapons, "  ", newline)},";
        return Splice(json, bytes, (int)objectContentStart, (int)objectContentStart, insertion, hasBom);
    }

    private static string Splice(string original, byte[] bytes, int start, int end, string replacement, bool bom)
    {
        // Offsets from Utf8JsonReader are byte offsets; convert back via the byte array so
        // non-ASCII text (the box-drawing comments in the template) does not shift them.
        var before = Encoding.UTF8.GetString(bytes, 0, start);
        var after = Encoding.UTF8.GetString(bytes, end, bytes.Length - end);
        var result = before + replacement + after;
        return bom ? '﻿' + result : result;
    }

    private static string LineIndent(byte[] bytes, int position)
    {
        var lineStart = position;
        while (lineStart > 0 && bytes[lineStart - 1] != (byte)'\n')
        {
            lineStart--;
        }

        var sb = new StringBuilder();
        for (var i = lineStart; i < position && (bytes[i] == (byte)' ' || bytes[i] == (byte)'\t'); i++)
        {
            sb.Append((char)bytes[i]);
        }

        return sb.ToString();
    }

    private static string BuildArray(IReadOnlyList<CsItem> weapons, string indent, string newline)
    {
        if (weapons.Count == 0)
        {
            return "[]";
        }

        var groups = new List<(string Label, List<CsItem> Items)>();
        foreach (var category in WeaponCatalog.Categories)
        {
            var items = weapons.Where(category.Weapons.Contains).ToList();
            if (items.Count > 0)
            {
                groups.Add((category.DisplayName, items));
            }
        }

        var other = weapons.Where(w => !WeaponCatalog.AllCatalogWeapons.Contains(w)).ToList();
        if (other.Count > 0)
        {
            groups.Add(("Other", other));
        }

        var inner = indent + "  ";
        var sb = new StringBuilder("[");
        for (var g = 0; g < groups.Count; g++)
        {
            var (label, items) = groups[g];
            sb.Append(newline).Append(inner).Append("// ").Append(label);
            sb.Append(newline).Append(inner);
            sb.Append(string.Join(", ", items.Select(w => $"\"{WeaponCatalog.GetConfigName(w)}\"")));
            if (g < groups.Count - 1)
            {
                sb.Append(',');
            }
        }

        sb.Append(newline).Append(indent).Append(']');
        return sb.ToString();
    }
}
