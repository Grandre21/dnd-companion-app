using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface IMonsterRepository
{
    Task<List<Monster>> GetMonstersForCampaignAsync(string campaignId);
    Task<Monster?> CreateMonsterAsync(Monster monster);
    Task<Monster?> UpdateMonsterAsync(Monster monster);
    Task DeleteMonsterAsync(string id);
}

/// <summary>Accesso dati per il bestiario (tabella <c>monsters</c>).</summary>
public class MonsterRepository : IMonsterRepository
{
    private readonly SupabaseService _supabase;

    public MonsterRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<List<Monster>> GetMonstersForCampaignAsync(string campaignId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Monster>()
            .Where(m => m.CampaignId == campaignId)
            .Get();
        return response.Models;
    }

    public async Task<Monster?> CreateMonsterAsync(Monster monster)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Monster>().Insert(monster);
        return response.Models.FirstOrDefault();
    }

    public async Task<Monster?> UpdateMonsterAsync(Monster monster)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Monster>().Update(monster);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteMonsterAsync(string id)
    {
        var client = await _supabase.GetClientAsync();
        await client.From<Monster>().Where(m => m.Id == id).Delete();
    }
}
