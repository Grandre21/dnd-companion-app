using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface IRaceRepository
{
    Task<List<Race>> GetRacesForCampaignAsync(string campaignId);
    Task<Race?> CreateRaceAsync(Race race);
    Task<Race?> UpdateRaceAsync(Race race);
    Task DeleteRaceAsync(string id);

    /// <summary>Creazione in blocco per l'import: una sola richiesta, quindi una sola transazione
    /// (§9). Insert e non Upsert: è l'unico dei due che rispetta [PrimaryKey("id", false)].</summary>
    Task<List<Race>> CreateManyAsync(List<Race> rows);

    /// <summary>Cancellazione per elenco di id. NON per prefisso: un LIKE costruito con testo
    /// digitato dall'utente colpirebbe righe che l'anteprima non ha mai mostrato.</summary>
    Task DeleteByIdsAsync(List<string> ids);
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

    public async Task<List<Race>> CreateManyAsync(List<Race> rows)
    {
        if (rows.Count == 0) return new List<Race>();

        var client = await _supabase.GetClientAsync();
        var response = await client.From<Race>().Insert(rows);
        return response.Models;
    }

    public async Task DeleteByIdsAsync(List<string> ids)
    {
        if (ids.Count == 0) return;

        var client = await _supabase.GetClientAsync();
        foreach (var blocco in ids.Chunk(100))
        {
            await client.From<Race>()
                .Filter("id", Postgrest.Constants.Operator.In, blocco.Cast<object>().ToList())
                .Delete();
        }
    }
}
