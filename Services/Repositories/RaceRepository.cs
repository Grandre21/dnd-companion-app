using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface IRaceRepository
{
    Task<List<Race>> GetRacesForCampaignAsync(string campaignId);
    Task<Race?> CreateRaceAsync(Race race);
    Task<Race?> UpdateRaceAsync(Race race);
    Task DeleteRaceAsync(string id);
}

/// <summary>Accesso dati per il catalogo razze (tabella <c>races</c>).</summary>
public class RaceRepository : IRaceRepository
{
    private readonly SupabaseService _supabase;

    public RaceRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<List<Race>> GetRacesForCampaignAsync(string campaignId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Race>()
            .Where(r => r.CampaignId == campaignId)
            .Get();
        return response.Models;
    }

    public async Task<Race?> CreateRaceAsync(Race race)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Race>().Insert(race);
        return response.Models.FirstOrDefault();
    }

    public async Task<Race?> UpdateRaceAsync(Race race)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Race>().Update(race);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteRaceAsync(string id)
    {
        var client = await _supabase.GetClientAsync();
        await client.From<Race>().Where(r => r.Id == id).Delete();
    }
}
