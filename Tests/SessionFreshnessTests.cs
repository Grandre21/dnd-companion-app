using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>
/// Test di <see cref="SessionFreshness"/>: le due decisioni pure che permettono a
/// <see cref="DndCompanion.Services.SupabaseService"/> di rinfrescare la sessione prima che il
/// bug della guardia in gotrue-csharp 4.2.7 (v. il commento sulla classe) diventi raggiungibile.
/// </summary>
public class SessionFreshnessTests
{
    // ---- VaRinfrescata ----

    [Fact]
    public void VaRinfrescata_scadenza_ampiamente_futura_ritorna_false()
    {
        var adesso = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        var scadenza = adesso.AddHours(1);

        Assert.False(SessionFreshness.VaRinfrescata(scadenza, adesso));
    }

    [Fact]
    public void VaRinfrescata_scadenza_gia_passata_ritorna_true()
    {
        var adesso = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        var scadenza = adesso.AddMinutes(-1);

        Assert.True(SessionFreshness.VaRinfrescata(scadenza, adesso));
    }

    [Fact]
    public void VaRinfrescata_scadenza_dentro_il_margine_ritorna_true()
    {
        // Questo è il caso che rende il test non vacuo: la scadenza è ancora nel futuro (un
        // banale Expired() direbbe "no"), ma dentro i 5 minuti di margine — è esattamente la
        // differenza fra questo helper e Session.Expired(), quella che chiude il bug.
        var adesso = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        var scadenza = adesso.AddMinutes(2);

        Assert.True(SessionFreshness.VaRinfrescata(scadenza, adesso));
    }

    [Fact]
    public void VaRinfrescata_esattamente_al_confine_dei_5_minuti_ritorna_true()
    {
        var adesso = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        var scadenza = adesso + SessionFreshness.Margine;

        Assert.True(SessionFreshness.VaRinfrescata(scadenza, adesso));
    }

    // ---- SiPuoRitentare ----

    [Fact]
    public void SiPuoRitentare_senza_fallimenti_precedenti_ritorna_true()
    {
        var adesso = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(SessionFreshness.SiPuoRitentare(null, adesso));
    }

    [Fact]
    public void SiPuoRitentare_con_un_fallimento_appena_avvenuto_ritorna_false()
    {
        var adesso = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        var ultimoFallimento = adesso.AddSeconds(-5);

        Assert.False(SessionFreshness.SiPuoRitentare(ultimoFallimento, adesso));
    }

    [Fact]
    public void SiPuoRitentare_con_un_fallimento_vecchio_ritorna_true()
    {
        var adesso = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
        var ultimoFallimento = adesso.AddMinutes(-1);

        Assert.True(SessionFreshness.SiPuoRitentare(ultimoFallimento, adesso));
    }
}
