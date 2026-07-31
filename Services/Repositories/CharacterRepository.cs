using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface ICharacterRepository
{
    Task<List<Character>> GetCharactersForCampaignAsync(string campaignId);
    Task<Character?> CreateCharacterAsync(Character character);
    Task<Character?> UpdateCharacterAsync(Character character);
    Task DeleteCharacterAsync(string id);
}

/// <summary>Accesso dati per i personaggi (tabella <c>characters</c>).</summary>
public class CharacterRepository : ICharacterRepository
{
    private readonly SupabaseService _supabase;

    public CharacterRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<List<Character>> GetCharactersForCampaignAsync(string campaignId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Character>()
            .Where(c => c.CampaignId == campaignId)
            .Get();
        return response.Models;
    }

    public async Task<Character?> CreateCharacterAsync(Character character)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Character>().Insert(character);
        return response.Models.FirstOrDefault();
    }

    public async Task<Character?> UpdateCharacterAsync(Character character)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Character>().Update(character);
        return response.Models.FirstOrDefault();
    }

    /// <summary>Elimina un personaggio. Inventario e incantesimi del PG se ne vanno con lui: le due
    /// chiavi esterne verso <c>characters</c> sono <c>ON DELETE CASCADE</c>, quindi non c'è nulla da
    /// ripulire prima. Chi può farlo lo decide la policy <c>characters_delete</c> (proprietario o
    /// master): senza il permesso il server non cancella nulla e non solleva errori, per cui la
    /// pagina deve comunque ricaricare l'elenco invece di fidarsi dell'esito.</summary>
    public async Task DeleteCharacterAsync(string id)
    {
        var client = await _supabase.GetClientAsync();
        await client.From<Character>().Where(c => c.Id == id).Delete();
    }
}
