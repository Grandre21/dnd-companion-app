using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface IBackgroundRepository
{
    Task<List<Background>> GetBackgroundsForCampaignAsync(string campaignId);
    Task<Background?> CreateBackgroundAsync(Background background);
    Task<Background?> UpdateBackgroundAsync(Background background);
    Task DeleteBackgroundAsync(string id);

    /// <summary>Creazione in blocco per l'import: una sola richiesta, quindi una sola transazione
    /// (§9). Insert e non Upsert: è l'unico dei due che rispetta [PrimaryKey("id", false)].</summary>
    Task<List<Background>> CreateManyAsync(List<Background> rows);

    /// <summary>Cancellazione per elenco di id. NON per prefisso: un LIKE costruito con testo
    /// digitato dall'utente colpirebbe righe che l'anteprima non ha mai mostrato.</summary>
    Task DeleteByIdsAsync(List<string> ids);
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

    public async Task<List<Background>> CreateManyAsync(List<Background> rows)
    {
        if (rows.Count == 0) return new List<Background>();

        var client = await _supabase.GetClientAsync();
        var response = await client.From<Background>().Insert(rows);
        return response.Models;
    }

    public async Task DeleteByIdsAsync(List<string> ids)
    {
        if (ids.Count == 0) return;

        var client = await _supabase.GetClientAsync();
        foreach (var blocco in ids.Chunk(100))
        {
            await client.From<Background>()
                .Filter("id", Postgrest.Constants.Operator.In, blocco.Cast<object>().ToList())
                .Delete();
        }
    }
}
