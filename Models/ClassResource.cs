namespace DndCompanion.Models;

/// <summary>
/// Una risorsa di classe con i suoi usi (Ira, Ispirazione bardica, Focus del monaco, ...) —
/// le caselline a matita accanto ai privilegi sulla scheda cartacea. È serializzata come elemento
/// del campo jsonb <c>class_resources</c> di <see cref="Character"/> (POCO, non una tabella a sé):
/// stesso pattern di <see cref="Combatant"/> dentro <c>combat_state.combatants</c>.
///
/// Quattro campi e non uno di più (D3 nello spec 2026-08-06): nessun campo «effetto», nessuna
/// formula, nessun innesco automatico. Il contatore conta e basta; la semantica del privilegio resta
/// nella prosa che il personaggio già porta altrove.
/// </summary>
public class ClassResource
{
    public string Nome { get; set; } = string.Empty;
    public int Max { get; set; }
    public int Spesi { get; set; }

    /// <summary>Quando si ricarica: <c>lungo</c>, <c>breve</c> o <c>nessuna</c> (risorse che si
    /// ripristinano in altro modo — una volta per turno, a discrezione del master — dove il
    /// contatore serve ma il riposo non lo tocca).</summary>
    public string Ricarica { get; set; } = "lungo";
}
