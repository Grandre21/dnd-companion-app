using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace DndCompanion.Tests.Integration;

/// <summary>
/// Fixture per i test d'integrazione RLS contro lo stack Supabase LOCALE (`supabase start`).
/// - Rileva se lo stack è raggiungibile (<see cref="Available"/>): se no, i test si auto-saltano.
/// - Crea 2 utenti di test e semina i dati in modo idempotente (cleanup + insert) via la chiave
///   <c>service_role</c>, che bypassa le RLS.
/// Le chiavi qui sono quelle FISSE di Supabase locale (uguali per chiunque, NON segrete).
/// </summary>
public sealed class LocalSupabaseFixture : IAsyncLifetime
{
    public const string ApiUrl = "http://127.0.0.1:54321";
    public const string AnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6ImFub24iLCJleHAiOjE5ODM4MTI5OTZ9.CRXP1A7WOeoJeXxjNni43kdQwgnWNReilDMblYTn_I0";
    public const string ServiceKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImV4cCI6MTk4MzgxMjk5Nn0.EGIM96RAZx35lJzdJsyH-qQwv8Hdp7fsn3W0YpN81IU";

    // UUID fissi → seed riproducibile.
    public const string CampaignC1 = "11111111-1111-1111-1111-111111111111"; // A master, B player
    public const string CampaignC2 = "22222222-2222-2222-2222-222222222222"; // A master, B NON membro
    // C3: nessuno dei due utenti di test ha una riga in campaign_members qui (nemmeno A, pur
    // essendone owner_id) → serve come destinazione "di cui A non è né membro né master" per lo
    // scenario WITH CHECK (vedi BackgroundBC1ForMasterMoveDenied).
    public const string CampaignC3 = "33333333-3333-3333-3333-333333333333";
    public const string NotePrivateC1 = "a1111111-1111-1111-1111-111111111111"; // A, privata, in C1
    public const string NoteSharedC1 = "a2222222-2222-2222-2222-222222222222";  // A, condivisa, in C1
    public const string NoteSharedC2 = "a3333333-3333-3333-3333-333333333333";  // A, condivisa, in C2

    // Background (Task 4): righe di provenienza diversa per isolare ogni scenario RLS.
    public const string BackgroundAC1 = "b1111111-1111-1111-1111-111111111111";         // A, in C1
    public const string BackgroundAC2 = "b2222222-2222-2222-2222-222222222222";         // A, in C2
    public const string BackgroundBC1ForMasterEdit = "b3333333-3333-3333-3333-333333333333"; // B, in C1 (il master A la modifica)
    // B, in C1: il master A (non autore) prova a spostarla in C3, di cui A non è master.
    // added_by resta B in entrambe le valutazioni, quindi solo il ramo is_campaign_master
    // distingue riga vecchia (C1, A è master → USING passa) da riga nuova (C3, A non è master
    // → WITH CHECK nega). Vedi nota nel test: lo scenario letterale del brief ("l'autore sposta
    // una propria riga") NON è invece bloccato da questa policy — added_by=self resta vero a
    // prescindere dalla destinazione (finding BLOCCANTE confermato in revisione, non corretto qui
    // perché richiederebbe divergere dalla policy di races, fuori mandato di questo task).
    public const string BackgroundBC1ForMasterMoveDenied = "b4444444-4444-4444-4444-444444444444";

    private const string EmailA = "rls-test-a@example.com";
    private const string EmailB = "rls-test-b@example.com";
    private const string Password = "password123";

    public bool Available { get; private set; }
    public string UserAId { get; private set; } = "";
    public string UserBId { get; private set; } = "";
    public string TokenA { get; private set; } = "";
    public string TokenB { get; private set; } = "";

