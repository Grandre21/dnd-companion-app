using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace DndCompanion.Tests.Integration;

/// <summary>
/// Test d'integrazione sulle Row-Level Security, eseguiti contro lo stack Supabase LOCALE.
/// Si auto-saltano (Skip) se lo stack non è in esecuzione, così non rompono CI/altre macchine.
/// Avvio: `supabase start`, poi `dotnet test Tests.Integration/`.
/// </summary>
[Collection("local-supabase")]
public sealed class RlsIntegrationTests
{
    private readonly LocalSupabaseFixture _fx;
    public RlsIntegrationTests(LocalSupabaseFixture fx) => _fx = fx;

    private async Task<int> CountNotesAsUser(string token, string noteId)
    {
        using var req = _fx.AsUser(HttpMethod.Get, $"notes?select=id&id=eq.{noteId}", token);
        using var resp = await _fx.Http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsArray().Count;
    }

    // --- Lettura note: isolamento del privato, visibilità del condiviso, gate di campagna ---

    [SkippableFact]
    public async Task PlayerB_non_legge_la_nota_privata_di_A_nella_stessa_campagna()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        Assert.Equal(0, await CountNotesAsUser(_fx.TokenB, LocalSupabaseFixture.NotePrivateC1));
    }

    [SkippableFact]
    public async Task PlayerB_legge_la_nota_condivisa_di_A_nella_stessa_campagna()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        Assert.Equal(1, await CountNotesAsUser(_fx.TokenB, LocalSupabaseFixture.NoteSharedC1));
    }

    [SkippableFact]
    public async Task NonMembroB_non_legge_la_nota_condivisa_di_unaltra_campagna()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        Assert.Equal(0, await CountNotesAsUser(_fx.TokenB, LocalSupabaseFixture.NoteSharedC2));
    }

    [SkippableFact]
    public async Task ProprietarioA_legge_la_propria_nota_privata()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        Assert.Equal(1, await CountNotesAsUser(_fx.TokenA, LocalSupabaseFixture.NotePrivateC1));
    }

    // --- Scrittura: combat solo al master, niente auto-promozione a master ---

    [SkippableFact]
    public async Task PlayerB_non_puo_scrivere_combat_state()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        using var req = _fx.AsUser(HttpMethod.Post, "combat_state", _fx.TokenB);
        req.Headers.Add("Prefer", "return=minimal");
        req.Content = JsonContent.Create(new[]
        {
            new { campaign_id = LocalSupabaseFixture.CampaignC1, combatants = Array.Empty<object>() }
        });
        using var resp = await _fx.Http.SendAsync(req);
        Assert.False(resp.IsSuccessStatusCode,
            $"Un player NON deve poter scrivere combat_state (status {(int)resp.StatusCode}).");
    }

    [SkippableFact]
    public async Task PlayerB_non_puo_auto_promuoversi_a_master()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");
        // C2: B non è membro → l'insert come master è respinto dalla RLS (niente conflitto su unique).
        using var req = _fx.AsUser(HttpMethod.Post, "campaign_members", _fx.TokenB);
        req.Headers.Add("Prefer", "return=minimal");
        req.Content = JsonContent.Create(new[]
        {
            new { campaign_id = LocalSupabaseFixture.CampaignC2, user_id = _fx.UserBId, role = "master" }
        });
        using var resp = await _fx.Http.SendAsync(req);
        Assert.False(resp.IsSuccessStatusCode,
            $"L'auto-promozione a master NON deve riuscire (status {(int)resp.StatusCode}).");
    }
}
