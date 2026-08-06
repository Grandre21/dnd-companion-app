using DndCompanion.Models;

namespace DndCompanion.Services;

/// <summary>
/// Fonte unica della corrispondenza <see cref="SkillType"/> ↔ nome italiano ↔ proprietà bool su
/// <see cref="Character"/>. Helper puro <c>static</c>: nessuno stato, nessuna I/O.
///
/// I nomi qui dichiarati coincidono <b>esattamente</b> (a meno di maiuscole/accenti) con quelli
/// usati dal pacchetto SRD <c>wwwroot/data/srd-2024-it.json</c> nel campo <c>skillChoices.from</c>
/// di ogni classe e <c>skillProficiencies</c> di ogni background: è il perno che tiene insieme il
/// vincolo di scelta (<see cref="SkillChoiceRules"/>) e le 18 coppie di bool del personaggio.
/// <see cref="Tests.SkillCatalogTests"/> verifica la coincidenza contro il pacchetto vero, non a
/// campione.
/// </summary>
public static class SkillCatalog
{
    /// <summary>Le 18 abilità nell'ordine in cui l'enum le dichiara. Letta via <c>Enum.GetValues</c>
    /// e non scritta a mano: così una skill aggiunta domani entra qui senza bisogno di ricordarsi
    /// di aggiornare anche questa lista.</summary>
    public static IReadOnlyList<SkillType> Tutte { get; } =
        Array.AsReadOnly((SkillType[])Enum.GetValues(typeof(SkillType)));

    private static readonly Dictionary<SkillType, string> NomiItaliani = new()
    {
        [SkillType.Athletics] = "Atletica",
        [SkillType.Acrobatics] = "Acrobazia",
        [SkillType.SleightOfHand] = "Rapidità di mano",
        [SkillType.Stealth] = "Furtività",
        [SkillType.Arcana] = "Arcano",
        [SkillType.History] = "Storia",
        [SkillType.Investigation] = "Indagare",
        [SkillType.Nature] = "Natura",
        [SkillType.Religion] = "Religione",
        [SkillType.AnimalHandling] = "Addestrare animali",
        [SkillType.Insight] = "Intuizione",
        [SkillType.Medicine] = "Medicina",
        [SkillType.Perception] = "Percezione",
        [SkillType.Survival] = "Sopravvivenza",
        [SkillType.Deception] = "Inganno",
        [SkillType.Intimidation] = "Intimidire",
        [SkillType.Performance] = "Intrattenere",
        [SkillType.Persuasion] = "Persuasione",
    };

    // Chiave di ricerca inversa, costruita una sola volta: la normalizzazione è quella di
    // CatalogKey.NormalizeName, NON string.Normalize(NormalizationForm.FormD). Il progetto compila
    // con InvariantGlobalization=true (v. DndCompanion.csproj), e sotto quel flag String.Normalize
    // non decompone nulla e non solleva eccezioni: è un no-op silenzioso (v. il commento in
    // CatalogKey.cs e CatalogKeyTests). Usare FormD qui riprodurrebbe esattamente il difetto che
    // quella classe esiste per evitare — accenti "riconosciuti" in dev con ICU pieno e ignorati nel
    // bundle pubblicato, perché il test gira sotto lo stesso flag della produzione.
    private static readonly Dictionary<string, SkillType> PerNomeNormalizzato =
        NomiItaliani.ToDictionary(kv => ChiaveDiRicerca(kv.Value), kv => kv.Key);

    /// <summary>Nome italiano ufficiale ("Atletica", "Rapidità di mano", "Addestrare animali").</summary>
    public static string Nome(SkillType abilita)
        => NomiItaliani.TryGetValue(abilita, out var nome) ? nome : abilita.ToString();

    /// <summary>Inverso di <see cref="Nome"/>. Tollerante: ignora maiuscole, spazi in eccesso e
    /// accenti. <c>null</c> se il nome non è riconosciuto.</summary>
    public static SkillType? DaNome(string? nome)
    {
        var chiave = ChiaveDiRicerca(nome);
        if (chiave.Length == 0) return null;
        return PerNomeNormalizzato.TryGetValue(chiave, out var skill) ? skill : null;
    }

    /// <summary>Le abilità riconosciute in un testo separato da virgole ("Atletica, Sopravvivenza").
    /// Voci non riconosciute scartate in silenzio, nessun duplicato, ordine di apparizione.</summary>
    public static IReadOnlyList<SkillType> DaElenco(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return Array.Empty<SkillType>();
        return DaElencoDiNomi(testo.Split(','));
    }

    /// <summary>Come <see cref="DaElenco"/> ma da una lista già strutturata
    /// (<c>PackageBackground.SkillProficiencies</c>).</summary>
    public static IReadOnlyList<SkillType> DaElencoDiNomi(IEnumerable<string>? nomi)
    {
        if (nomi is null) return Array.Empty<SkillType>();

        var risultato = new List<SkillType>();
        var viste = new HashSet<SkillType>();
        foreach (var voce in nomi)
        {
            var skill = DaNome(voce);
            if (skill is not null && viste.Add(skill.Value))
                risultato.Add(skill.Value);
        }
        return risultato;
    }