    public HttpClient Http { get; } = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task InitializeAsync()
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiUrl}/auth/v1/health");
            req.Headers.Add("apikey", AnonKey);
            using var resp = await Http.SendAsync(req);
            Available = resp.IsSuccessStatusCode;
        }
        catch
        {
            Available = false;
        }
        if (!Available) return;

        await EnsureUserAsync(EmailA);
        await EnsureUserAsync(EmailB);
        (UserAId, TokenA) = await SignInAsync(EmailA);
        (UserBId, TokenB) = await SignInAsync(EmailB);

        await SeedAsync();
    }

    public Task DisposeAsync()
    {
        Http.Dispose();
        return Task.CompletedTask;
    }

    // Crea l'utente via admin API; se esiste già va bene (il login restituirà comunque id+token).
    private async Task EnsureUserAsync(string email)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiUrl}/auth/v1/admin/users");
        req.Headers.Add("apikey", ServiceKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ServiceKey);
        req.Content = JsonContent.Create(new { email, password = Password, email_confirm = true });
        using var resp = await Http.SendAsync(req);
        // 200/201 = creato; 4xx tipicamente "già registrato" → ignorato di proposito.
    }

    private async Task<(string id, string token)> SignInAsync(string email)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiUrl}/auth/v1/token?grant_type=password");
        req.Headers.Add("apikey", AnonKey);
        req.Content = JsonContent.Create(new { email, password = Password });
        using var resp = await Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var json = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        return (json["user"]!["id"]!.GetValue<string>(), json["access_token"]!.GetValue<string>());
    }

    private async Task SeedAsync()
    {
        // Cleanup idempotente: elimina le campagne di test → cascade (FK ON DELETE CASCADE) su
        // campaign_members, notes, combat_state, backgrounds. Poi reinserisce da zero.
        await DeleteAsync($"campaigns?id=in.({CampaignC1},{CampaignC2},{CampaignC3})");

        await InsertAsync("campaigns", new[]
        {
            new { id = CampaignC1, name = "RLS Test C1", owner_id = UserAId, invite_code = "RLSTESTC1" },
            new { id = CampaignC2, name = "RLS Test C2", owner_id = UserAId, invite_code = "RLSTESTC2" },
            // C3: owner_id valorizzato solo perché la colonna è NOT NULL: is_campaign_master/member
            // guardano campaign_members, non campaigns.owner_id, quindi A resta "estraneo" a C3.
            new { id = CampaignC3, name = "RLS Test C3", owner_id = UserAId, invite_code = "RLSTESTC3" },
        });
        await InsertAsync("campaign_members", new[]
        {
            new { campaign_id = CampaignC1, user_id = UserAId, role = "master" },
            new { campaign_id = CampaignC1, user_id = UserBId, role = "player" },
            new { campaign_id = CampaignC2, user_id = UserAId, role = "master" },
        });
        await InsertAsync("notes", new[]
        {
            new { id = NotePrivateC1, title = "A privata C1", is_shared = false, owner_id = UserAId, campaign_id = CampaignC1 },
            new { id = NoteSharedC1, title = "A condivisa C1", is_shared = true, owner_id = UserAId, campaign_id = CampaignC1 },
            new { id = NoteSharedC2, title = "A condivisa C2", is_shared = true, owner_id = UserAId, campaign_id = CampaignC2 },
        });
        await InsertAsync("backgrounds", new[]
        {
            new { id = BackgroundAC1, name = "Background A in C1", added_by = UserAId, campaign_id = CampaignC1 },
            new { id = BackgroundAC2, name = "Background A in C2", added_by = UserAId, campaign_id = CampaignC2 },
            new { id = BackgroundBC1ForMasterEdit, name = "Background B in C1 (master edit)", added_by = UserBId, campaign_id = CampaignC1 },
            new { id = BackgroundBC1ForMasterMoveDenied, name = "Background B in C1 (master move denied)", added_by = UserBId, campaign_id = CampaignC1 },
        });
    }

    private async Task DeleteAsync(string pathAndQuery)
    {
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"{ApiUrl}/rest/v1/{pathAndQuery}");
        req.Headers.Add("apikey", ServiceKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ServiceKey);
        using var resp = await Http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Cleanup '{pathAndQuery}' fallito: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
    }

    private async Task InsertAsync(string table, object rows)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiUrl}/rest/v1/{table}");
        req.Headers.Add("apikey", ServiceKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ServiceKey);
        req.Headers.Add("Prefer", "return=minimal");
        req.Content = JsonContent.Create(rows);
        using var resp = await Http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Seed '{table}' fallito: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
    }

    /// <summary>Crea una richiesta REST autenticata come l'utente con il JWT indicato.</summary>
    public HttpRequestMessage AsUser(HttpMethod method, string pathAndQuery, string userToken)
    {
        var req = new HttpRequestMessage(method, $"{ApiUrl}/rest/v1/{pathAndQuery}");
        req.Headers.Add("apikey", AnonKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        return req;
    }
}

[CollectionDefinition("local-supabase")]
public sealed class LocalSupabaseCollection : ICollectionFixture<LocalSupabaseFixture> { }
