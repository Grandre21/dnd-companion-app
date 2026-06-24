using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface ICharacterRepository
{
    Task<List<Character>> GetCharactersForCampaignAsync(string campaignId);
    Task<Character?> CreateCharacterAsync(Character character);
    Task<Character?> UpdateCharacterAsync(Character character);
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
}
