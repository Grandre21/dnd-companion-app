using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface ICombatStateRepository
{
    Task<CombatState?> GetCombatStateAsync(string campaignId);
    Task SaveCombatStateAsync(CombatState state);
}

/// <summary>Accesso dati per il combattimento condiviso (tabella <c>combat_state</c>: una riga per campagna).</summary>
public class CombatStateRepository : ICombatStateRepository
{
    private readonly SupabaseService _supabase;

    public CombatStateRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<CombatState?> GetCombatStateAsync(string campaignId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CombatState>()
            .Where(c => c.CampaignId == campaignId)
            .Get();
        return response.Models.FirstOrDefault();
    }

    public async Task SaveCombatStateAsync(CombatState state)
    {
        state.UpdatedAt = DateTime.UtcNow;
        var client = await _supabase.GetClientAsync();
        await client.From<CombatState>().Upsert(state);
    }
}
