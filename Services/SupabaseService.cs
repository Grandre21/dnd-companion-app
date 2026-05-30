using System.Security.Cryptography;
using DndCompanion.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace DndCompanion.Services;

public class SupabaseService
{
    private readonly Supabase.Client _client;
    private readonly NavigationManager _navigation;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public SupabaseService(IConfiguration configuration, IJSRuntime js, NavigationManager navigation)
    {
        _navigation = navigation;

        var url = configuration["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url non configurato in appsettings.json");

        var anonKey = configuration["Supabase:AnonKey"]
            ?? throw new InvalidOperationException("Supabase:AnonKey non configurato in appsettings.json");

        var options = new Supabase.SupabaseOptions
        {
            AutoConnectRealtime = false,
            // Persistenza sessione su localStorage: il meta-client collega questo
            // handler a Gotrue e ne carica la sessione durante InitializeAsync().
            // In WASM IJSRuntime è anche IJSInProcessRuntime (sync), richiesto dall'handler.
            SessionHandler = new BrowserSessionHandler((IJSInProcessRuntime)js)
        };

        _client = new Supabase.Client(url, anonKey, options);
    }

    /// <summary>
    /// Punto unico di bootstrap della sessione. Serializzato da _initLock: il PRIMO chiamante
    /// (Home o AuthRedirect, non importa l'ordine) esegue init + ripristino sessione persistita
    /// + eventuale processing del ritorno OAuth; tutti gli altri ricevono un client con la
    /// sessione GIÀ risolta. Questo elimina la race tra AuthRedirect e l'init delle pagine.
    /// </summary>
    public async Task<Supabase.Client> GetClientAsync()
    {
        if (_initialized) return _client;

        await _initLock.WaitAsync();
        try
        {
            if (!_initialized)
            {
                await _client.InitializeAsync();

                // 1) Ripristina la sessione persistita (localStorage) → l'utente resta loggato al reload.
                //    LoadSession è sincrono e non fa rete: nessuna superficie di errore qui.
                _client.Auth.LoadSession();

                // 2) Ritorno OAuth (flusso Implicit, default libreria): i token sono nel fragment
                //    #access_token=...; un fallimento in error_description=. Va processato QUI,
                //    così la sessione è pronta prima che qualunque pagina legga l'identità.
                var uri = _navigation.Uri;
                var isOAuthReturn = uri.Contains("access_token=") || uri.Contains("error_description=");
                if (isOAuthReturn)
                {
                    try
                    {
                        await _client.Auth.GetSessionFromUrl(new Uri(uri), storeSession: true);
                    }
                    catch
                    {
                        // Sessione non stabilita: l'utente risulterà non loggato e AuthRedirect
                        // lo porterà al login.
                    }
                }

                _initialized = true;

                // 3) Pulisce l'URL dai token DOPO aver marcato _initialized (le ri-renderizzazioni
                //    indotte dalla navigazione trovano già il client pronto, niente nuovo bootstrap).
                if (isOAuthReturn)
                {
                    _navigation.NavigateTo(_navigation.BaseUri, forceLoad: false, replace: true);
                }
            }
        }
        finally
        {
            _initLock.Release();
        }

        return _client;
    }

