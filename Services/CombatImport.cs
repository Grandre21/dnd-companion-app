using System.Text.RegularExpressions;
using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>
/// Logica pura per importare mostri e personaggi come combattenti nel tracker iniziativa.
/// Nessuno stato/I/O: il PF di Combatant è int, mentre Monster.HitPoints è testo libero
/// (es. "45 (6d8+18)") → si estrae il primo intero.
/// </summary>
public static class CombatImport
{
    // Primo intero nel testo PF; fallback 1 (mai < 1). Es. "45 (6d8+18)" -> 45, "" -> 1, "n/a" -> 1.
    public static int ParseLeadingHp(string? hitPointsText)
    {
        if (string.IsNullOrWhiteSpace(hitPointsText)) return 1;
        var match = Regex.Match(hitPointsText, @"\d+");
        if (match.Success && int.TryParse(match.Value, out var n) && n >= 1) return n;
        return 1;
    }

    // q copie di un Combatant dal mostro: nome numerato se q>1, Initiative=0, CurrentHp=MaxHp=ParseLeadingHp.
    // q <= 0 -> sequenza vuota.
    public static IEnumerable<Combatant> FromMonster(Monster monster, int quantity)
        => FromNameAndHp(monster.Name, monster.HitPoints, quantity);

    /// <summary>Come <see cref="FromMonster"/>, ma da una voce di manuale. Serve perché il tracker
    /// attinge a entrambe le sorgenti: un mostro che vive solo nel pacchetto non ha una riga di
    /// database, e non gli serve — <c>Combatant</c> è un POCO dentro il jsonb di
    /// <c>combat_state</c>, senza chiave esterna verso <c>monsters</c>.</summary>
    public static IEnumerable<Combatant> FromPackageMonster(PackageMonster monster, int quantity)
        => FromNameAndHp(monster.Name, monster.HitPoints, quantity);

    private static IEnumerable<Combatant> FromNameAndHp(string name, string? hitPointsText, int quantity)
    {
        var hp = ParseLeadingHp(hitPointsText);
        for (var i = 1; i <= quantity; i++)
        {
            yield return new Combatant
            {
                Name = quantity == 1 ? name : $"{name} {i}",
                Initiative = 0,
                CurrentHp = hp,
                MaxHp = hp,
            };
        }
    }

    /// <summary>Un Combatant dal PG: PF clampati come oggi, ma <c>Initiative</c> parte dal
    /// <b>bonus</b> di Destrezza (<see cref="CharacterCalculations.GetInitiative"/>) invece che da 0.
    /// Non è un tiro — l'app non tira i dadi — solo il punto di partenza: chi tira somma a mente o
    /// corregge il campo, chi non usa l'iniziativa a turni ha comunque un ordine sensato. Se il
    /// bonus è 0 (Destrezza 10, o non valorizzata: il default del modello) il valore resta 0 come oggi.</summary>
    public static Combatant FromCharacter(Character character)
    {
        var maxHp = Math.Max(1, character.MaxHitPoints);
        return new Combatant
        {
            Name = character.Name,
            OwnerId = character.OwnerId,
            Initiative = CharacterCalculations.GetInitiative(character),
            CurrentHp = Math.Clamp(character.HitPoints, 0, maxHp),
            MaxHp = maxHp,
        };
    }
}
