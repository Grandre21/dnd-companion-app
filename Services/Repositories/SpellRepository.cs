using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface ISpellRepository
{
    Task<List<Spell>> GetSpellsForCampaignAsync(string campaignId);
    Task<List<Spell>> SearchSpellsAsync(string campaignId, string query);
    Task<Spell?> CreateSpellAsync(Spell spell);
    Task<Spell?> UpdateSpellAsync(Spell spell);
    Task DeleteSpellAsync(string id);

    /// <summary>Creazione in blocco per l'import: una sola richiesta, quindi una sola transazione
    /// (§9). Insert e non Upsert: è l'unico dei due che rispetta [PrimaryKey("id", false)].</summary>
    Task<List<Spell>> CreateManyAsync(List<Spell> rows);

    /// <summary>La riga di questa campagna con quella provenienza, se c'è. Serve alla
    /// materializzazione (§4.4), che legge prima di inserire e rilegge se l'inserimento urta il
    /// vincolo di unicità.</summary>
    Task<Spell?> GetOneBySourceAsync(string campaignId, string sourceId);

    /// <summary>Cancellazione per elenco di id. NON per prefisso: un LIKE costruito con testo
    /// digitato dall'utente colpirebbe righe che l'anteprima non ha mai mostrato.</summary>
    Task DeleteByIdsAsync(List<string> ids);
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

    public async Task<List<Spell>> CreateManyAsync(List<Spell> rows)
    {
        if (rows.Count == 0) return new List<Spell>();

        var client = await _supabase.GetClientAsync();
        var response = await client.From<Spell>().Insert(rows);
        return response.Models;
    }

    public async Task<Spell?> GetOneBySourceAsync(string campaignId, string sourceId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<Spell>()
            .Where(s => s.CampaignId == campaignId)
            .Filter("source_id", Postgrest.Constants.Operator.Equals, sourceId)
            .Get();
        return response.Models.FirstOrDefault();
    }

    // A blocchi: gli id finiscono nella query string come id=in.(…), e un import completo può
    // superarne il limite di lunghezza — proprio il caso per cui la rimozione in blocco esiste.
    // Il Delete di questa libreria non restituisce nulla da controllare (gotcha noto, §3 di
    // DA-FARE): chi chiama riconta dopo, invece di fidarsi.
    public async Task DeleteByIdsAsync(List<string> ids)
    {
        if (ids.Count == 0) return;

        var client = await _supabase.GetClientAsync();
        foreach (var blocco in ids.Chunk(100))
        {
            await client.From<Spell>()
                .Filter("id", Postgrest.Constants.Operator.In, blocco.Cast<object>().ToList())
                .Delete();
        }
    }
}