    public async Task<List<Character>> GetCharactersForCampaignAsync(string campaignId)
    {
        var client = await GetClientAsync();
        var response = await client.From<Character>()
            .Where(c => c.CampaignId == campaignId)
            .Get();
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

    public async Task<List<Spell>> GetSpellsForCampaignAsync(string campaignId)
    {
        var client = await GetClientAsync();
        var response = await client.From<Spell>()
            .Where(s => s.CampaignId == campaignId)
            .Get();
        return response.Models;
    }

    public async Task<List<Spell>> SearchSpellsAsync(string campaignId, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetSpellsForCampaignAsync(campaignId);

        var client = await GetClientAsync();
        var response = await client.From<Spell>()
            .Where(s => s.CampaignId == campaignId)
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

    public async Task<List<Monster>> GetMonstersForCampaignAsync(string campaignId)
    {
        var client = await GetClientAsync();
        var response = await client.From<Monster>()
            .Where(m => m.CampaignId == campaignId)
            .Get();
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

    public async Task<List<Note>> GetNotesForCampaignAsync(string campaignId, string userId)
    {
        var client = await GetClientAsync();
        var response = await client.From<Note>()
            .Where(n => n.CampaignId == campaignId)
            .Get();
        // Visibili nella campagna: le condivise + le proprie note private.
        return response.Models
            .Where(n => n.IsShared || n.OwnerId == userId)
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

    public async Task<List<Profile>> GetProfilesAsync()
    {
        var client = await GetClientAsync();
        var response = await client.From<Profile>().Get();
        return response.Models;
    }

    public async Task<List<Race>> GetRacesForCampaignAsync(string campaignId)
    {
        var client = await GetClientAsync();
        var response = await client.From<Race>()
            .Where(r => r.CampaignId == campaignId)
            .Get();
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

    public async Task<List<CharacterClass>> GetClassesForCampaignAsync(string campaignId)
    {
        var client = await GetClientAsync();
        var response = await client.From<CharacterClass>()
            .Where(c => c.CampaignId == campaignId)
            .Get();
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

    // =====================================================================
    // Campagne
    // =====================================================================

    // Alfabeto senza caratteri ambigui (niente 0/O, 1/I/L) per i codici invito.
    private const string InviteCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public async Task<List<Campaign>> GetUserCampaignsAsync(string userId)
    {
        var client = await GetClientAsync();
        var members = await client.From<CampaignMember>()
            .Where(m => m.UserId == userId)
            .Get();

        var ids = members.Models.Select(m => m.CampaignId).Distinct().ToList();
        if (ids.Count == 0) return new List<Campaign>();

        var campaigns = await client.From<Campaign>()
            .Filter("id", Postgrest.Constants.Operator.In, ids.Cast<object>().ToList())
            .Get();

        return campaigns.Models
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<Campaign> CreateCampaignAsync(string name, string ownerId)
    {
        var client = await GetClientAsync();
        var inviteCode = await GenerateUniqueInviteCodeAsync();

        var insert = await client.From<Campaign>().Insert(new Campaign
        {
            Name = name,
            OwnerId = ownerId,
            InviteCode = inviteCode,
            CreatedAt = DateTime.UtcNow
        });
        var campaign = insert.Models.First();

        // Il creatore diventa automaticamente master della campagna.
        await client.From<CampaignMember>().Insert(new CampaignMember
        {
            CampaignId = campaign.Id,
            UserId = ownerId,
            Role = "master",
            JoinedAt = DateTime.UtcNow
        });

        return campaign;
    }

    public async Task<Campaign?> JoinCampaignAsync(string inviteCode, string userId)
    {
        var client = await GetClientAsync();
        var code = inviteCode.Trim().ToUpperInvariant();

        var found = await client.From<Campaign>()
            .Where(c => c.InviteCode == code)
            .Get();
        var campaign = found.Models.FirstOrDefault();
        if (campaign is null) return null;

        var existing = await client.From<CampaignMember>()
            .Where(m => m.CampaignId == campaign.Id && m.UserId == userId)
            .Get();

        if (existing.Models.Count == 0)
        {
            await client.From<CampaignMember>().Insert(new CampaignMember
            {
                CampaignId = campaign.Id,
                UserId = userId,
                Role = "player",
                JoinedAt = DateTime.UtcNow
            });
        }

        return campaign;
    }

    public async Task<List<CampaignMember>> GetCampaignMembersAsync(string campaignId)
    {
        var client = await GetClientAsync();
        var response = await client.From<CampaignMember>()
            .Where(m => m.CampaignId == campaignId)
            .Get();
        return response.Models;
    }

    public async Task<string?> GetUserRoleInCampaignAsync(string userId, string campaignId)
    {
        var client = await GetClientAsync();
        var response = await client.From<CampaignMember>()
            .Where(m => m.UserId == userId && m.CampaignId == campaignId)
            .Get();
        return response.Models.FirstOrDefault()?.Role;
    }

    private async Task<string> GenerateUniqueInviteCodeAsync()
    {
        var client = await GetClientAsync();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = GenerateInviteCode();
            var clash = await client.From<Campaign>().Where(c => c.InviteCode == code).Get();
            if (clash.Models.Count == 0) return code;
        }
        // Fallback estremamente improbabile: aggiunge entropia per evitare loop infiniti.
        return GenerateInviteCode() + Guid.NewGuid().ToString("N")[..2].ToUpperInvariant();
    }

    // Lunghezza del codice (parte variabile). Con alfabeto a 31 caratteri:
    // 8 char ≈ 39.7 bit di entropia → enumerazione impraticabile (il codice dà
    // accesso ai dati condivisi della campagna).
    private const int InviteCodeLength = 8;

    private static string GenerateInviteCode()
    {
        var alphabetLen = InviteCodeAlphabet.Length;
        // Soglia di rejection sampling per eliminare il bias di modulo.
        var maxUnbiased = 256 - (256 % alphabetLen);

        var chars = new char[InviteCodeLength];
        Span<byte> buffer = stackalloc byte[1];
        var i = 0;
        while (i < chars.Length)
        {
            RandomNumberGenerator.Fill(buffer);
            if (buffer[0] >= maxUnbiased) continue; // scarta i valori che introdurrebbero bias
            chars[i++] = InviteCodeAlphabet[buffer[0] % alphabetLen];
        }
        return "DND-" + new string(chars);
    }

    // =====================================================================
    // Profili
    // =====================================================================

    /// <summary>Crea la riga profiles per l'utente se non esiste già. Idempotente.</summary>
    public async Task EnsureProfileAsync(string userId, string? displayName)
    {
        var client = await GetClientAsync();
        var existing = await client.From<Profile>().Where(p => p.Id == userId).Get();
        if (existing.Models.Count > 0) return;

        await client.From<Profile>().Insert(new Profile
        {
            Id = userId,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow
        });
    }
}
