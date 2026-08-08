using DndCompanion.Models;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test di <see cref="CharacterFeatureDensity"/> — con quanto risalto rendere un privilegio nella
/// vista di gioco. L'ordine dei quattro casi di <see cref="CharacterFeatureDensity.Classifica"/> è
/// la specifica: qui si copre anche la PRECEDENZA fra i casi, non solo l'esito di ciascuno da solo.
/// </summary>
public class CharacterFeatureDensityTests
{
    /// <summary>Voce di base "neutra": nessun contatore, non attivabile, nota vuota — di suo
    /// cadrebbe nel caso 4 (Riga). I singoli test la sovrascrivono per entrare negli altri casi.</summary>
    private static VistaPrivilegio Voce(
        string nota = "",
        ClassResource? contatore = null,
        bool attivabile = false,
        bool notaDiCatalogo = false) =>
        new("Ira", nota, "bonus", "classe", contatore, attivabile, 1, null, notaDiCatalogo);

    private static ClassResource Contatore(int max, int spesi) =>
        new() { Nome = "Ira", Max = max, Spesi = spesi };

    // -----------------------------------------------------------------------------------
    // I quattro casi, da soli
    // -----------------------------------------------------------------------------------

    /// <summary>Caso 1: la nota di una voce attiva è già mostrata per intero dalla strip ATTIVO in
    /// cima allo schermo — ripeterla nella lista è duplicazione.</summary>
    [Fact]
    public void Classifica_VoceAttiva_DaRiga()
    {
        var voce = Voce(nota: "testo lungo", attivabile: true);

        Assert.Equal(DensitaPrivilegio.Riga, CharacterFeatureDensity.Classifica(voce, attiva: true));
    }

    /// <summary>Caso 2: contatore esaurito. Ira 0/3 non è usabile fino al riposo lungo — rumore
    /// certificato dal dato.</summary>
    [Fact]
    public void Classifica_ContatoreEsaurito_DaSpenta()
    {
        var voce = Voce(contatore: Contatore(max: 3, spesi: 3));

        Assert.Equal(DensitaPrivilegio.Spenta, CharacterFeatureDensity.Classifica(voce, attiva: false));
    }

    /// <summary>Guardia sul "Max &gt; 0" del caso 2: un contatore con Max 0 (mai inizializzato)
    /// soddisferebbe "Spesi &gt;= Max" (0 &gt;= 0) e apparirebbe erroneamente esaurito. Il Max &gt; 0
    /// evita di trattare un contatore senza dati come un contatore vuoto — cade nel caso 3
    /// ("ha un contatore") e resta Piena.</summary>
    [Fact]
    public void Classifica_ContatoreConMaxZero_NonEDaSpenta()
    {
        var voce = Voce(contatore: Contatore(max: 0, spesi: 0));

        Assert.NotEqual(DensitaPrivilegio.Spenta, CharacterFeatureDensity.Classifica(voce, attiva: false));
        Assert.Equal(DensitaPrivilegio.Piena, CharacterFeatureDensity.Classifica(voce, attiva: false));
    }

    [Fact]
    public void Classifica_ContatoreApertoENonEsaurito_DaPiena()
    {
        var voce = Voce(contatore: Contatore(max: 3, spesi: 1));

        Assert.Equal(DensitaPrivilegio.Piena, CharacterFeatureDensity.Classifica(voce, attiva: false));
    }

    [Fact]
    public void Classifica_Attivabile_SenzaContatoreNeNota_DaPiena()
    {
        var voce = Voce(attivabile: true);

        Assert.Equal(DensitaPrivilegio.Piena, CharacterFeatureDensity.Classifica(voce, attiva: false));
    }

    [Fact]
    public void Classifica_NotaScrittaDallUtente_DaPiena()
    {
        var voce = Voce(nota: "3 volte al giorno, come mi serve", notaDiCatalogo: false);

        Assert.Equal(DensitaPrivilegio.Piena, CharacterFeatureDensity.Classifica(voce, attiva: false));
    }

    /// <summary>Il criterio non è "nota non vuota", è "chi l'ha scritta": una nota di catalogo lunga
    /// quanto si vuole resta a un tocco di distanza, non in prima vista.</summary>
    [Fact]
    public void Classifica_NotaDiCatalogo_SenzaContatoreNeAttivabile_DaRiga()
    {
        var voce = Voce(nota: "Descrizione ufficiale lunga del talento", notaDiCatalogo: true);

        Assert.Equal(DensitaPrivilegio.Riga, CharacterFeatureDensity.Classifica(voce, attiva: false));
    }

    [Fact]
    public void Classifica_NienteDiTutto_DaRiga()
    {
        var voce = Voce();

        Assert.Equal(DensitaPrivilegio.Riga, CharacterFeatureDensity.Classifica(voce, attiva: false));
    }

    // -----------------------------------------------------------------------------------
    // Precedenza fra i casi — l'unica cosa che l'ORDINE decide (v. brief)
    // -----------------------------------------------------------------------------------

    /// <summary>Precedenza 1 prima di 2: una voce attiva CON un contatore esaurito deve dare Riga,
    /// non Spenta. Se il caso 2 venisse controllato per primo, l'esaurimento vincerebbe e questo
    /// test diventerebbe rosso.</summary>
    [Fact]
    public void Classifica_AttivaEConContatoreEsaurito_VinceAttiva_DaRiga()
    {
        var voce = Voce(contatore: Contatore(max: 3, spesi: 3));

        Assert.Equal(DensitaPrivilegio.Riga, CharacterFeatureDensity.Classifica(voce, attiva: true));
    }

    /// <summary>Precedenza 2 prima di 3: una voce con contatore esaurito E una nota scritta
    /// dall'utente deve dare Spenta, non Piena. È il caso che il collaudo per mutazione (scambiare i
    /// casi 2 e 3) deve far diventare rosso.</summary>
    [Fact]
    public void Classifica_ContatoreEsaurito_ConNotaDellUtente_VinceEsaurito_DaSpenta()
    {
        var voce = Voce(nota: "testo scritto da me", notaDiCatalogo: false, contatore: Contatore(max: 3, spesi: 3));

        Assert.Equal(DensitaPrivilegio.Spenta, CharacterFeatureDensity.Classifica(voce, attiva: false));
    }
}
