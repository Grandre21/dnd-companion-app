using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace DndCompanion.Tests.Integration;

/// <summary>
/// Test d'integrazione sulla visibilità dei personaggi (characters_select ristretta a
/// proprietario/master) e sulla RPC <c>get_party_overview</c>, eseguiti contro lo stack Supabase
/// LOCALE. Si auto-saltano (Skip) se lo stack non è in esecuzione, stessa forma di
/// <see cref="BackgroundsRlsIntegrationTests"/>. Avvio: `supabase start`, poi
/// `dotnet test Tests.Integration/`.
///
/// NOTA: queste migrazioni (20260731000000_party_visibility.sql) NON vengono applicate in
/// automatico dallo stack locale di test a meno che non sia già stato eseguito `supabase db reset`
/// (o equivalente) DOPO averla scritta. Se lo stack locale è avviato ma la migrazione non è ancora
/// applicata, questi test falliscono (non si auto-saltano): la Skip è solo per "stack giù", non per
/// "migrazione mancante" — a differenza dello stack giù, una migrazione mancante è un errore da
/// vedere, non da nascondere.
/// </summary>
[Collection("local-supabase")]
public sealed class PartyRlsIntegrationTests
{
    private readonly LocalSupabaseFixture _fx;
    public PartyRlsIntegrationTests(LocalSupabaseFixture fx) => _fx = fx;

    private async Task<JsonArray> GetCharactersAsUser(string token, string campaignId)
    {
        using var req = _fx.AsUser(HttpMethod.Get,
            $"characters?select=id,name&campaign_id=eq.{campaignId}", token);
        using var resp = await _fx.Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsArray();
    }

    private async Task<JsonArray> GetInventoryAsUser(string token, string characterId)
    {
        using var req = _fx.AsUser(HttpMethod.Get,
            $"inventory?select=id,name&character_id=eq.{characterId}", token);
        using var resp = await _fx.Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsArray();
    }

    private async Task<JsonArray> GetPartyOverviewAsUser(string token, string campaignId)
    {
        using var req = _fx.AsUser(HttpMethod.Post, "rpc/get_party_overview", token);
        req.Content = JsonContent.Create(new { p_campaign_id = campaignId });
        using var resp = await _fx.Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsArray();
    }

    // --- characters_select: proprietario/master sì, l'altro player no ---

