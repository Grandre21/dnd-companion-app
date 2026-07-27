using DndCompanion.Models;
using DndCompanion.Models.Packages;

namespace DndCompanion.Services;

/// <summary>Conversione fra le voci del formato di scambio e le righe di catalogo, nelle due
/// direzioni che l'import usa: creare una riga nuova, oppure applicare una voce **sopra** una riga
/// esistente. Logica pura.
///
/// La regola che questa classe custodisce, e che nessuna verifica manuale può controllare — il
/// conteggio delle righe non cambia — è: un aggiornamento **non deve mai** toccare l'identità
/// (<c>Id</c>, <c>CampaignId</c>, <c>CreatedAt</c>), la proprietà (<c>AddedBy</c>) né le colonne che
/// il formato non trasporta. E un campo **assente** nel file non è un campo **vuoto**: il parser
/// valida solo id e nome, quindi le voci minimali sono legittime e non devono svuotare nulla.</summary>
public static class PackageRowMerge
{
    /// <summary>L'unica unità che il database accetta: <c>races_speed_unit_check</c> ammette solo
    /// 'm' e 'ft'. Un file scritto a mano con "metri" o "M" farebbe fallire con 400 l'intera
    /// sezione, e la stessa cosa accadrebbe alla copia creata da "duplica e modifica" — per questo
    /// è <b>public</b>: la usano sia l'import sia le pagine di catalogo, e riscriverla a mano in un
    /// `.razor` la farebbe divergere.
    ///
    /// Le forme estese vanno riconosciute invece che finire nel fallback: "feet" letto come metri
    /// trasformerebbe una velocità di 30 piedi in 30 metri — un dato sbagliato e silenzioso, mentre
    /// prima il CHECK almeno lo respingeva con un errore visibile.</summary>
    public static string UnitaValida(string? unit)
    {
        var u = unit?.Trim() ?? string.Empty;
        return u.Equals("ft", StringComparison.OrdinalIgnoreCase)
            || u.Equals("feet", StringComparison.OrdinalIgnoreCase)
            || u.Equals("foot", StringComparison.OrdinalIgnoreCase)
            || u.Equals("piedi", StringComparison.OrdinalIgnoreCase)
                ? "ft"
                : "m";
    }

    private static string Unisci(List<string> values) => string.Join(", ", values);

    // ---- Creazione: righe nuove, Id lasciato al database ----

    public static Race NuovaSpecie(PackageSpecies p, string campaignId, string? userId) => new()
    {
        Name = p.Name,
        Description = p.Description,
        Speed = p.Speed?.Value ?? 9,
        SpeedUnit = UnitaValida(p.Speed?.Unit),
        Traits = p.Traits,
        SourceId = p.Id,
        CampaignId = campaignId,
        AddedBy = userId,
    };

    public static CharacterClass NuovaClasse(PackageClass p, string campaignId, string? userId) => new()
    {
        Name = p.Name,
        HitDie = p.HitDie,
        PrimaryAbility = p.PrimaryAbility,
        SavingThrows = Unisci(p.SavingThrows),
        SkillChoices = DescriviScelte(p.SkillChoices) ?? string.Empty,
        SourceId = p.Id,
        CampaignId = campaignId,
        AddedBy = userId,
    };

    public static Background NuovoBackground(PackageBackground p, string campaignId, string? userId) => new()
    {
        Name = p.Name,
        Description = p.Description,
        AbilityScores = Unisci(p.AbilityScores),
        OriginFeat = p.OriginFeat,
        SkillProficiencies = Unisci(p.SkillProficiencies),
        ToolProficiency = p.ToolProficiency,
        Equipment = p.Equipment,
        SourceId = p.Id,
        CampaignId = campaignId,
        AddedBy = userId,
    };

    public static Spell NuovoIncantesimo(PackageSpell p, string campaignId, string? userId) => new()
    {
        Name = p.Name,
        Level = p.Level ?? 0,
        School = p.School,
        CastingTime = p.CastingTime,
        Range = p.Range,
        Components = p.Components,
        Duration = p.Duration,
        Description = p.Description,
        Classes = Unisci(p.Classes),
        SourceId = p.Id,
        CampaignId = campaignId,
        AddedBy = userId,
    };

