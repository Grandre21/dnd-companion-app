using DndCompanion.Models;

namespace DndCompanion.Services.Repositories;

public interface ICharacterSpellRepository
{
    Task<List<CharacterSpell>> GetCharacterSpellsAsync(string characterId);
    Task<CharacterSpell?> AddSpellToCharacterAsync(CharacterSpell entry);
    Task<bool> UpdateCharacterSpellAsync(CharacterSpell entry);
    Task RemoveCharacterSpellAsync(string id);

    /// <summary>I legami che puntano a uno degli incantesimi indicati. Serve a misurare l'impatto
    /// di una cancellazione prima di eseguirla: character_spells_spell_id_fkey è ON DELETE CASCADE,
    /// quindi togliere un incantesimo dal catalogo lo toglie dalle schede che lo conoscono (§8).</summary>
    Task<List<CharacterSpell>> GetBySpellIdsAsync(List<string> spellIds);
}

/// <summary>Accesso dati per gli incantesimi noti del singolo PG (tabella <c>character_spells</c>).</summary>
public class CharacterSpellRepository : ICharacterSpellRepository
{
    private readonly SupabaseService _supabase;

    public CharacterSpellRepository(SupabaseService supabase) => _supabase = supabase;

    public async Task<List<CharacterSpell>> GetCharacterSpellsAsync(string characterId)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CharacterSpell>()
            .Where(cs => cs.CharacterId == characterId)
            .Get();
        return response.Models
            .OrderBy(cs => cs.CreatedAt)
            .ToList();
    }

    public async Task<CharacterSpell?> AddSpellToCharacterAsync(CharacterSpell entry)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CharacterSpell>().Insert(entry);
        return response.Models.FirstOrDefault();
    }

    public async Task<bool> UpdateCharacterSpellAsync(CharacterSpell entry)
    {
        var client = await _supabase.GetClientAsync();
        var response = await client.From<CharacterSpell>().Update(entry);
        return response.Models.Count > 0;
    }

    // Coerente con gli altri Delete dei repository: Task (no bool). Postgrest lancia PostgrestException
    // sugli errori HTTP (gestiti dal try/catch del chiamante); un Delete bloccato dall'RLS però ritorna
    // "successo" silenziosamente (limite di supabase-csharp 0.16.2, vedi DA-FARE §3): la UI gate via
    // CanEdit rispecchia comunque le RLS, quindi il caso non si presenta nell'uso normale.
    public async Task RemoveCharacterSpellAsync(string id)
    {
        var client = await _supabase.GetClientAsync();
        await client.From<CharacterSpell>().Where(cs => cs.Id == id).Delete();
    }

    public async Task<List<CharacterSpell>> GetBySpellIdsAsync(List<string> spellIds)
    {
        if (spellIds.Count == 0) return new List<CharacterSpell>();

        var client = await _supabase.GetClientAsync();
        var risultato = new List<CharacterSpell>();

        // A blocchi come DeleteByIdsAsync, e per la stessa ragione: con un catalogo importato per
        // intero l'elenco di id supererebbe la lunghezza utile della query string, e il 414 si
        // presenterebbe all'utente come un generico errore nel calcolo dell'anteprima.
        foreach (var blocco in spellIds.Chunk(100))
        {
            var response = await client.From<CharacterSpell>()
                .Filter("spell_id", Postgrest.Constants.Operator.In, blocco.Cast<object>().ToList())
                .Get();
            risultato.AddRange(response.Models);
        }
        return risultato;
    }
}
