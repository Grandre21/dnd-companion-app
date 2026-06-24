using DndCompanion.Models;
using DndCompanion.Services.Repositories;
using Xunit;

namespace DndCompanion.Tests;

// Logica pura di ordinamento inventario estratta da InventoryRepository (SortForDisplay):
// per tipo poi nome, entrambi case-insensitive; tipo nullo trattato come stringa vuota.
public class InventoryRepositoryTests
{
    private static InventoryItem I(string name, string? type) => new()
    {
        Id = name,
        CharacterId = "c",
        Name = name,
        ItemType = type,
    };

    [Fact]
    public void SortForDisplay_orders_by_type_then_name_case_insensitive()
    {
        var items = new[]
        {
            I("Scudo", "Armatura"),
            I("ascia", "weapon"),
            I("Corda", "Altro"),
            I("Arco", "weapon"),
            I("Elmo", "armatura"), // stesso tipo di "Armatura" ignorando il case
        };

        var order = InventoryRepository.SortForDisplay(items).Select(i => i.Name).ToList();

        // Tipi: Altro < Armatura(==armatura) < weapon; entro il tipo, nome case-insensitive.
        Assert.Equal(new[] { "Corda", "Elmo", "Scudo", "Arco", "ascia" }, order);
    }

    [Fact]
    public void SortForDisplay_treats_null_type_as_empty_and_first()
    {
        var items = new[]
        {
            I("Spada", "weapon"),
            I("Misterioso", null),
        };

        var order = InventoryRepository.SortForDisplay(items).Select(i => i.Name).ToList();

        Assert.Equal(new[] { "Misterioso", "Spada" }, order);
    }
}
