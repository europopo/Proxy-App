using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ProxyApp;

public class PacEntry
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public static class PacHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProxyApp");

    private static readonly string HistoryFilePath = Path.Combine(DataDirectory, "pac-history.json");

    private static readonly IReadOnlyList<PacEntry> DefaultEntries = new List<PacEntry>
    {
        new() { Name = "szh_w", Url = "http://rbins-ap.bosch.com/szh_w.pac" },
        new() { Name = "mi", Url = "http://rbins-ap.bosch.com/mi.pac" },
        new() { Name = "sgp", Url = "http://rbins-ap.bosch.com/sgp.pac" }
    };

    public static IReadOnlyList<PacEntry> Load()
    {
        if (!File.Exists(HistoryFilePath))
        {
            List<PacEntry> defaults = Normalize(DefaultEntries).ToList();
            SaveAll(defaults);
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(HistoryFilePath);
            List<PacEntry>? entries = JsonSerializer.Deserialize<List<PacEntry>>(json);

            if (entries is not null && entries.Count > 0)
            {
                return Normalize(entries);
            }

            // 兼容旧格式：["http://xx.pac", "http://yy.pac"]
            List<string>? legacyUrls = JsonSerializer.Deserialize<List<string>>(json);
            if (legacyUrls is null) return Array.Empty<PacEntry>();

            return Normalize(legacyUrls.Select(url => new PacEntry
            {
                Name = url?.Trim() ?? string.Empty,
                Url = url?.Trim() ?? string.Empty
            }));
        }
        catch
        {
            return Array.Empty<PacEntry>();
        }
    }

    public static void SaveOrUpdate(string name, string url)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) return;

        List<PacEntry> entries = Load().ToList();
        Upsert(entries, name.Trim(), url.Trim());
        SaveAll(entries);
    }

    public static void SaveOrUpdateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        string normalizedUrl = url.Trim();
        SaveOrUpdate(normalizedUrl, normalizedUrl);
    }

    public static void ReplaceAll(IEnumerable<PacEntry> entries)
    {
        List<PacEntry> normalized = Normalize(entries).Take(50).ToList();
        SaveAll(normalized);
    }

    private static void Upsert(List<PacEntry> entries, string name, string url)
    {
        entries.RemoveAll(x => string.Equals(x.Url, url, StringComparison.OrdinalIgnoreCase));
        entries.Insert(0, new PacEntry { Name = name, Url = url });

        if (entries.Count > 50)
        {
            entries.RemoveRange(50, entries.Count - 50);
        }
    }

    private static List<PacEntry> Normalize(IEnumerable<PacEntry> entries)
    {
        return entries
            .Where(x => !string.IsNullOrWhiteSpace(x.Url))
            .Select(x => new PacEntry
            {
                Name = string.IsNullOrWhiteSpace(x.Name) ? x.Url.Trim() : x.Name.Trim(),
                Url = x.Url.Trim()
            })
            .GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static void SaveAll(List<PacEntry> entries)
    {
        Directory.CreateDirectory(DataDirectory);
        string json = JsonSerializer.Serialize(entries, JsonOptions);
        File.WriteAllText(HistoryFilePath, json);
    }
}
