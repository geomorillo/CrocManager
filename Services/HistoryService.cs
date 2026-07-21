// CrocManager - Interfaz gráfica para croc
// Autor: Manuel Jhobanny Morillo Ordoñez
// © 2026 - Todos los derechos reservados

using System.Text.Json;
using CrocManager.Models;

namespace CrocManager.Services;

public class HistoryService
{
    private static readonly string HistoryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".crocmanager");

    private static readonly string HistoryFile = Path.Combine(HistoryDir, "history.json");
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private readonly object _lock = new();

    public HistoryService()
    {
        if (!Directory.Exists(HistoryDir))
            Directory.CreateDirectory(HistoryDir);
    }

    public void Add(HistoryEntry entry)
    {
        lock (_lock)
        {
            var store = Load();
            store.Entries.Insert(0, entry);
            Save(store);
        }
    }

    public List<HistoryEntry> GetAll()
    {
        lock (_lock)
        {
            return Load().Entries;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            Save(new HistoryStore());
        }
    }

    private HistoryStore Load()
    {
        if (!File.Exists(HistoryFile))
            return new HistoryStore();
        try
        {
            var json = File.ReadAllText(HistoryFile);
            return JsonSerializer.Deserialize<HistoryStore>(json, JsonOpts) ?? new HistoryStore();
        }
        catch
        {
            return new HistoryStore();
        }
    }

    private void Save(HistoryStore store)
    {
        var json = JsonSerializer.Serialize(store, JsonOpts);
        File.WriteAllText(HistoryFile, json);
    }
}
