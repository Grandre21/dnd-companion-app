using DndCompanion.Models;
using DndCompanion.Pages;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// <see cref="Characters.CloneCharacter"/> (Pages/Characters.razor) deve fare un round-trip
/// COMPLETO: il form di modifica lo usa per popolare la bozza, e <c>SaveFormAsync</c> rimanda quella
/// bozza intera a <c>UpdateCharacterAsync</c> — postgrest serializza TUTTE le colonne mappate, non
/// solo quelle toccate dall'utente, quindi una proprietà non copiata dal clone si azzera in
/// database al primo salvataggio. È già successo due volte (Subclass, poi ClassResources/
/// ArmorTraining/WeaponProficiencies/ToolProficiencies): questo test confronta per riflessione TUTTE
/// le proprietà pubbliche dichiarate su <see cref="Character"/> (non quelle eredidate da BaseModel,
/// che CloneCharacter non tocca), così il prossimo campo dimenticato fa fallire QUESTO test invece
/// di azzerare dati in produzione.
/// </summary>
public class CharacterCloneTests
{
    [Fact]
    public void CloneCharacter_copia_ogni_proprieta_pubblica_dichiarata_su_Character()
    {
        var originale = CostruisciCharacterConValoriDistinti();
        var clone = Characters.CloneCharacter(originale);

        CharacterReflectionTestHelpers.AssertPersonaggiUguali(originale, clone, (prop, valoreOriginale, valoreClone) =>
            $"Character.{prop.Name}: originale='{valoreOriginale}' ma il clone ha " +
            $"'{valoreClone}' — CloneCharacter non copia (più) questo campo.");
    }

    [Fact]
    public void CloneCharacter_copia_ClassResources_per_valore_non_per_riferimento()
    {
        var originale = new Character
        {
            ClassResources = new List<ClassResource>
            {
                new() { Nome = "Ira", Max = 2, Spesi = 0, Ricarica = "lungo" },
            },
        };

        var clone = Characters.CloneCharacter(originale);

        // Deve essere una lista NUOVA (con ClassResource NUOVI dentro), non lo stesso riferimento:
        // altrimenti la bozza in modifica e il personaggio aperto condividerebbero lo stato, e
        // "Annulla" sul form non annullerebbe più nulla.
        Assert.NotSame(originale.ClassResources, clone.ClassResources);
        Assert.NotSame(originale.ClassResources[0], clone.ClassResources[0]);

        clone.ClassResources[0].Spesi = 2;
        clone.ClassResources.Add(new ClassResource { Nome = "Nuova", Max = 1, Spesi = 0, Ricarica = "breve" });

        Assert.Equal(0, originale.ClassResources[0].Spesi); // l'originale non ha visto la spesa...
        Assert.Single(originale.ClassResources);             // ...né la nuova voce aggiunta al clone.
    }

    // Un valore diverso dal default di OGNI proprietà (compresi i default non-zero dichiarati sul
    // modello, es. Level = 1, ArmorClass = 10, Size = "Media"): se CloneCharacter dimentica una
    // proprietà, il clone la ritrova al suo valore di default — diverso da quello impostato qui — e
    // il confronto sopra lo scopre.
    private static Character CostruisciCharacterConValoriDistinti()
    {
        var c = new Character();
        foreach (var prop in CharacterReflectionTestHelpers.ProprietaCharacter())
        {
            if (prop.Name == nameof(Character.ClassResources))
            {
                prop.SetValue(c, new List<ClassResource>
                {
                    new() { Nome = "Ira", Max = 2, Spesi = 1, Ricarica = "lungo" },
                    new() { Nome = "Azione impetuosa", Max = 1, Spesi = 0, Ricarica = "breve" },
                });
                continue;
            }

            if (prop.Name == nameof(Character.CharacterFeatures))
            {
                prop.SetValue(c, new List<CharacterFeature>
                {
                    new()
                    {
                        Nome = "Ira", Nota = "1/riposo lungo", Azione = "Bonus",
                        Risorsa = "Ira", Attivabile = true,
                    },
                    new()
                    {
                        Nome = "Percezione del pericolo", Nota = "Vantaggio ai TS su trappole",
                        Azione = "Nessuna", Risorsa = "Nessuna", Attivabile = false,
                    },
                });
                continue;
            }

            object valore = prop.PropertyType == typeof(string)
                ? $"valore-{prop.Name}"
                : prop.PropertyType == typeof(int)
                    ? 42
                    : prop.PropertyType == typeof(bool)
                        ? true
                        : prop.PropertyType == typeof(DateTime?)
                            ? new DateTime(2020, 1, 1)
                            : throw new InvalidOperationException(
                                $"Character.{prop.Name} ha tipo {prop.PropertyType} — questo test " +
                                "non sa ancora costruirne un valore distinto dal default: aggiungilo " +
                                "qui prima di fidarti del round-trip di CloneCharacter.");
            prop.SetValue(c, valore);
        }
        return c;
    }
}
