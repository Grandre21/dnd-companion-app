using System.Reflection;
using DndCompanion.Models;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Confronto per riflessione di due <see cref="Character"/>, condiviso da
/// <see cref="CharacterCloneTests"/> (<c>Characters.CloneCharacter</c>) e
/// <see cref="CreationChainTests"/> (<see cref="DndCompanion.Services.CreationChain.Deriva"/>):
/// entrambi verificano un round-trip COMPLETO — ogni proprietà pubblica dichiarata su
/// <see cref="Character"/>, <see cref="Character.ClassResources"/> compreso (una lista: confrontarla
/// per riferimento non basterebbe). Era la stessa copia del medesimo corpo in due file (MINORE 9 del
/// gate del 2026-08-06): un punto solo da aggiornare al prossimo campo nuovo, invece di due che
/// possono scollegarsi — senza indebolire la copertura di <see cref="CharacterCloneTests"/>, che
/// <c>CLAUDE.md</c> cita esplicitamente come la rete che reclama il prossimo campo dimenticato.
/// </summary>
public static class CharacterReflectionTestHelpers
{
    public static IEnumerable<PropertyInfo> ProprietaCharacter() =>
        typeof(Character).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    /// <summary><paramref name="descrizioneDifetto"/> personalizza il messaggio di fallimento: i due
    /// chiamanti verificano round-trip diversi (il clone del form di modifica, il fold della
    /// creazione guidata) e vogliono un messaggio che nomini il meccanismo giusto, non un testo
    /// generico che non dice a chi rivolgersi.</summary>
    public static void AssertPersonaggiUguali(
        Character atteso, Character effettivo, Func<PropertyInfo, object?, object?, string> descrizioneDifetto)
    {
        foreach (var prop in ProprietaCharacter())
        {
            if (prop.Name == nameof(Character.ClassResources))
            {
                var a = Assert.IsType<List<ClassResource>>(prop.GetValue(atteso));
                var e = Assert.IsType<List<ClassResource>>(prop.GetValue(effettivo));
                Assert.Equal(a.Count, e.Count);
                for (var i = 0; i < a.Count; i++)
                {
                    Assert.Equal(a[i].Nome, e[i].Nome);
                    Assert.Equal(a[i].Max, e[i].Max);
                    Assert.Equal(a[i].Spesi, e[i].Spesi);
                    Assert.Equal(a[i].Ricarica, e[i].Ricarica);
                }
                continue;
            }

            var valoreAtteso = prop.GetValue(atteso);
            var valoreEffettivo = prop.GetValue(effettivo);
            Assert.True(Equals(valoreAtteso, valoreEffettivo), descrizioneDifetto(prop, valoreAtteso, valoreEffettivo));
        }
    }
}
