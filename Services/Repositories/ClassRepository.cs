using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface IClassRepository
{
    Task<List<CharacterClass>> GetClassesForCampaignAsync(string campaignId);
    Task<CharacterClass?> CreateClassAsync(CharacterClass characterClass);
    Task<CharacterClass?> UpdateClassAsync(CharacterClass characterClass);
    Task DeleteClassAsync(string id);

    /// <summary>Creazione in blocco per l'import: una sola richiesta, quindi una sola transazione
    /// (§9). Insert e non Upsert: è l'unico dei due che rispetta [PrimaryKey("id", false)].</summary>
    Task<List<CharacterClass>> CreateManyAsync(List<CharacterClass> rows);

    /// <summary>Cancellazione per elenco di id. NON per prefisso: un LIKE costruito con testo
    /// digitato dall'utente colpirebbe righe che l'anteprima non ha mai mostrato.</summary>
    Task DeleteByIdsAsync(List<string> ids);
}

/// <summary>Accesso dati per il catalogo classi (tabella <c>classes</c>).</summary>
public class ClassRepository : IClassRepository
{
    private readonly SupabaseService _supabase;

    public ClassRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<List<CharacterClass>> GetClassesForCampaignAsync(string campaignId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CharacterClass>()
            .Where(c => c.CampaignId == campaignId)
            .Get();
        return response.Models;
    }

    public async Task<CharacterClass?> CreateClassAsync(CharacterClass characterClass)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CharacterClass>().Insert(characterClass);
        return response.Models.FirstOrDefault();
    }

    public async Task<CharacterClass?> UpdateClassAsync(CharacterClass characterClass)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CharacterClass>().Update(characterClass);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteClassAsync(string id)
    {
        var client = await _supabase.GetClientAsync();
        await client.From<CharacterClass>().Where(c => c.Id == id).Delete();
    }

    public async Task<List<CharacterClass>> CreateManyAsync(List<CharacterClass> rows)
    {
        if (rows.Count == 0) return new List<CharacterClass>();

        var client = await _supabase.GetClientAsync();
        var response = await client.From<CharacterClass>().Insert(rows);
        return response.Models;
    }

    public async Task DeleteByIdsAsync(List<string> ids)
    {
        if (ids.Count == 0) return;

        var client = await _supabase.GetClientAsync();
        foreach (var blocco in ids.Chunk(100))
        {
            await client.From<CharacterClass>()
                .Filter("id", Postgrest.Constants.Operator.In, blocco.Cast<object>().ToList())
                .Delete();
        }
    }
}
