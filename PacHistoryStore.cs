using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ProxyApp;

public static class PacHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProxyApp");

    private static readonly string HistoryFilePath = Path.Combine(DataDirectory, "pac-history.json");

    public static IReadOnlyList<string> Load()
    {
        if (!File.Exists(HistoryFilePath)) return Array.Empty<string>();

        try
        {
            string json = File.ReadAllText(HistoryFilePath);
            List<string>? urls = JsonSerializer.Deserialize<List<string>>(json);
            return urls?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static void SaveOrUpdate(string pacUrl)
    {
        if (string.IsNullOrWhiteSpace(pacUrl)) return;

        List<string> urls = Load().ToList();
        Upsert(urls, pacUrl.Trim());
        SaveAll(urls);
    }

    public static void ReplaceAll(IEnumerable<string> pacUrls)
    {
        List<string> urls = pacUrls
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

        SaveAll(urls);
    }

    private static void Upsert(List<string> urls, string pacUrl)
    {
        urls.RemoveAll(x => string.Equals(x, pacUrl, StringComparison.OrdinalIgnoreCase));
        urls.Insert(0, pacUrl);

        if (urls.Count > 50)
        {
            urls.RemoveRange(50, urls.Count - 50);
        }
    }

    private static void SaveAll(List<string> urls)
    {
        Directory.CreateDirectory(DataDirectory);
        string json = JsonSerializer.Serialize(urls, JsonOptions);
        File.WriteAllText(HistoryFilePath, json);
    }
}
