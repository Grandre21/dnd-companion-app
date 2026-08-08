using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test dei due helper puri `internal static` di <see cref="ActiveEffectsService"/> —
/// <c>ContieneNome</c> e <c>ToggleNome</c>, la parte del servizio testabile senza IJSRuntime.
/// </summary>
public class ActiveEffectsServiceTests
{
    // -----------------------------------------------------------------------------------
    // ContieneNome
    // -----------------------------------------------------------------------------------

    /// <summary>«Ira» e «IRA» sono lo stesso privilegio: è il difetto invisibile che questo
    /// helper esiste per evitare (v. il commento XML del servizio).</summary>
    [Fact]
    public void ContieneNome_IgnoraMaiuscoleENelNomeCercato()
    {
        var attivi = new[] { "Ira" };

        Assert.True(ActiveEffectsService.ContieneNome(attivi, "IRA"));
        Assert.True(ActiveEffectsService.ContieneNome(attivi, "ira"));
    }

    [Fact]
    public void ContieneNome_CollezioneNull_TornaFalse()
        => Assert.False(ActiveEffectsService.ContieneNome(null, "Ira"));

    [Fact]
    public void ContieneNome_CollezioneVuota_TornaFalse()
        => Assert.False(ActiveEffectsService.ContieneNome(Array.Empty<string>(), "Ira"));

    [Fact]
    public void ContieneNome_NomeAssente_TornaFalse()
        => Assert.False(ActiveEffectsService.ContieneNome(new[] { "Ira" }, "Ispirazione bardica"));

    // -----------------------------------------------------------------------------------
    // ToggleNome
    // -----------------------------------------------------------------------------------

    [Fact]
    public void ToggleNome_CollezioneNull_AccendeIlPrivilegio()
    {
        var risultato = ActiveEffectsService.ToggleNome(null, "Ira");

        Assert.Single(risultato);
        Assert.Equal("Ira", risultato.Single());
    }

    [Fact]
    public void ToggleNome_AccendeEPoiSpegne()
    {
        var acceso = ActiveEffectsService.ToggleNome(null, "Ira");
        Assert.Single(acceso);

        var spento = ActiveEffectsService.ToggleNome(acceso, "Ira");
        Assert.Empty(spento);
    }

    /// <summary>Il toggle riconosce che «IRA» è già acceso come «Ira» (confronto normalizzato) e lo
    /// spegne, invece di aggiungere una seconda voce duplicata.</summary>
    [Fact]
    public void ToggleNome_SpegneUnPrivilegioGiaAccesoConMaiuscoleDiverse()
    {
        var acceso = ActiveEffectsService.ToggleNome(null, "Ira");

        var spento = ActiveEffectsService.ToggleNome(acceso, "IRA");

        Assert.Empty(spento);
    }

    /// <summary>Il nome resta nella forma con cui è stato salvato la PRIMA volta, non in quella
    /// (magari diversa) con cui arriva il toggle successivo: altrimenti l'utente vedrebbe il
    /// privilegio ridisegnato con un'altra grafia dopo un giro di accensione/spegnimento parziale
    /// su un'altra voce.</summary>
    [Fact]
    public void ToggleNome_ConservaLaFormaOriginaleDelNomeGiaPresente_NonQuellaDelToggleSuccessivo()
    {
        var acceso = ActiveEffectsService.ToggleNome(null, "Ira");
        // Accendo un secondo privilegio: la lista ora ha due voci, e il toggle su "IRA" (maiuscolo)
        // deve rimuovere la voce "Ira" già presente, non alterarne la grafia né toccare l'altra.
        var conSeconda = ActiveEffectsService.ToggleNome(acceso, "Ispirazione bardica");

        var risultato = ActiveEffectsService.ToggleNome(conSeconda, "Ispirazione BARDICA");

        Assert.Single(risultato);
        Assert.Equal("Ira", risultato.Single());
    }

    [Fact]
    public void ToggleNome_NonMutaLaCollezioneOriginale()
    {
        var originale = new List<string> { "Ira" };

        var risultato = ActiveEffectsService.ToggleNome(originale, "Ira");

        Assert.Single(originale); // la lista passata come input non è stata svuotata in place
        Assert.Empty(risultato);
    }
}
