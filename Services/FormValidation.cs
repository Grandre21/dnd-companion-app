using DndCompanion.Models;

namespace DndCompanion.Services;

// Validazione di dominio lato client per i form (UX, non sicurezza: l'autorità resta RLS).
// Ogni Validate* ritorna il PRIMO messaggio d'errore, o null se i dati sono validi.
internal static class FormValidation
{
    internal static bool InRange(int value, int min, int max) => value >= min && value <= max;

    internal static string? ValidateMonster(Monster m)
    {
        if (string.IsNullOrWhiteSpace(m.Name)) return "Il nome è obbligatorio";
        if (!InRange(m.ArmorClass, 0, 40)) return "La CA deve essere tra 0 e 40";

        var stats = new (string Label, int Value)[]
        {
            ("Forza", m.Strength), ("Destrezza", m.Dexterity), ("Costituzione", m.Constitution),
            ("Intelligenza", m.Intelligence), ("Saggezza", m.Wisdom), ("Carisma", m.Charisma)
        };
        foreach (var (label, value) in stats)
            if (!InRange(value, 1, 30)) return $"{label}: il punteggio deve essere tra 1 e 30";

        return null;
    }

    internal static string? ValidateRace(Race r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Il nome è obbligatorio";
        if (!InRange(r.Speed, 0, 120)) return "La velocità deve essere tra 0 e 120";
        return null;
    }
}