    [SkippableFact]
    public async Task PlayerB_non_legge_il_personaggio_di_A_via_REST_diretto()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        var rows = await GetCharactersAsUser(_fx.TokenB, LocalSupabaseFixture.CampaignC1);
        Assert.DoesNotContain(rows, r => r!["id"]!.GetValue<string>() == LocalSupabaseFixture.CharacterAC1);
    }

    [SkippableFact]
    public async Task PlayerB_legge_il_proprio_personaggio_via_REST_diretto()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        var rows = await GetCharactersAsUser(_fx.TokenB, LocalSupabaseFixture.CampaignC1);
        Assert.Contains(rows, r => r!["id"]!.GetValue<string>() == LocalSupabaseFixture.CharacterBC1);
    }

    [SkippableFact]
    public async Task MasterA_legge_tutti_i_personaggi_di_C1()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        var rows = await GetCharactersAsUser(_fx.TokenA, LocalSupabaseFixture.CampaignC1);
        Assert.Contains(rows, r => r!["id"]!.GetValue<string>() == LocalSupabaseFixture.CharacterAC1);
        Assert.Contains(rows, r => r!["id"]!.GetValue<string>() == LocalSupabaseFixture.CharacterBC1);
    }

    // --- Cascata su inventory (v. commento in 20260731000000_party_visibility.sql) ---

    [SkippableFact]
    public async Task PlayerB_non_legge_linventario_del_personaggio_di_A()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        var rows = await GetInventoryAsUser(_fx.TokenB, LocalSupabaseFixture.CharacterAC1);
        Assert.Empty(rows);
    }

    [SkippableFact]
    public async Task MasterA_legge_linventario_del_personaggio_di_B()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        var rows = await GetInventoryAsUser(_fx.TokenA, LocalSupabaseFixture.CharacterBC1);
        Assert.Contains(rows, r => r!["id"]!.GetValue<string>() == LocalSupabaseFixture.InventoryItemBC1);
    }

    // --- get_party_overview: sole colonne sintetiche, a entrambi i membri ---

    private static readonly string[] ExpectedColumns =
    {
        "character_id", "name", "race", "class", "level", "armor_class",
        "hit_points", "max_hit_points", "passive_perception", "speed",
        "owner_id", "owner_nickname",
    };

    // Personaggi legittimamente in C1 (v. commenti su CharacterAC1/BC1/ForHoppingTest in
    // LocalSupabaseFixture): il master (CharacterAC1), il player (CharacterBC1) e
    // CharacterForHoppingTest — quest'ultimo seminato apposta in C1 (non altrove) perché lo
    // scenario di campaign hopping richiede un membro DI C1 che tenti di spostare la propria riga
    // in una campagna estranea. Non è quindi un dato spurio: 3 è il numero giusto oggi. Si
    // verifica l'insieme esatto (non solo il conteggio) così il test non si rompe silenziosamente
    // al prossimo personaggio aggiunto al seed — chi lo aggiunge aggiorna anche questa lista.
    private static readonly string[] ExpectedCharacterIdsC1 =
    {
        LocalSupabaseFixture.CharacterAC1,
        LocalSupabaseFixture.CharacterBC1,
        LocalSupabaseFixture.CharacterForHoppingTest,
    };

    [SkippableFact]
    public async Task RPC_party_overview_restituisce_le_sole_colonne_sintetiche_al_player()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        var rows = await GetPartyOverviewAsUser(_fx.TokenB, LocalSupabaseFixture.CampaignC1);

        // Insieme esatto, non solo conteggio: CharacterAC2 (C2) e CharacterForTwinTest (C3) NON
        // devono comparire, altrimenti la RPC starebbe perdendo il filtro per campagna.
        var actualIds = rows.Select(r => r!["character_id"]!.GetValue<string>());
        Assert.Equal(ExpectedCharacterIdsC1.OrderBy(id => id), actualIds.OrderBy(id => id));
        Assert.DoesNotContain(rows, r => r!["character_id"]!.GetValue<string>() == LocalSupabaseFixture.CharacterAC2);
        Assert.DoesNotContain(rows, r => r!["character_id"]!.GetValue<string>() == LocalSupabaseFixture.CharacterForTwinTest);

        // Cuore della privacy della vista Party: solo colonne sintetiche, mai le colonne grezze.
        foreach (var row in rows)
        {
            var keys = ((JsonObject)row!).Select(kv => kv.Key).ToArray();
            Assert.Equal(ExpectedColumns.OrderBy(k => k), keys.OrderBy(k => k));
        }
    }

    [SkippableFact]
    public async Task RPC_party_overview_restituisce_le_stesse_righe_al_master()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        var rows = await GetPartyOverviewAsUser(_fx.TokenA, LocalSupabaseFixture.CampaignC1);

        // Stesso insieme esatto del test sopra lato player: il master non vede righe diverse, né
        // in più né in meno (v. commento su ExpectedCharacterIdsC1 per il perché sono 3).
        var actualIds = rows.Select(r => r!["character_id"]!.GetValue<string>());
        Assert.Equal(ExpectedCharacterIdsC1.OrderBy(id => id), actualIds.OrderBy(id => id));
        Assert.DoesNotContain(rows, r => r!["character_id"]!.GetValue<string>() == LocalSupabaseFixture.CharacterAC2);
        Assert.DoesNotContain(rows, r => r!["character_id"]!.GetValue<string>() == LocalSupabaseFixture.CharacterForTwinTest);
    }

    [SkippableFact]
    public async Task RPC_party_overview_calcola_la_percezione_passiva()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        var rows = await GetPartyOverviewAsUser(_fx.TokenA, LocalSupabaseFixture.CampaignC1);

        var rigaA = rows.Single(r => r!["character_id"]!.GetValue<string>() == LocalSupabaseFixture.CharacterAC1);
        Assert.Equal(LocalSupabaseFixture.CharacterAC1PassivePerception, rigaA!["passive_perception"]!.GetValue<int>());
    }

    [SkippableFact]
    public async Task RPC_party_overview_vuota_per_chi_non_e_membro_della_campagna()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        // B non è membro di C2, che PERÒ ha un personaggio (CharacterAC2): un array vuoto qui prova
        // la guardia di appartenenza, non la semplice assenza di dati.
        var rows = await GetPartyOverviewAsUser(_fx.TokenB, LocalSupabaseFixture.CampaignC2);
        Assert.Empty(rows);
    }
}
