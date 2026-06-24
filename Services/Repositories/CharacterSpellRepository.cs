using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface ICharacterSpellRepository
{
    Task<List<CharacterSpell>> GetCharacterSpellsAsync(string characterId);
    Task<CharacterSpell?> AddSpellToCharacterAsync(CharacterSpell entry);
    Task<bool> UpdateCharacterSpellAsync(CharacterSpell entry);
    Task<bool> RemoveCharacterSpellAsync(string id);
}

/// <summary>Accesso dati per gli incantesimi noti del singolo PG (tabella <c>character_spells</c>).</summary>
public class CharacterSpellRepository : ICharacterSpellRepository
{
    private readonly SupabaseService _supabase;

    public CharacterSpellRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<List<CharacterSpell>> GetCharacterSpellsAsync(string characterId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CharacterSpell>()
            .Where(cs => cs.CharacterId == characterId)
            .Get();
        return response.Models
            .OrderBy(cs => cs.CreatedAt)
            .ToList();
    }

    public async Task<CharacterSpell?> AddSpellToCharacterAsync(CharacterSpell entry)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CharacterSpell>().Insert(entry);
        return response.Models.FirstOrDefault();
    }

    public async Task<bool> UpdateCharacterSpellAsync(CharacterSpell entry)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CharacterSpell>().Update(entry);
        return response.Models.Count > 0;
    }

    public async Task<bool> RemoveCharacterSpellAsync(string id)
    {
        var client = await _supabase.GetClientAsync();
        await client.From<CharacterSpell>().Where(cs => cs.Id == id).Delete();
        return true;
    }
}
