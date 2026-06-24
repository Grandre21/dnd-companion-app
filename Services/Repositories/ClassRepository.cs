using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface IClassRepository
{
    Task<List<CharacterClass>> GetClassesForCampaignAsync(string campaignId);
    Task<CharacterClass?> CreateClassAsync(CharacterClass characterClass);
    Task<CharacterClass?> UpdateClassAsync(CharacterClass characterClass);
    Task DeleteClassAsync(string id);
}

/// <summary>Accesso dati per il catalogo classi (tabella <c>classes</c>).</summary>
public class ClassRepository : IClassRepository
{
    private readonly SupabaseService _supabase;

    public ClassRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<List<CharacterClass>> GetClassesForCampaignAsync(string campaignId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CharacterClass>()
            .Where(c => c.CampaignId == campaignId)
            .Get();
        return response.Models;
    }

    public async Task<CharacterClass?> CreateClassAsync(CharacterClass characterClass)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CharacterClass>().Insert(characterClass);
        return response.Models.FirstOrDefault();
    }

    public async Task<CharacterClass?> UpdateClassAsync(CharacterClass characterClass)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CharacterClass>().Update(characterClass);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteClassAsync(string id)
    {
        var client = await _supabase.GetClientAsync();
        await client.From<CharacterClass>().Where(c => c.Id == id).Delete();
    }
}
