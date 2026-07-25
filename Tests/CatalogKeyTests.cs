using DndCompanion.Services;

namespace DndCompanion.Tests;

public class CatalogKeyTests
{
    [Theory]
    [InlineData("Invisibilità", "invisibilita")]
    [InlineData("  Oscurità  ", "oscurita")]
    [InlineData("Velocità", "velocita")]
    [InlineData("Palla di Fuoco", "palla di fuoco")]
    [InlineData("PERCEZIONE", "percezione")]
    [InlineData("Éclair", "eclair")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeName_PiegaAccentiEMinuscole(string? input, string atteso)
        => Assert.Equal(atteso, CatalogKey.NormalizeName(input));

    // Il progetto compila con InvariantGlobalization=true: String.Normalize non decompone e non
    // solleva eccezioni. Questo test è la rete di sicurezza contro un ritorno a quell'API.
    [Fact]
    public void NormalizeName_NomiAccentatiDiversiSoloPerAccento_CollassanoSullaStessaChiave()
    {
        Assert.Equal(CatalogKey.NormalizeName("Invisibilità"), CatalogKey.NormalizeName("INVISIBILITA"));
    }

    [Fact]
    public void For_ConSourceId_UsaLIdentificatore()
        => Assert.Equal("srd-2024-it/elfo", CatalogKey.For("srd-2024-it/elfo", "Elfo"));

    [Fact]
    public void For_SenzaSourceId_UsaIlNomeNormalizzato()
        => Assert.Equal("elfo", CatalogKey.For(null, "Elfo"));

    [Fact]
    public void For_SourceIdVuoto_TrattatoComeAssente()
        => Assert.Equal("elfo", CatalogKey.For("   ", "Elfo"));

    [Theory]
    [InlineData("srd-2024-it/elfo", true)]
    [InlineData("mio-pacchetto/elfo", false)]
    [InlineData("srd-2024-it-altro/elfo", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsFromAppPackage_RiconosceIlPrefissoDelPacchettoDellApp(string? sourceId, bool atteso)
        => Assert.Equal(atteso, CatalogKey.IsFromAppPackage(sourceId));
}
