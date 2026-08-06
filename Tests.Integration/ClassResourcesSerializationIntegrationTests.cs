using DndCompanion.Models;
using DndCompanion.Services;
using Supabase.Gotrue;
using Xunit;

namespace DndCompanion.Tests.Integration;

/// <summary>
/// Test d'integrazione sul giro di andata e ritorno di <c>class_resources</c> (jsonb),
/// dell'addestramento e dei due flag arma introdotti da
/// <c>supabase/migrations/20260806130000_scheda_carta.sql</c>, eseguiti attraverso il VERO client
/// postgrest-csharp — lo stesso <see cref="SupabaseClient"/> e lo stesso path
/// <c>client.From&lt;Character&gt;().Insert/Update</c> di
/// <c>Services/Repositories/CharacterRepository.cs</c> e <c>InventoryRepository.cs</c> — invece di
/// REST grezzo, perché il rischio non è nella policy RLS ma nella serializzazione: postgrest-csharp
/// manda TUTTE le colonne mappate del modello a ogni Update/Insert, e <see cref="ClassResource"/> è
/// un POCO PascalCase senza alcun attributo di serializzazione (nessun [Column], nessun
/// [JsonProperty], a differenza di <see cref="Character"/>/<see cref="InventoryItem"/> che sono
/// BaseModel con [Column]). Se il jsonb prodotto per List&lt;ClassResource&gt; non combacia con ciò
/// che Postgres accetta o si legge indietro in modo diverso da come è stato scritto, l'effetto non è
/// "la funzione nuova non va": è che OGNI salvataggio di characters/inventory fallisce, per tutti,
/// perché ogni Update manda anche questa colonna.
///
/// Si auto-salta (Skip) se lo stack Supabase locale non è in esecuzione, stesso pattern degli altri
/// file in Tests.Integration/. Avvio: `supabase start`, poi `dotnet test Tests.Integration/`.
/// </summary>
[Collection("local-supabase")]
public sealed class ClassResourcesSerializationIntegrationTests : IAsyncLifetime
{
    private readonly LocalSupabaseFixture _fx;
    private readonly List<string> _characterIdsToClean = new();
    private SupabaseClient _client = null!;

    public ClassResourcesSerializationIntegrationTests(LocalSupabaseFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        // Costruito comunque anche se lo stack è giù: nessuna chiamata di rete nel costruttore
        // (stesso schema di Services/SupabaseService.cs), la rete parte solo nelle chiamate Insert/
        // Update/Get che i singoli [SkippableFact] non raggiungono se Skip.IfNot scatta prima.
        _client = BuildClientAsUser(_fx.TokenA);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (!_fx.Available) return;
        // Cascade (characters -> inventory ON DELETE CASCADE) ripulisce anche gli oggetti creati
        // dai test sull'inventario: basta cancellare i personaggi di questa istanza di test.
        foreach (var id in _characterIdsToClean)
        {
            try { await _client.From<Character>().Where(c => c.Id == id).Delete(); }
            catch { /* pulizia a titolo di cortesia: un fallimento qui non deve nascondere l'esito del test */ }
        }
    }

