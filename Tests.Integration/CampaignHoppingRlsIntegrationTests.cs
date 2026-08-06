using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace DndCompanion.Tests.Integration;

/// <summary>
/// Test d'integrazione sulla chiusura del varco RLS «campaign hopping» (migrazione
/// 20260806120000_close_campaign_hopping.sql), eseguiti contro lo stack Supabase LOCALE. Si
/// auto-saltano (Skip) se lo stack non è in esecuzione o se la migrazione non è ancora applicata
/// in locale, stessa forma di <see cref="BackgroundsRlsIntegrationTests"/>. Avvio: `supabase start`,
/// poi `dotnet test Tests.Integration/`.
///
/// Copre le due tabelle più esposte fra le sette toccate dalla migrazione (v. commento in cima al
/// file SQL): <c>notes</c> (il caso peggiore — nessun ramo master, l'iniezione è irreversibile) e
/// <c>characters</c> (l'effetto più ampio — il PG compare nell'import del tracker e l'autore non
/// perde la vista). Le altre cinque tabelle (races/classes/spells/monsters/backgrounds) condividono
/// TESTUALMENTE la stessa struttura di policy (added_by invece di owner_id, stesso OR con
/// is_campaign_master): la conferma che quello schema è chiuso sta nel nuovo test aggiunto a
/// <see cref="BackgroundsRlsIntegrationTests"/>, non duplicata qui tabella per tabella.
///
/// Ogni scenario copre una delle due metà della correzione (v. commento SQL):
/// - "hopping": la USING sulla riga vecchia passa (l'autore è ancora membro della campagna
///   d'origine), ma la WITH CHECK sulla riga nuova deve negare la campagna di destinazione.
/// - "gemello": la riga è già orfana (l'autore non è membro della campagna in cui si trova), quindi
///   deve essere la USING stessa a negare qualunque update, anche il più innocuo.
/// Per ciascuna tabella c'è anche un update legittimo, che deve continuare a funzionare esattamente
/// come prima: una policy troppo stretta romperebbe l'app in silenzio, e conta quanto il varco chiuso.
/// </summary>
[Collection("local-supabase")]
public sealed class CampaignHoppingRlsIntegrationTests
{
    private readonly LocalSupabaseFixture _fx;
    public CampaignHoppingRlsIntegrationTests(LocalSupabaseFixture fx) => _fx = fx;

    private async Task<JsonArray> GetNotesAsUser(string token, string campaignId)
    {
        using var req = _fx.AsUser(HttpMethod.Get,
            $"notes?select=id,title&campaign_id=eq.{campaignId}", token);
        using var resp = await _fx.Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsArray();
    }

    private async Task<JsonArray> PatchNoteAsUser(string token, string noteId, object patch)
    {
        using var req = _fx.AsUser(HttpMethod.Patch, $"notes?id=eq.{noteId}", token);
        req.Headers.Add("Prefer", "return=representation");
        req.Content = JsonContent.Create(patch);
        using var resp = await _fx.Http.SendAsync(req);
        // Non ci basiamo sullo status HTTP: una WITH CHECK negata sulla riga nuova è un errore
        // Postgres esplicito (4xx), mentre una USING che non trova righe da toccare torna comunque
        // 2xx con rappresentazione vuota. Le assert dei singoli test guardano il contenuto, non lo
        // status — stessa cautela di MasterA_non_puo_spostare_in_C3... in BackgroundsRlsIntegrationTests.
        if (resp.IsSuccessStatusCode)
            return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsArray();
        return new JsonArray();
    }

    private async Task<JsonArray> GetCharactersAsUser(string token, string campaignId)
    {
        using var req = _fx.AsUser(HttpMethod.Get,
            $"characters?select=id,name&campaign_id=eq.{campaignId}", token);
        using var resp = await _fx.Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsArray();
    }

    private async Task<JsonArray> PatchCharacterAsUser(string token, string characterId, object patch)
    {
        using var req = _fx.AsUser(HttpMethod.Patch, $"characters?id=eq.{characterId}", token);
        req.Headers.Add("Prefer", "return=representation");
        req.Content = JsonContent.Create(patch);
        using var resp = await _fx.Http.SendAsync(req);
        if (resp.IsSuccessStatusCode)
            return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsArray();
        return new JsonArray();
    }

    // ==========================================================================================
    // notes — il caso peggiore: nessun ramo master, quindi qui la chiusura del varco è la SOLA
    // difesa (non c'è un master che possa ripulire una nota iniettata).
    // ==========================================================================================

    [SkippableFact]
    public async Task Autore_non_puo_spostare_una_propria_nota_condivisa_in_una_campagna_di_cui_non_e_membro()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");

        // A è ancora membro/master di C1 (la riga vecchia soddisfa la USING) ma non ha alcuna riga
        // in campaign_members per C3 (la riga nuova NON soddisfa la WITH CHECK: notes non ha un
        // ramo master, quindi qui basta is_campaign_member).
        await PatchNoteAsUser(_fx.TokenA, LocalSupabaseFixture.NoteForHoppingTest,
            new { campaign_id = LocalSupabaseFixture.CampaignC3 });

