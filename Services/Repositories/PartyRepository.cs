using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface IPartyRepository
{
    Task<List<PartyMember>> GetPartyOverviewAsync(string campaignId);
}

/// <summary>
/// Accesso dati per la vista Party: NON una tabella, ma la RPC <c>get_party_overview</c>
/// (SECURITY DEFINER lato database), che restituisce solo le colonne sintetiche del gruppo e solo
/// se il chiamante è membro della campagna — la RLS su <c>characters</c> resta comunque quella che
/// filtra la scheda personaggio (repository-per-aggregato in <see cref="CharacterRepository"/>).
/// </summary>
public class PartyRepository : IPartyRepository
{
    private readonly SupabaseService _supabase;

    public PartyRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<List<PartyMember>> GetPartyOverviewAsync(string campaignId)
    {
        var client = await _supabase.GetClientAsync();
        var result = await client.Rpc<List<PartyMember>>(
            "get_party_overview",
            new Dictionary<string, object> { { "p_campaign_id", campaignId } });
        return result ?? new List<PartyMember>();
    }
}