    /// <summary>
    /// Stesso schema di costruzione di <see cref="SupabaseService"/> (Gotrue Client + Postgrest.Client
    /// con GetHeaders che manda apikey + Bearer del token), qui col token fisso di un utente di test
    /// invece che con la sessione bootstrap: costruisce lo stesso <see cref="SupabaseClient"/> che i
    /// repository usano in produzione, autenticato come UserA — master di
    /// <see cref="LocalSupabaseFixture.CampaignC1"/>, quindi membro: <c>characters_insert</c> lo ammette.
    ///
    /// GOTCHA SOLO-LOCALE (verificato empiricamente, non un difetto di produzione — altrimenti
    /// l'app non funzionerebbe affatto): <c>Postgrest.Table&lt;T&gt;.GenerateUrl()</c> duplica DI
    /// PROPOSITO <c>ClientOptions.Headers["apikey"]</c> anche nella querystring (fonte:
    /// github.com/supabase-community/postgrest-csharp, Table.cs — comportamento presente anche nella
    /// versione 3.5.1 usata da questo repo). Il Kong di `supabase start` (v. il kong.yml del
    /// container) costruisce l'header Authorization a partire da <c>headers.apikey</c> ma non
    /// consuma/rimuove un <c>apikey</c> arrivato in query: lo passa tale e quale a PostgREST, che lo
    /// legge come un filtro sconosciuto e risponde <c>PGRST100</c> ("failed to parse filter") — SU
    /// OGNI richiesta, Insert/Update/Get comprese, indipendentemente da class_resources. Qui NON
    /// mettiamo "apikey" in <c>ClientOptions.Headers</c> (a differenza di SupabaseService.cs): lo
    /// mandiamo SOLO via <c>GetHeaders</c>, che il client invia comunque come header HTTP vero e
    /// proprio, bypassando il ramo che lo duplica in query.
    /// </summary>
    private static SupabaseClient BuildClientAsUser(string token)
    {
        var auth = new Client(new ClientOptions
        {
            Url = $"{LocalSupabaseFixture.ApiUrl}/auth/v1",
        });
        var postgrest = new Postgrest.Client($"{LocalSupabaseFixture.ApiUrl}/rest/v1", new Postgrest.ClientOptions())
        {
            GetHeaders = () => new Dictionary<string, string>
            {
                { "apikey", LocalSupabaseFixture.AnonKey },
                { "Authorization", $"Bearer {token}" },
            },
        };
        return new SupabaseClient(auth, postgrest);
    }

    private Character NewCharacter(List<ClassResource> risorse) => new()
    {
        OwnerId = _fx.UserAId,
        CampaignId = LocalSupabaseFixture.CampaignC1,
        Name = $"Test scheda-alla-pari {Guid.NewGuid():N}",
        HitPoints = 10,
        MaxHitPoints = 10,
        ClassResources = risorse,
        ArmorTraining = "Armature leggere e medie, scudi",
        WeaponProficiencies = "Armi semplici e da guerra",
        ToolProficiencies = "Strumenti da ladro",
    };

    /// <summary>Confronto per posizione (non solo per insieme): un riordino silenzioso nel viaggio
    /// jsonb->POCO sarebbe comunque un difetto da vedere, non da tollerare.</summary>
    private static void AssertSameResources(IReadOnlyList<ClassResource> attese, IReadOnlyList<ClassResource> effettive)
    {
        Assert.Equal(attese.Count, effettive.Count);
        for (var i = 0; i < attese.Count; i++)
        {
            Assert.Equal(attese[i].Nome, effettive[i].Nome);
            Assert.Equal(attese[i].Max, effettive[i].Max);
            Assert.Equal(attese[i].Spesi, effettive[i].Spesi);
            Assert.Equal(attese[i].Ricarica, effettive[i].Ricarica);
        }
    }

    // --- 1) Due risorse di classe: il salvataggio riesce e torna identico, sia nell'eco dell'Insert
    // sia in una rilettura indipendente (prova vera del viaggio jsonb->POCO, non solo dell'eco). ---

    [SkippableFact]
    public async Task Salva_e_rilegge_due_risorse_di_classe_identiche()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");

        var risorse = new List<ClassResource>
        {
            new() { Nome = "Ira", Max = 3, Spesi = 1, Ricarica = "lungo" },
            new() { Nome = "Secondo fiato", Max = 1, Spesi = 0, Ricarica = "breve" },
        };
        var character = NewCharacter(risorse);

        var insertResponse = await _client.From<Character>().Insert(character);
        var inserted = Assert.Single(insertResponse.Models);
        _characterIdsToClean.Add(inserted.Id);

        Assert.Equal("Armature leggere e medie, scudi", inserted.ArmorTraining);
        Assert.Equal("Armi semplici e da guerra", inserted.WeaponProficiencies);
        Assert.Equal("Strumenti da ladro", inserted.ToolProficiencies);
        AssertSameResources(risorse, inserted.ClassResources);

