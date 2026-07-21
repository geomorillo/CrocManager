// CrocManager - Interfaz gráfica para croc
// Autor: Manuel Jhobanny Morillo Ordoñez
// © 2026 - Todos los derechos reservados

namespace CrocManager.Models;

public class HistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Type { get; set; } = "";        // "send" | "receive" | "text"
    public string Status { get; set; } = "";       // "completed" | "cancelled" | "error"
    public string Code { get; set; } = "";
    public string? Destination { get; set; }
    public List<string> Files { get; set; } = [];
    public string? TextContent { get; set; }
    public string? Error { get; set; }
    public long SizeBytes { get; set; }
}

public class HistoryStore
{
    public List<HistoryEntry> Entries { get; set; } = [];
}