        var rimastaInC1 = await GetNotesAsUser(_fx.TokenA, LocalSupabaseFixture.CampaignC1);
        Assert.Contains(rimastaInC1, r => r!["id"]!.GetValue<string>() == LocalSupabaseFixture.NoteForHoppingTest);

        var finitaInC3 = await GetNotesAsUser(_fx.TokenA, LocalSupabaseFixture.CampaignC3);
        Assert.DoesNotContain(finitaInC3, r => r!["id"]!.GetValue<string>() == LocalSupabaseFixture.NoteForHoppingTest);
    }

    [SkippableFact]
    public async Task ExMembro_non_modifica_una_propria_nota_rimasta_in_una_campagna_che_ha_lasciato()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");

        // NoteForTwinTest sta in C3, di cui A NON è membro/master (simula un ex-membro senza dover
        // rimuovere davvero una riga da campaign_members): la USING sulla riga vecchia deve negare
        // anche il tentativo più innocuo, un semplice cambio di titolo.
        var result = await PatchNoteAsUser(_fx.TokenA, LocalSupabaseFixture.NoteForTwinTest,
            new { title = "Tentativo di rinomina da ex-membro" });
        Assert.Empty(result);
    }

    [SkippableFact]
    public async Task Autore_rinomina_legittimamente_una_propria_nota_restando_membro_della_campagna()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");

        // Update legittimo di controllo: A resta membro di C1, campaign_id non cambia. Deve
        // continuare a funzionare esattamente come prima della migrazione.
        var result = await PatchNoteAsUser(_fx.TokenA, LocalSupabaseFixture.NoteForLegitimateUpdateTest,
            new { title = "Rinominata legittimamente" });
        Assert.Single(result);
        Assert.Equal("Rinominata legittimamente", result[0]!["title"]!.GetValue<string>());
    }

    // ==========================================================================================
    // characters — l'effetto più ampio: il PG spostato compare fra i personaggi (e nell'import del
    // tracker) della campagna bersaglio, e l'autore non perde la vista (owner_id resta un ramo di
    // characters_select anche fuori dalla propria campagna).
    // ==========================================================================================

    [SkippableFact]
    public async Task Proprietario_non_puo_spostare_un_proprio_personaggio_in_una_campagna_di_cui_non_e_membro()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");

        // B è ancora membro di C1 (USING sulla riga vecchia passa) ma non è né membro né master di
        // C3 (WITH CHECK sulla riga nuova nega, sia il ramo proprietario che quello master).
        await PatchCharacterAsUser(_fx.TokenB, LocalSupabaseFixture.CharacterForHoppingTest,
            new { campaign_id = LocalSupabaseFixture.CampaignC3 });

        var rimastoInC1 = await GetCharactersAsUser(_fx.TokenB, LocalSupabaseFixture.CampaignC1);
        Assert.Contains(rimastoInC1, r => r!["id"]!.GetValue<string>() == LocalSupabaseFixture.CharacterForHoppingTest);
    }

    [SkippableFact]
    public async Task ExMembro_non_modifica_un_proprio_personaggio_rimasto_in_una_campagna_che_ha_lasciato()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");

        // CharacterForTwinTest sta in C3, di cui B non è membro/master: la USING sulla riga vecchia
        // deve negare anche un update innocuo (un cambio di nome), esattamente come per le note.
        var result = await PatchCharacterAsUser(_fx.TokenB, LocalSupabaseFixture.CharacterForTwinTest,
            new { name = "Tentativo di rinomina da ex-membro" });
        Assert.Empty(result);
    }

    [SkippableFact]
    public async Task Proprietario_modifica_legittimamente_il_proprio_personaggio_restando_membro_della_campagna()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");

        // Update legittimo di controllo (proprietario): B resta membro di C1, campaign_id non
        // cambia. Usa la colonna "notes" (testo libero del PG, non verificata da altri test) per non
        // toccare valori che PartyRlsIntegrationTests confronta.
        var result = await PatchCharacterAsUser(_fx.TokenB, LocalSupabaseFixture.CharacterBC1,
            new { notes = "Annotazione scritta dal proprietario" });
        Assert.Single(result);
        Assert.Equal("Annotazione scritta dal proprietario", result[0]!["notes"]!.GetValue<string>());
    }

    [SkippableFact]
    public async Task Master_modifica_legittimamente_un_personaggio_altrui_nella_propria_campagna()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");

        // Update legittimo di controllo (master): il ramo is_campaign_master non è stato toccato
        // dalla migrazione, quindi deve continuare a funzionare tale e quale sulle righe altrui.
        var result = await PatchCharacterAsUser(_fx.TokenA, LocalSupabaseFixture.CharacterBC1,
            new { notes = "Annotazione scritta dal master" });
        Assert.Single(result);
        Assert.Equal("Annotazione scritta dal master", result[0]!["notes"]!.GetValue<string>());
    }
}
