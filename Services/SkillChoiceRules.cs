using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Il vincolo di scelta di una classe: quante abilità e fra quali.</summary>
public sealed record VincoloAbilita(int Quante, IReadOnlyList<SkillType> Fra);

/// <summary>L'esito della validazione. <see cref="Sovrapposte"/> sono le scelte che il background
/// concede già (spreco da segnalare, non da vietare). <see cref="Messaggio"/> è <c>null</c> se non
/// c'è nulla da dire.</summary>
public sealed record EsitoScelteAbilita(
    bool Completa,
    IReadOnlyList<SkillType> Sovrapposte,
    string? Messaggio);

/// <summary>
/// Vincolo "scegli N abilità fra queste M" di una classe, e la sua validazione contro le scelte del
/// giocatore. Helper puro <c>static</c>.
/// </summary>
public static class SkillChoiceRules
{
    /// <summary>Il vincolo dal dato strutturato di una classe di pacchetto.
    ///
    /// <b>Il degrado è totale, mai parziale.</b> Torna <c>null</c> quando l'argomento è
    /// <c>null</c>, quando <see cref="PackageSkillChoices.Count"/> non è positivo, quando
    /// <see cref="PackageSkillChoices.From"/> è vuoto, o quando <b>anche un solo nome</b> della
    /// lista non mappa su una <see cref="SkillType"/> via <see cref="SkillCatalog.DaNome"/>.
    ///
    /// Il perché: le 18 coppie di bool di <c>Character</c> non sanno rappresentare un'abilità
    /// homebrew. Un vincolo che ne elencasse una la renderebbe irraggiungibile — il giocatore
    /// vedrebbe un'opzione che non può selezionare, o peggio la selezionerebbe senza che nulla
    /// venga scritto sulla scheda. Meglio nessun vincolo (picker libero a 18 caselle, sempre
    /// <c>Completa</c> in <see cref="Valida"/>) che un vincolo bugiardo: il vincolo vale intero o
    /// non vale.
    ///
    /// Quarto caso di degrado totale, accanto ai tre già elencati sopra: <c>Count</c> più grande di
    /// quante voci restano dopo la deduplica (es. <c>{count: 3, from: ["Arcano", "Storia"]}</c>)
    /// produrrebbe un <see cref="VincoloAbilita"/> che <see cref="Valida"/> non potrà MAI dichiarare
    /// completo — un vincolo insoddisfacibile è anche più bugiardo di uno assente: il giocatore
    /// resterebbe bloccato per sempre su "Scegline ancora N" senza nulla da spuntare (SERIO 2 del
    /// gate del 2026-08-06). Il formato "N fra: …" di <c>CharacterClass.SkillChoices</c> è testo
    /// libero e invertibile con qualunque N (v. <c>PackageRowMerge.LeggiScelte</c>): non è ipotetico.</summary>
    public static VincoloAbilita? DaPacchetto(PackageSkillChoices? scelte)
    {
        if (scelte is null) return null;
        if (scelte.Count <= 0) return null;
        if (scelte.From is null || scelte.From.Count == 0) return null;

        var fra = new List<SkillType>();
        var viste = new HashSet<SkillType>();
        foreach (var nome in scelte.From)
        {
            var skill = SkillCatalog.DaNome(nome);
            if (skill is null) return null; // un solo nome non riconosciuto invalida tutto il vincolo
            if (viste.Add(skill.Value)) fra.Add(skill.Value);
        }

        if (scelte.Count > fra.Count) return null; // vincolo insoddisfacibile: "scegline 3 fra 2"

        return new VincoloAbilita(scelte.Count, fra);
    }

    /// <summary>Il vincolo dal testo libero di una classe di campagna
    /// (<c>CharacterClass.SkillChoices</c>). Non un secondo parser: passa da
    /// <see cref="PackageRowMerge.LeggiScelte"/>, che già inverte il formato canonico
    /// <c>"2 fra: Arcano, Storia"</c> e le sue varianti scritte a mano, e torna <c>null</c> sulla
    /// prosa che non riconosce — caso che <see cref="DaPacchetto"/> propaga tale e quale.</summary>
    public static VincoloAbilita? DaTesto(string? testoLibero)
        => DaPacchetto(PackageRowMerge.LeggiScelte(testoLibero));

    /// <summary>Valida le scelte contro il vincolo. Con vincolo <c>null</c> (nessun vincolo
    /// disponibile) è sempre <see cref="EsitoScelteAbilita.Completa"/>: il picker libero non blocca
    /// mai. Argomenti <c>null</c> in <paramref name="scelte"/>/<paramref name="concesse"/> trattati
    /// come liste vuote, mai eccezioni.</summary>
    public static EsitoScelteAbilita Valida(
        VincoloAbilita? vincolo,
        IReadOnlyList<SkillType>? scelte,
        IReadOnlyList<SkillType>? concesse)
    {
        var sceltEffettive = scelte ?? Array.Empty<SkillType>();
        var concesseEffettive = concesse ?? Array.Empty<SkillType>();

        var sovrapposte = sceltEffettive
            .Where(concesseEffettive.Contains)
            .Distinct()
            .ToList();

        if (vincolo is null)
        {
            return new EsitoScelteAbilita(true, sovrapposte, MessaggioSovrapposizione(sovrapposte));
        }

        // I duplicati non contano doppio: Sovrapposte (sopra) usa già .Distinct(), e Completa deve
        // seguire la stessa regola, non un conteggio diverso nello stesso metodo (MINORE 5 del gate
        // del 2026-08-06) — altrimenti [Arcana, Arcana] risulterebbe completo su un vincolo da 2.
        var distinte = sceltEffettive.Distinct().ToList();

        var fuoriElenco = distinte.Any(s => !vincolo.Fra.Contains(s));
        var completa = !fuoriElenco && distinte.Count == vincolo.Quante;

        string? messaggio;
        if (fuoriElenco)
            messaggio = "Una o più scelte non fanno parte dell'elenco consentito dalla classe.";
        else if (distinte.Count < vincolo.Quante)
            messaggio = $"Scegline ancora {vincolo.Quante - distinte.Count}.";
        else if (distinte.Count > vincolo.Quante)
            messaggio = $"Ne hai scelte {distinte.Count} su {vincolo.Quante}.";
        else
            messaggio = MessaggioSovrapposizione(sovrapposte);

        return new EsitoScelteAbilita(completa, sovrapposte, messaggio);
    }

    private static string? MessaggioSovrapposizione(IReadOnlyList<SkillType> sovrapposte)
    {
        if (sovrapposte.Count == 0) return null;

        var nomi = string.Join(", ", sovrapposte.Select(SkillCatalog.Nome));
        return sovrapposte.Count == 1
            ? $"{nomi} te la dà già il background: sceglierla è uno spreco."
            : $"{nomi} te le dà già il background: sceglierle è uno spreco.";
    }
}