    public static bool Competente(Character pg, SkillType abilita) => abilita switch
    {
        SkillType.Athletics => pg.ProfAthletics,
        SkillType.Acrobatics => pg.ProfAcrobatics,
        SkillType.SleightOfHand => pg.ProfSleightOfHand,
        SkillType.Stealth => pg.ProfStealth,
        SkillType.Arcana => pg.ProfArcana,
        SkillType.History => pg.ProfHistory,
        SkillType.Investigation => pg.ProfInvestigation,
        SkillType.Nature => pg.ProfNature,
        SkillType.Religion => pg.ProfReligion,
        SkillType.AnimalHandling => pg.ProfAnimalHandling,
        SkillType.Insight => pg.ProfInsight,
        SkillType.Medicine => pg.ProfMedicine,
        SkillType.Perception => pg.ProfPerception,
        SkillType.Survival => pg.ProfSurvival,
        SkillType.Deception => pg.ProfDeception,
        SkillType.Intimidation => pg.ProfIntimidation,
        SkillType.Performance => pg.ProfPerformance,
        SkillType.Persuasion => pg.ProfPersuasion,
        _ => false
    };

    public static void ImpostaCompetenza(Character pg, SkillType abilita, bool valore)
    {
        switch (abilita)
        {
            case SkillType.Athletics: pg.ProfAthletics = valore; break;
            case SkillType.Acrobatics: pg.ProfAcrobatics = valore; break;
            case SkillType.SleightOfHand: pg.ProfSleightOfHand = valore; break;
            case SkillType.Stealth: pg.ProfStealth = valore; break;
            case SkillType.Arcana: pg.ProfArcana = valore; break;
            case SkillType.History: pg.ProfHistory = valore; break;
            case SkillType.Investigation: pg.ProfInvestigation = valore; break;
            case SkillType.Nature: pg.ProfNature = valore; break;
            case SkillType.Religion: pg.ProfReligion = valore; break;
            case SkillType.AnimalHandling: pg.ProfAnimalHandling = valore; break;
            case SkillType.Insight: pg.ProfInsight = valore; break;
            case SkillType.Medicine: pg.ProfMedicine = valore; break;
            case SkillType.Perception: pg.ProfPerception = valore; break;
            case SkillType.Survival: pg.ProfSurvival = valore; break;
            case SkillType.Deception: pg.ProfDeception = valore; break;
            case SkillType.Intimidation: pg.ProfIntimidation = valore; break;
            case SkillType.Performance: pg.ProfPerformance = valore; break;
            case SkillType.Persuasion: pg.ProfPersuasion = valore; break;
        }
    }

    public static bool Esperto(Character pg, SkillType abilita) => abilita switch
    {
        SkillType.Athletics => pg.ExpAthletics,
        SkillType.Acrobatics => pg.ExpAcrobatics,
        SkillType.SleightOfHand => pg.ExpSleightOfHand,
        SkillType.Stealth => pg.ExpStealth,
        SkillType.Arcana => pg.ExpArcana,
        SkillType.History => pg.ExpHistory,
        SkillType.Investigation => pg.ExpInvestigation,
        SkillType.Nature => pg.ExpNature,
        SkillType.Religion => pg.ExpReligion,
        SkillType.AnimalHandling => pg.ExpAnimalHandling,
        SkillType.Insight => pg.ExpInsight,
        SkillType.Medicine => pg.ExpMedicine,
        SkillType.Perception => pg.ExpPerception,
        SkillType.Survival => pg.ExpSurvival,
        SkillType.Deception => pg.ExpDeception,
        SkillType.Intimidation => pg.ExpIntimidation,
        SkillType.Performance => pg.ExpPerformance,
        SkillType.Persuasion => pg.ExpPersuasion,
        _ => false
    };

    public static void ImpostaEsperienza(Character pg, SkillType abilita, bool valore)
    {
        switch (abilita)
        {
            case SkillType.Athletics: pg.ExpAthletics = valore; break;
            case SkillType.Acrobatics: pg.ExpAcrobatics = valore; break;
            case SkillType.SleightOfHand: pg.ExpSleightOfHand = valore; break;
            case SkillType.Stealth: pg.ExpStealth = valore; break;
            case SkillType.Arcana: pg.ExpArcana = valore; break;
            case SkillType.History: pg.ExpHistory = valore; break;
            case SkillType.Investigation: pg.ExpInvestigation = valore; break;
            case SkillType.Nature: pg.ExpNature = valore; break;
            case SkillType.Religion: pg.ExpReligion = valore; break;
            case SkillType.AnimalHandling: pg.ExpAnimalHandling = valore; break;
            case SkillType.Insight: pg.ExpInsight = valore; break;
            case SkillType.Medicine: pg.ExpMedicine = valore; break;
            case SkillType.Perception: pg.ExpPerception = valore; break;
            case SkillType.Survival: pg.ExpSurvival = valore; break;
            case SkillType.Deception: pg.ExpDeception = valore; break;
            case SkillType.Intimidation: pg.ExpIntimidation = valore; break;
            case SkillType.Performance: pg.ExpPerformance = valore; break;
            case SkillType.Persuasion: pg.ExpPersuasion = valore; break;
        }
    }

    // CatalogKey.NormalizeName piega maiuscole e accenti ma non gli spazi ripetuti in mezzo al
    // testo (solo quelli iniziali/finali, via Trim): un "spazi in eccesso" scritto a mano può
    // comunque contenerne. Li collassiamo qui, non dentro CatalogKey, che non ne ha bisogno per gli
    // usi che già ha (nomi di catalogo, non prosa libera da tavolo).
    private static string ChiaveDiRicerca(string? nome)
    {
        var normalizzato = CatalogKey.NormalizeName(nome);
        if (normalizzato.Length == 0) return string.Empty;

        var sb = new System.Text.StringBuilder(normalizzato.Length);
        var precedenteEraSpazio = false;
        foreach (var c in normalizzato)
        {
            var eSpazio = c == ' ';
            if (eSpazio && precedenteEraSpazio) continue;
            sb.Append(c);
            precedenteEraSpazio = eSpazio;
        }
        return sb.ToString();
    }
}
