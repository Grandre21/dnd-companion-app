using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>
/// Helper di sole funzioni pure: il bonus d'attacco calcolato per un'arma, secondo le regole
/// 5e 2024. Nessuno stato, nessun side effect, nessuna I/O.
///
/// Non decide mai al posto dell'utente: chi consuma questo valore lo mostra come suggerimento
/// accanto al campo <c>AttackBonus</c> scritto a mano, che resta sovrano quando è valorizzato.
/// Nessun bonus condizionato allo stato del personaggio (privilegi, Ira, ecc.): quella semantica
/// resta nelle note testuali dell'arma.
/// </summary>
public static class WeaponCalculations
{
    /// <summary>
    /// Il modificatore di caratteristica che l'arma usa, senza competenza.
    /// Mischia → Forza; accurata (<see cref="InventoryItem.IsFinesse"/>) → il migliore fra Forza e
    /// Destrezza; a distanza (<see cref="InventoryItem.IsRanged"/>) → Destrezza.
    ///
    /// Caso combinato accurata + a distanza (es. una balestra leggera "accurata" in homebrew):
    /// vince l'accuratezza, cioè si prende il migliore fra Forza e Destrezza. Nella 5e le armi a
    /// distanza standard non sono mai anche accurate, quindi la combinazione è già homebrew; in
    /// quel caso trattare "accurata" come il flag più permissivo (il migliore dei due) è la lettura
    /// che non penalizza mai il giocatore rispetto a quanto la sola proprietà "a distanza" darebbe.
    /// </summary>
    public static int ModificatoreArma(InventoryItem arma, Character pg)
    {
        var forza = CharacterCalculations.GetModifier(pg.Strength);
        var destrezza = CharacterCalculations.GetModifier(pg.Dexterity);

        if (arma.IsFinesse) return Math.Max(forza, destrezza);
        if (arma.IsRanged) return destrezza;
        return forza;
    }

    /// <summary>
    /// Il bonus d'attacco che l'app calcola per quest'arma su questo personaggio:
    /// <see cref="ModificatoreArma"/> più il bonus di competenza
    /// (<see cref="CharacterCalculations.GetProficiencyBonus"/>), salvo che l'arma sia segnata
    /// come non competente (<see cref="InventoryItem.IsNotProficient"/>).
    /// </summary>
    public static int BonusAttacco(InventoryItem arma, Character pg)
    {
        var modificatore = ModificatoreArma(arma, pg);
        var competenza = arma.IsNotProficient ? 0 : CharacterCalculations.GetProficiencyBonus(pg.Level);
        return modificatore + competenza;
    }
}
