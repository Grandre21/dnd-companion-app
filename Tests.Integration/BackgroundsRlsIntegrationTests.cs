using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace DndCompanion.Tests.Integration;

/// <summary>
/// Test d'integrazione sulle RLS della tabella "backgrounds" (modello 2024 + import dati, Task 4),
/// eseguiti contro lo stack Supabase LOCALE. Si auto-saltano (Skip) se lo stack non è in esecuzione,
/// stessa forma di <see cref="RlsIntegrationTests"/>. Avvio: `supabase start`, poi
/// `dotnet test Tests.Integration/`.
/// </summary>
[Collection("local-supabase")]
public sealed class BackgroundsRlsIntegrationTests
{
    private readonly LocalSupabaseFixture _fx;
    public BackgroundsRlsIntegrationTests(LocalSupabaseFixture fx) => _fx = fx;

    private async Task<JsonArray> GetBackgroundsAsUser(string token, string campaignId)
    {
        using var req = _fx.AsUser(HttpMethod.Get,
            $"backgrounds?select=id,name&campaign_id=eq.{campaignId}", token);
        using var resp = await _fx.Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsArray();
    }

    private async Task<JsonArray> PatchBackgroundAsUser(string token, string backgroundId, object patch)
    {
        using var req = _fx.AsUser(HttpMethod.Patch, $"backgrounds?id=eq.{backgroundId}", token);
        req.Headers.Add("Prefer", "return=representation");
        req.Content = JsonContent.Create(patch);
        using var resp = await _fx.Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsArray();
    }

    // --- Lettura: membro sì, non-membro no (backgrounds_select) ---