        var reread = await _client.From<Character>().Where(c => c.Id == inserted.Id).Get();
        var riletto = Assert.Single(reread.Models);
        Assert.Equal("Armature leggere e medie, scudi", riletto.ArmorTraining);
        Assert.Equal("Armi semplici e da guerra", riletto.WeaponProficiencies);
        Assert.Equal("Strumenti da ladro", riletto.ToolProficiencies);
        AssertSameResources(risorse, riletto.ClassResources);
    }

    // --- 3) Lista vuota: deve restare '[]' andata e ritorno, mai null né un errore di scrittura. ---

    [SkippableFact]
    public async Task Salva_e_rilegge_lista_vuota_di_risorse_senza_diventare_null()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");

        var character = NewCharacter(new List<ClassResource>());

        var insertResponse = await _client.From<Character>().Insert(character);
        var inserted = Assert.Single(insertResponse.Models);
        _characterIdsToClean.Add(inserted.Id);

        Assert.NotNull(inserted.ClassResources);
        Assert.Empty(inserted.ClassResources);

        var reread = await _client.From<Character>().Where(c => c.Id == inserted.Id).Get();
        var riletto = Assert.Single(reread.Models);
        Assert.NotNull(riletto.ClassResources);
        Assert.Empty(riletto.ClassResources);
    }

    // --- 4) Inventario: i tre booleani nuovi (is_finesse/is_ranged/is_not_proficient) a true,
    // salvati e riletti. ---

    [SkippableFact]
    public async Task Salva_e_rilegge_un_oggetto_con_i_tre_flag_arma_a_true()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");

        var character = NewCharacter(new List<ClassResource>());
        var characterInsert = await _client.From<Character>().Insert(character);
        var inserted = Assert.Single(characterInsert.Models);
        _characterIdsToClean.Add(inserted.Id);

        var item = new InventoryItem
        {
            CharacterId = inserted.Id,
            Name = "Rapier con tre flag a true",
            IsFinesse = true,
            IsRanged = true,
            IsNotProficient = true,
        };
        var itemInsert = await _client.From<InventoryItem>().Insert(item);
        var insertedItem = Assert.Single(itemInsert.Models);

        Assert.True(insertedItem.IsFinesse);
        Assert.True(insertedItem.IsRanged);
        Assert.True(insertedItem.IsNotProficient);

        var reread = await _client.From<InventoryItem>().Where(i => i.Id == insertedItem.Id).Get();
        var rilettoItem = Assert.Single(reread.Models);
        Assert.True(rilettoItem.IsFinesse);
        Assert.True(rilettoItem.IsRanged);
        Assert.True(rilettoItem.IsNotProficient);
    }

    // --- 5) Il test che vale più di tutti: un Update che tocca SOLO gli hit_points non deve perdere
    // né corrompere class_resources — è lo scenario reale, perché ogni Update di
    // CharacterRepository.UpdateCharacterAsync manda comunque l'intero modello, class_resources
    // incluso, a prescindere da quale campo l'utente abbia davvero cambiato in UI. ---

    [SkippableFact]
    public async Task Aggiornare_un_campo_qualunque_non_perde_le_risorse_di_classe()
    {
        Skip.IfNot(_fx.Available, "Stack Supabase locale non in esecuzione (`supabase start`).");

        var risorseIniziali = new List<ClassResource>
        {
            new() { Nome = "Ispirazione bardica", Max = 4, Spesi = 2, Ricarica = "lungo" },
            new() { Nome = "Focus del monaco", Max = 5, Spesi = 3, Ricarica = "breve" },
        };
        var character = NewCharacter(risorseIniziali);

        var insertResponse = await _client.From<Character>().Insert(character);
        var inserted = Assert.Single(insertResponse.Models);
        _characterIdsToClean.Add(inserted.Id);
        AssertSameResources(risorseIniziali, inserted.ClassResources);

        // Cambia SOLO gli hit_points sull'istanza già tornata dall'Insert: class_resources resta
        // quello che il server ha già confermato, ma viaggia di nuovo nell'Update come tutte le
        // altre colonne del modello.
        inserted.HitPoints = 7;
        var updateResponse = await _client.From<Character>().Update(inserted);
        var updated = Assert.Single(updateResponse.Models);

        Assert.Equal(7, updated.HitPoints);
        AssertSameResources(risorseIniziali, updated.ClassResources);

        // Rilettura indipendente: non fidarsi della sola eco dell'Update.
        var reread = await _client.From<Character>().Where(c => c.Id == inserted.Id).Get();
        var riletto = Assert.Single(reread.Models);
        Assert.Equal(7, riletto.HitPoints);
        AssertSameResources(risorseIniziali, riletto.ClassResources);
    }
}
