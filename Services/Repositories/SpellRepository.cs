using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface ISpellRepository
{
    Task<List<Spell>> GetSpellsForCampaignAsync(string campaignId);
    Task<List<Spell>> SearchSpellsAsync(string campaignId, string query);
    Task<Spell?> CreateSpellAsync(Spell spell);
    Task<Spell?> UpdateSpellAsync(Spell spell);
    Task DeleteSpellAsync(string id);
}

/// <summary>Accesso dati per il catalogo incantesimi (tabella <c>spells</c>).</summary>
public class SpellRepository : ISpellRepository
{
    private readonly SupabaseService _supabase;

    public SpellRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<List<Spell>> GetSpellsForCampaignAsync(string campaignId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Spell>()
            .Where(s => s.CampaignId == campaignId)
            .Get();
        return response.Models;
    }

    public async Task<List<Spell>> SearchSpellsAsync(string campaignId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetSpellsForCampaignAsync(campaignId);

        var client = await _supabase.GetClientAsync();
        var response = await client.From<Spell>()
            .Where(s => s.CampaignId == campaignId)
            .Filter("name", Postgrest.Constants.Operator.ILike, $"%{query.Trim()}%")
            .Get();
        return response.Models;
    }

    public async Task<Spell?> CreateSpellAsync(Spell spell)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Spell>().Insert(spell);
        return response.Models.FirstOrDefault();
    }

    public async Task<Spell?> UpdateSpellAsync(Spell spell)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Spell>().Update(spell);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteSpellAsync(string id)
    {
        var client = await _supabase.GetClientAsync();
        await client.From<Spell>().Where(s => s.Id == id).Delete();
    }
}
