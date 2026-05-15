using DndCompanion.Models;
using Microsoft.Extensions.Configuration;

namespace DndCompanion.Services;

public class SupabaseService
{
    private readonly Supabase.Client _client;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public SupabaseService(IConfiguration configuration)
    {
        var url = configuration["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url non configurato in appsettings.json");

        var anonKey = configuration["Supabase:AnonKey"]
            ?? throw new InvalidOperationException("Supabase:AnonKey non configurato in appsettings.json");

        var options = new Supabase.SupabaseOptions
        {
            AutoConnectRealtime = false
        };

        _client = new Supabase.Client(url, anonKey, options);
    }

    public async Task<Supabase.Client> GetClientAsync()
    {
        if (_initialized) return _client;

        await _initLock.WaitAsync();
        try
        {
            if (!_initialized)
            {
                await _client.InitializeAsync();
                _initialized = true;
            }
        }
        finally
        {
            _initLock.Release();
        }

        return _client;
    }

    public async Task<List<Character>> GetCharactersForPlayerAsync(string playerId)
    {
        var client = await GetClientAsync();
        var response = await client.From<Character>()
            .Where(c => c.PlayerId == playerId)
            .Get();
        return response.Models;
    }

    public async Task<List<Character>> GetAllCharactersAsync()
    {
        var client = await GetClientAsync();
        var response = await client.From<Character>().Get();
        return response.Models;
    }

    public async Task<Character?> CreateCharacterAsync(Character character)
    {
        var client = await GetClientAsync();
        var response = await client.From<Character>().Insert(character);
        return response.Models.FirstOrDefault();
    }

    public async Task<Character?> UpdateCharacterAsync(Character character)
    {
        var client = await GetClientAsync();
        var response = await client.From<Character>().Update(character);
        return response.Models.FirstOrDefault();
    }

    public async Task<List<Spell>> GetAllSpellsAsync()
    {
        var client = await GetClientAsync();
        var response = await client.From<Spell>().Get();
        return response.Models;
    }

    public async Task<List<Spell>> SearchSpellsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllSpellsAsync();

