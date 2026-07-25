using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface IBackgroundRepository
{
    Task<List<Background>> GetBackgroundsForCampaignAsync(string campaignId);
    Task<Background?> CreateBackgroundAsync(Background background);
    Task<Background?> UpdateBackgroundAsync(Background background);
    Task DeleteBackgroundAsync(string id);
}

/// <summary>Accesso dati per il catalogo background (tabella <c>backgrounds</c>).</summary>
public class BackgroundRepository : IBackgroundRepository
{
    private readonly SupabaseService _supabase;

    public BackgroundRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<List<Background>> GetBackgroundsForCampaignAsync(string campaignId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Background>()
            .Where(b => b.CampaignId == campaignId)
            .Get();
        return response.Models;
    }

    public async Task<Background?> CreateBackgroundAsync(Background background)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Background>().Insert(background);
        return response.Models.FirstOrDefault();
    }

    public async Task<Background?> UpdateBackgroundAsync(Background background)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Background>().Update(background);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteBackgroundAsync(string id)
    {
        var client = await _supabase.GetClientAsync();
        await client.From<Background>().Where(b => b.Id == id).Delete();
    }
}