    public static Monster NuovoMostro(PackageMonster p, string campaignId, string? userId) => new()
    {
        Name = p.Name,
        ChallengeRating = p.ChallengeRating,
        ArmorClass = p.ArmorClass ?? 10,
        HitPoints = p.HitPoints,
        Description = p.Description,
        SourceId = p.Id,
        CampaignId = campaignId,
        AddedBy = userId,
    };

    // ---- Aggiornamento: solo i campi che il file porta davvero ----

    public static void ApplicaSpecie(PackageSpecies p, Race r)
    {
        Scrivi(p.Name, v => r.Name = v);
        Scrivi(p.Description, v => r.Description = v);
        Scrivi(p.Traits, v => r.Traits = v);
        if (p.Speed is not null)
        {
            r.Speed = p.Speed.Value;
            r.SpeedUnit = UnitaValida(p.Speed.Unit);
        }
        // r.Languages e i sei bonus di caratteristica restano: il formato non li trasporta.
    }

    public static void ApplicaClasse(PackageClass p, CharacterClass c)
    {
        Scrivi(p.Name, v => c.Name = v);
        Scrivi(p.HitDie, v => c.HitDie = v);
        Scrivi(p.PrimaryAbility, v => c.PrimaryAbility = v);
        Scrivi(Unisci(p.SavingThrows), v => c.SavingThrows = v);
        Scrivi(DescriviScelte(p.SkillChoices), v => c.SkillChoices = v);
        // c.Description, c.Features, c.ArmorProficiencies, c.WeaponProficiencies restano.
    }

    public static void ApplicaBackground(PackageBackground p, Background b)
    {
        Scrivi(p.Name, v => b.Name = v);
        Scrivi(p.Description, v => b.Description = v);
        Scrivi(Unisci(p.AbilityScores), v => b.AbilityScores = v);
        Scrivi(p.OriginFeat, v => b.OriginFeat = v);
        Scrivi(Unisci(p.SkillProficiencies), v => b.SkillProficiencies = v);
        Scrivi(p.ToolProficiency, v => b.ToolProficiency = v);
        Scrivi(p.Equipment, v => b.Equipment = v);
    }

    public static void ApplicaIncantesimo(PackageSpell p, Spell s)
    {
        Scrivi(p.Name, v => s.Name = v);
        // Il livello si applica SOLO se il file lo dichiara: con `int` un campo assente valeva 0,
        // e 0 è un livello legittimo — un incantesimo di livello 3 diventava un trucchetto.
        if (p.Level is not null) s.Level = p.Level.Value;
        Scrivi(p.School, v => s.School = v);
        Scrivi(p.CastingTime, v => s.CastingTime = v);
        Scrivi(p.Range, v => s.Range = v);
        Scrivi(p.Components, v => s.Components = v);
        Scrivi(p.Duration, v => s.Duration = v);
        Scrivi(p.Description, v => s.Description = v);
        Scrivi(Unisci(p.Classes), v => s.Classes = v);
    }

    public static void ApplicaMostro(PackageMonster p, Monster m)
    {
        Scrivi(p.Name, v => m.Name = v);
        Scrivi(p.ChallengeRating, v => m.ChallengeRating = v);
        if (p.ArmorClass is not null) m.ArmorClass = p.ArmorClass.Value;
        Scrivi(p.HitPoints, v => m.HitPoints = v);
        Scrivi(p.Description, v => m.Description = v);
        // Le sei caratteristiche, size, type, alignment, speed e abilities restano: azzerarle
        // riporterebbe a 10 le statistiche di un mostro completato a mano.
    }

    // Un campo vuoto nel file significa "non lo so", non "cancellalo": il parser valida solo id e
    // nome, quindi una voce minimale è legittima e non deve svuotare una riga già compilata.
    private static void Scrivi(string? value, Action<string> assegna)
    {
        if (!string.IsNullOrWhiteSpace(value)) assegna(value);
    }

    /// <summary>Unica formulazione della descrizione delle scelte di abilità: la usano creazione,
    /// aggiornamento e "duplica e modifica" (Task 6), e tenerle separate le farebbe divergere il
    /// giorno in cui il formato cambia. Per questo è <b>public</b>.
    ///
    /// Restituisce <c>null</c> quando il file non dichiara le scelte: è ciò che permette a
    /// <c>ApplicaClasse</c> di non svuotare una colonna già compilata.</summary>
    public static string? DescriviScelte(PackageSkillChoices? choices)
        => choices is null ? null : $"{choices.Count} fra: {Unisci(choices.From)}";
}