        var client = await GetClientAsync();
        var response = await client.From<Spell>()
            .Filter("name", Postgrest.Constants.Operator.ILike, $"%{query.Trim()}%")
            .Get();
        return response.Models;
    }

    public async Task<Spell?> CreateSpellAsync(Spell spell)
    {
        var client = await GetClientAsync();
        var response = await client.From<Spell>().Insert(spell);
        return response.Models.FirstOrDefault();
    }

    public async Task<Spell?> UpdateSpellAsync(Spell spell)
    {
        var client = await GetClientAsync();
        var response = await client.From<Spell>().Update(spell);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteSpellAsync(string id)
    {
        var client = await GetClientAsync();
        await client.From<Spell>().Where(s => s.Id == id).Delete();
    }

    public async Task<List<Monster>> GetAllMonstersAsync()
    {
        var client = await GetClientAsync();
        var response = await client.From<Monster>().Get();
        return response.Models;
    }

    public async Task<Monster?> CreateMonsterAsync(Monster monster)
    {
        var client = await GetClientAsync();
        var response = await client.From<Monster>().Insert(monster);
        return response.Models.FirstOrDefault();
    }

    public async Task<Monster?> UpdateMonsterAsync(Monster monster)
    {
        var client = await GetClientAsync();
        var response = await client.From<Monster>().Update(monster);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteMonsterAsync(string id)
    {
        var client = await GetClientAsync();
        await client.From<Monster>().Where(m => m.Id == id).Delete();
    }

    public async Task<List<Note>> GetNotesForPlayerAsync(string playerId)
    {
        var client = await GetClientAsync();
        // Custom auth + permissive RLS: fetch all and filter client-side.
        var response = await client.From<Note>().Get();
        return response.Models
            .Where(n => n.PlayerId == playerId || n.IsShared)
            .OrderByDescending(n => n.UpdatedAt ?? n.CreatedAt ?? DateTime.MinValue)
            .ToList();
    }

    public async Task<Note?> CreateNoteAsync(Note note)
    {
        var now = DateTime.UtcNow;
        note.CreatedAt = now;
        note.UpdatedAt = now;
        var client = await GetClientAsync();
        var response = await client.From<Note>().Insert(note);
        return response.Models.FirstOrDefault();
    }

    public async Task<Note?> UpdateNoteAsync(Note note)
    {
        note.UpdatedAt = DateTime.UtcNow;
        var client = await GetClientAsync();
        var response = await client.From<Note>().Update(note);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteNoteAsync(string id)
    {
        var client = await GetClientAsync();
        await client.From<Note>().Where(n => n.Id == id).Delete();
    }

    public async Task<List<Player>> GetAllPlayersAsync()
    {
        var client = await GetClientAsync();
        var response = await client.From<Player>().Get();
        return response.Models;
    }

    public async Task<List<Race>> GetAllRacesAsync()
    {
        var client = await GetClientAsync();
        var response = await client.From<Race>().Get();
        return response.Models;
    }

    public async Task<Race?> CreateRaceAsync(Race race)
    {
        var client = await GetClientAsync();
        var response = await client.From<Race>().Insert(race);
        return response.Models.FirstOrDefault();
    }

    public async Task<Race?> UpdateRaceAsync(Race race)
    {
        var client = await GetClientAsync();
        var response = await client.From<Race>().Update(race);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteRaceAsync(string id)
    {
        var client = await GetClientAsync();
        await client.From<Race>().Where(r => r.Id == id).Delete();
    }

    public async Task<List<CharacterClass>> GetAllClassesAsync()
    {
        var client = await GetClientAsync();
        var response = await client.From<CharacterClass>().Get();
        return response.Models;
    }

    public async Task<CharacterClass?> CreateClassAsync(CharacterClass characterClass)
    {
        var client = await GetClientAsync();
        var response = await client.From<CharacterClass>().Insert(characterClass);
        return response.Models.FirstOrDefault();
    }

    public async Task<CharacterClass?> UpdateClassAsync(CharacterClass characterClass)
    {
        var client = await GetClientAsync();
        var response = await client.From<CharacterClass>().Update(characterClass);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteClassAsync(string id)
    {
        var client = await GetClientAsync();
        await client.From<CharacterClass>().Where(c => c.Id == id).Delete();
    }

    public async Task<List<InventoryItem>> GetInventoryForCharacterAsync(string characterId)
    {
        var client = await GetClientAsync();
        var response = await client.From<InventoryItem>()
            .Where(i => i.CharacterId == characterId)
            .Get();
        return response.Models
            .OrderBy(i => i.ItemType ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<InventoryItem?> CreateInventoryItemAsync(InventoryItem item)
    {
        var client = await GetClientAsync();
        var response = await client.From<InventoryItem>().Insert(item);
        return response.Models.FirstOrDefault();
    }

    public async Task<InventoryItem?> UpdateInventoryItemAsync(InventoryItem item)
    {
        var client = await GetClientAsync();
        var response = await client.From<InventoryItem>().Update(item);
        return response.Models.FirstOrDefault();
    }

    public async Task DeleteInventoryItemAsync(string id)
    {
        var client = await GetClientAsync();
        await client.From<InventoryItem>().Where(i => i.Id == id).Delete();
    }

    public async Task<List<CharacterSpell>> GetCharacterSpellsAsync(string characterId)
    {
        var client = await GetClientAsync();
        var response = await client.From<CharacterSpell>()
            .Where(cs => cs.CharacterId == characterId)
            .Get();
        return response.Models
            .OrderBy(cs => cs.CreatedAt)
            .ToList();
    }

    public async Task<CharacterSpell?> AddSpellToCharacterAsync(CharacterSpell entry)
    {
        var client = await GetClientAsync();
        var response = await client.From<CharacterSpell>().Insert(entry);
        return response.Models.FirstOrDefault();
    }

    public async Task<bool> UpdateCharacterSpellAsync(CharacterSpell entry)
    {
        var client = await GetClientAsync();
        var response = await client.From<CharacterSpell>().Update(entry);
        return response.Models.Count > 0;
    }

    public async Task<bool> RemoveCharacterSpellAsync(string id)
    {
        var client = await GetClientAsync();
        await client.From<CharacterSpell>().Where(cs => cs.Id == id).Delete();
        return true;
    }
}
