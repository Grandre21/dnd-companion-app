using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface IMonsterRepository
{
    Task<List<Monster>> GetMonstersForCampaignAsync(string campaignId);
    Task<Monster?> CreateMonsterAsync(Monster monster);
    Task<Monster?> UpdateMonsterAsync(Monster monster);
    Task DeleteMonsterAsync(string id);

    /// <summary>Creazione in blocco per l'import: una sola richiesta, quindi una sola transazione
    /// (§9). Insert e non Upsert: è l'unico dei due che rispetta [PrimaryKey("id", false)].</summary>
    Task<List<Monster>> CreateManyAsync(List<Monster> rows);

    /// <summary>Cancellazione per elenco di id. NON per prefisso: un LIKE costruito con testo
    /// digitato dall'utente colpirebbe righe che l'anteprima non ha mai mostrato.</summary>
    Task DeleteByIdsAsync(List<string> ids);
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

    public async Task<List<Monster>> CreateManyAsync(List<Monster> rows)
    {
        if (rows.Count == 0) return new List<Monster>();

        var client = await _supabase.GetClientAsync();
        var response = await client.From<Monster>().Insert(rows);
        return response.Models;
    }

    public async Task DeleteByIdsAsync(List<string> ids)
    {
        if (ids.Count == 0) return;

        var client = await _supabase.GetClientAsync();
        foreach (var blocco in ids.Chunk(100))
        {
            await client.From<Monster>()
                .Filter("id", Postgrest.Constants.Operator.In, blocco.Cast<object>().ToList())
                .Delete();
        }
    }
}
