using System.Text.Json.Serialization;

namespace DndCompanion.Models.Packages;

/// <summary>Pacchetto di dati importabile/esportabile (§5 dello spec). POCO di sola
/// deserializzazione: non sono Model Postgrest e non hanno attributi di tabella.</summary>
public sealed class CatalogPackage
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("edition")] public string Edition { get; set; } = string.Empty;
    [JsonPropertyName("language")] public string Language { get; set; } = string.Empty;
    [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
    [JsonPropertyName("license")] public PackageLicense? License { get; set; }
    [JsonPropertyName("species")] public List<PackageSpecies> Species { get; set; } = new();
    [JsonPropertyName("backgrounds")] public List<PackageBackground> Backgrounds { get; set; } = new();
    [JsonPropertyName("feats")] public List<PackageFeat> Feats { get; set; } = new();
    [JsonPropertyName("classes")] public List<PackageClass> Classes { get; set; } = new();
    [JsonPropertyName("spells")] public List<PackageSpell> Spells { get; set; } = new();
    [JsonPropertyName("monsters")] public List<PackageMonster> Monsters { get; set; } = new();
}

public sealed class PackageLicense
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("attribution")] public string Attribution { get; set; } = string.Empty;
}

public sealed class PackageSpeed
{
    [JsonPropertyName("value")] public int Value { get; set; }
    /// <summary>"m" o "ft" (§4.5). Il pacchetto SRD distribuito con l'app usa i <b>piedi</b>: il
    /// Golia sta a 35 ft, cioè 10,5 m, e <see cref="Value"/> è un <c>int</c> — un decimale farebbe
    /// fallire la deserializzazione dell'intero pacchetto, non della singola voce (v. la nota
    /// datata in §4.5 dello spec). Il default resta "m" per non cambiare come vengono letti i
    /// pacchetti di terzi già scritti senza questo campo; il riconoscimento vero passa comunque da
    /// <c>PackageRowMerge.UnitaValida</c>.</summary>
    [JsonPropertyName("unit")] public string Unit { get; set; } = "m";
}

public sealed class PackageSpecies
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("size")] public string Size { get; set; } = string.Empty;
    [JsonPropertyName("speed")] public PackageSpeed? Speed { get; set; }
    [JsonPropertyName("traits")] public string Traits { get; set; } = string.Empty;
}

public sealed class PackageBackground
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    /// <summary>Le TRE caratteristiche su cui il background concede i bonus. La ripartizione
    /// (+2/+1 oppure +1/+1/+1) la sceglie il giocatore, non il background (§4.2).</summary>
    [JsonPropertyName("abilityScores")] public List<string> AbilityScores { get; set; } = new();
    [JsonPropertyName("originFeat")] public string OriginFeat { get; set; } = string.Empty;
    [JsonPropertyName("skillProficiencies")] public List<string> SkillProficiencies { get; set; } = new();
    [JsonPropertyName("toolProficiency")] public string ToolProficiency { get; set; } = string.Empty;
    [JsonPropertyName("equipment")] public string Equipment { get; set; } = string.Empty;
}

/// <summary>Talento. Solo consultazione: non ha tabella e non è importabile (§5).</summary>
public sealed class PackageFeat
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
}

public sealed class PackageSkillChoices
{
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("from")] public List<string> From { get; set; } = new();
}

public sealed class PackageClassLevel
{
    [JsonPropertyName("level")] public int Level { get; set; }
    [JsonPropertyName("features")] public List<string> Features { get; set; } = new();
    /// <summary>Nove slot, dal livello 1 al 9.</summary>
    [JsonPropertyName("spellSlots")] public List<int> SpellSlots { get; set; } = new();
}

/// <summary>Una sottoclasse: il nome che il personaggio sceglie al 3° livello, il testo delle sue
/// regole e i livelli in cui gli dà qualcosa.
///
/// Sezione <b>annidata</b> dentro la classe e non di primo livello, per una ragione di
/// compatibilità: il campo è nuovo e i client già installati leggono lo stesso file: ignorano ciò
/// che non conoscono (<c>JsonSerializerOptions</c> non ha <c>UnmappedMemberHandling.Disallow</c>),
/// mentre un incremento di <c>schemaVersion</c> glielo farebbe rifiutare per intero.</summary>
public sealed class PackageSubclass
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("levels")] public List<PackageClassLevel> Levels { get; set; } = new();
}

public sealed class PackageClass
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("hitDie")] public string HitDie { get; set; } = string.Empty;
    [JsonPropertyName("primaryAbility")] public string PrimaryAbility { get; set; } = string.Empty;
    [JsonPropertyName("savingThrows")] public List<string> SavingThrows { get; set; } = new();
    [JsonPropertyName("skillChoices")] public PackageSkillChoices? SkillChoices { get; set; }
    [JsonPropertyName("levels")] public List<PackageClassLevel> Levels { get; set; } = new();

    /// <summary>Le sottoclassi della classe. Lo SRD ne concede una ciascuna; un file può dichiararne
    /// quante vuole — nessun limite di numero. Dal 2026-08-01 l'import <b>le scrive</b> nella colonna
    /// <c>classes.subclasses</c>, nel formato di
    /// <see cref="T:DndCompanion.Services.SubclassText"/>: prima le leggeva e le buttava, perché la
    /// tabella non aveva dove tenerle.</summary>
    [JsonPropertyName("subclasses")] public List<PackageSubclass> Subclasses { get; set; } = new();
}

public sealed class PackageSpell
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("level")] public int? Level { get; set; }
    [JsonPropertyName("school")] public string School { get; set; } = string.Empty;
    [JsonPropertyName("castingTime")] public string CastingTime { get; set; } = string.Empty;
    [JsonPropertyName("range")] public string Range { get; set; } = string.Empty;
    [JsonPropertyName("components")] public string Components { get; set; } = string.Empty;
    [JsonPropertyName("duration")] public string Duration { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("classes")] public List<string> Classes { get; set; } = new();
}

public sealed class PackageMonster
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("challengeRating")] public string ChallengeRating { get; set; } = string.Empty;
    [JsonPropertyName("armorClass")] public int? ArmorClass { get; set; }
    [JsonPropertyName("hitPoints")] public string HitPoints { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
}
