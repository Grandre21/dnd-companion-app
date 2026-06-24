using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface IInventoryRepository
{
    Task<List<InventoryItem>> GetInventoryForCharacterAsync(string characterId);
    Task<InventoryItem?> CreateInventoryItemAsync(InventoryItem item);
    Task<InventoryItem?> UpdateInventoryItemAsync(InventoryItem item);
    Task DeleteInventoryItemAsync(string id);
}

/// <summary>Accesso dati per l'inventario dei personaggi (tabella <c>inventory_items</c>).</summary>
public class InventoryRepository : IInventoryRepository
{
    private readonly SupabaseService _supabase;

    public InventoryRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<List<InventoryItem>> GetInventoryForCharacterAsync(string characterId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<InventoryItem>()
            .Where(i => i.CharacterId == characterId)
            .Get();
        return response.Models
            .OrderBy(i => i.ItemType ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<InventoryItem?> CreateInventoryItemAsync(InventoryItem item)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<InventoryItem>().Insert(item);
        return response.Models.FirstOrDefault();
    }

    public async Task<InventoryItem?> UpdateInventoryItemAsync(InventoryItem item)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<InventoryItem>().Update(item);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteInventoryItemAsync(string id)
    {
        var client = await _supabase.GetClientAsync();
        await client.From<InventoryItem>().Where(i => i.Id == id).Delete();
    }
}