    [SkippableFact]
    public async Task PlayerB_legge_i_background_della_propria_campagna()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        var rows = await GetBackgroundsAsUser(_fx.TokenB, LocalSupabaseFixture.CampaignC1);
        Assert.Contains(rows, r => r!["id"]!.GetValue<string>() == LocalSupabaseFixture.BackgroundAC1);
    }

    [SkippableFact]
    public async Task NonMembroB_non_vede_i_background_di_unaltra_campagna()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        var rows = await GetBackgroundsAsUser(_fx.TokenB, LocalSupabaseFixture.CampaignC2);
        Assert.Empty(rows);
    }

    // --- Scrittura: autore o master sì, altro player no (backgrounds_update, ramo USING) ---

    [SkippableFact]
    public async Task PlayerB_non_modifica_il_background_creato_da_A()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        // B è membro di C1 ma non è né autore né master: la USING esclude la riga.
        // Nessun errore HTTP (l'update semplicemente non trova righe da toccare) → verifichiamo
        // che la rappresentazione tornata sia vuota.
        var result = await PatchBackgroundAsUser(
            _fx.TokenB, LocalSupabaseFixture.BackgroundAC1, new { name = "Modificato da B" });
        Assert.Empty(result);
    }

    [SkippableFact]
    public async Task MasterA_modifica_il_background_di_B_nella_sua_campagna()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        var result = await PatchBackgroundAsUser(
            _fx.TokenA, LocalSupabaseFixture.BackgroundBC1ForMasterEdit, new { name = "Rinominato dal master" });
        Assert.Single(result);
        Assert.Equal("Rinominato dal master", result[0]!["name"]!.GetValue<string>());
    }

    // --- is_campaign_master valutato sulla riga nuova: un master non può spostare fuori dalla
    // propria autorità un background che non ha creato lui ---
    //
    // NOTA storica (Task 4, 2026-07-25): il brief di allora chiedeva letteralmente "un membro non
    // può spostare un PROPRIO background in un'altra campagna" come scenario protetto dalla WITH
    // CHECK, ma con la policy di quel momento — added_by = auth.uid() OR is_campaign_master(...),
    // ricalcata da races_update — quello scenario specifico NON era protetto: added_by non cambia
    // con lo spostamento, quindi il ramo "sei l'autore" restava vero a prescindere dalla
    // destinazione. Non fu corretto lì perché divergere dalle altre sei tabelle era fuori mandato
    // di quel task. QUESTA LACUNA È ORA CHIUSA dalla migrazione
    // `20260806120000_close_campaign_hopping.sql` (2026-08-06), che lega il ramo autore alla
    // appartenenza corrente (is_campaign_member) invece che alla sola uguaglianza su added_by — la
    // prova è il test aggiunto sotto,
    // <see cref="AutoreA_non_puo_piu_spostare_un_proprio_background_in_una_campagna_di_cui_non_e_membro"/>.
    // Il test storico sotto resta invariato: verifica un secondo scenario, distinto e già protetto
    // anche prima della correzione — un master (sulla riga vecchia la condizione è vera, perché è
    // master della campagna di origine) che sposta una riga NON sua in una campagna di cui non è
    // master (sulla riga nuova la condizione è falsa: né "sei l'autore" né "sei master").

    [SkippableFact]
    public async Task MasterA_non_puo_spostare_in_C3_un_background_di_B_che_non_ha_creato_lui()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");

        // A è master di C1 (la riga vecchia soddisfa la condizione) ma non ha alcuna riga in
        // campaign_members per C3: sulla riga nuova la condizione (is_campaign_master) è falsa.
        using var req = _fx.AsUser(HttpMethod.Patch,
            $"backgrounds?id=eq.{LocalSupabaseFixture.BackgroundBC1ForMasterMoveDenied}", _fx.TokenA);
        req.Content = JsonContent.Create(new { campaign_id = LocalSupabaseFixture.CampaignC3 });
        using var resp = await _fx.Http.SendAsync(req);
        // Non ci basiamo sullo status HTTP di resp: un rifiuto sulla riga nuova è un errore
        // Postgres esplicito (4xx), ma un blocco "a monte" (0 righe) tornerebbe comunque 2xx — in
        // entrambi i casi la prova affidabile è rileggere dove si trova davvero la riga dopo il
        // tentativo.

        var rimastaInC1 = await GetBackgroundsAsUser(_fx.TokenA, LocalSupabaseFixture.CampaignC1);
        Assert.Contains(rimastaInC1,
            r => r!["id"]!.GetValue<string>() == LocalSupabaseFixture.BackgroundBC1ForMasterMoveDenied);
    }

    // --- Chiusura del varco (migrazione 20260806120000_close_campaign_hopping.sql): lo scenario
    // letterale del brief del Task 4, "l'autore sposta una propria riga in un'altra campagna",
    // che il test storico sopra documentava come NON protetto. Ora lo è: added_by = auth.uid() non
    // basta più, serve anche is_campaign_member(campaign_id) sulla riga di destinazione. ---

    [SkippableFact]
    public async Task AutoreA_non_puo_piu_spostare_un_proprio_background_in_una_campagna_di_cui_non_e_membro()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");

        // A è autore E membro/master di C1 (la riga vecchia soddisfa entrambe le vecchie e le nuove
        // condizioni) ma non ha alcuna riga in campaign_members per C3: prima della migrazione il
        // ramo "sei l'autore" bastava comunque; ora la WITH CHECK richiede anche is_campaign_member
        // sulla riga nuova, che qui è falsa.
        using var req = _fx.AsUser(HttpMethod.Patch,
            $"backgrounds?id=eq.{LocalSupabaseFixture.BackgroundAC1}", _fx.TokenA);
        req.Content = JsonContent.Create(new { campaign_id = LocalSupabaseFixture.CampaignC3 });
        using var resp = await _fx.Http.SendAsync(req);
        // Stessa cautela del test sopra: non fidarsi dello status HTTP, rileggere la posizione.

        var rimastaInC1 = await GetBackgroundsAsUser(_fx.TokenA, LocalSupabaseFixture.CampaignC1);
        Assert.Contains(rimastaInC1, r => r!["id"]!.GetValue<string>() == LocalSupabaseFixture.BackgroundAC1);
    }
}
