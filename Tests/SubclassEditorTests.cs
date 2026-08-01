using DndCompanion.Models.Packages;
using DndCompanion.Services;
using Xunit;

namespace DndCompanion.Tests;

/// <summary>Manipolazione dell'elenco di sottoclassi (<see cref="SubclassEditor"/>): la pagina
/// Classi lo usa per aggiungere, modificare e rimuovere una sottoclasse dal campo testuale
/// <c>classes.subclasses</c>, senza mai improvvisare la lettura/scrittura del formato — quella
/// resta di <see cref="SubclassText"/>.</summary>
public class SubclassEditorTests
{
    [Fact]
    public void Aggiungere_a_un_campo_vuoto_crea_il_primo_blocco()
    {
        var risultato = SubclassEditor.Aggiungi(
            null, SubclassEditor.Costruisci("Campione", "", "L3 — Critico migliorato"));

        var voci = SubclassText.Leggi(risultato);

        Assert.Single(voci);
        Assert.Equal("Campione", voci[0].Name);
        Assert.Equal(new[] { 3 }, voci[0].Levels.Select(l => l.Level));
    }

    /// <summary>Il difetto che chiude: la voce sostituita veniva rimossa e riaccodata, quindi
    /// correggere un refuso nella descrizione di «Campione» la faceva saltare in fondo all'elenco. Non
    /// è invisibile: quell'ordine è quello che si legge nella card della pagina Classi e nel menu delle
    /// tre schermate del personaggio.</summary>
    [Fact]
    public void Modificare_una_sottoclasse_non_la_sposta_in_fondo_allelenco()
    {
        var esistente = SubclassText.Serializza(new[]
        {
            SubclassEditor.Costruisci("Campione", "vecchia", "L3 — Critico"),
            SubclassEditor.Costruisci("Cavaliere", "", "L3 — Manovre"),
            SubclassEditor.Costruisci("Assassino", "", "L3 — Agguato"),
        });

        var risultato = SubclassEditor.Aggiungi(
            esistente, SubclassEditor.Costruisci("Campione", "corretta", "L3 — Critico"));

        var voci = SubclassText.Leggi(risultato);

        Assert.Equal(new[] { "Campione", "Cavaliere", "Assassino" }, voci.Select(s => s.Name));
        Assert.Equal("corretta", voci[0].Description);
    }

    /// <summary>Il difetto che chiude: ribattezzare «Campione» in «Cavaliere» quando un «Cavaliere»
    /// esiste già ne lasciava una sola — descrizione e privilegi dell'altra persi senza un avviso,
    /// perché dal punto di vista di <c>Aggiungi</c> sostituire per nome è esattamente ciò che le si
    /// chiede. La domanda va posta prima, e la pagina Classi la pone.</summary>
    [Fact]
    public void CollideConUnAltra_riconosce_il_rinomino_su_un_nome_gia_in_elenco()
    {
        var elenco = SubclassText.Serializza(new[]
        {
            SubclassEditor.Costruisci("Campione", "", "L3 — Critico"),
            SubclassEditor.Costruisci("Cavaliere", "", "L3 — Manovre"),
        });

        // Sto modificando «Campione» e gli dò il nome dell'altra voce: collisione.
        Assert.True(SubclassEditor.CollideConUnAltra(elenco, " cavaliere ", "Campione"));

        // Salvo «Campione» col suo stesso nome: è la voce che sto modificando, nessuna collisione.
        Assert.False(SubclassEditor.CollideConUnAltra(elenco, "CAMPIONE", "Campione"));

        // Un nome nuovo, e il caso di un'aggiunta (nessuna voce in modifica).
        Assert.False(SubclassEditor.CollideConUnAltra(elenco, "Assassino", "Campione"));
        Assert.True(SubclassEditor.CollideConUnAltra(elenco, "Cavaliere", null));
        Assert.False(SubclassEditor.CollideConUnAltra(elenco, "   ", null));
        Assert.False(SubclassEditor.CollideConUnAltra(null, "Cavaliere", null));
    }

    /// <summary>Il caso che la prima versione della guardia lasciava passare: se l'elenco ha già
    /// <b>due</b> voci omonime, salvare quel nome le collassa in una sola e l'altra è persa — e
    /// scattava proprio quando il nome era quello in modifica, cioè il caso normale. È raggiungibile:
    /// il parser controlla presenza e unicità degli <c>id</c> delle sottoclassi, non l'unicità dei
    /// nomi, quindi un file con due «Campione» entra a catalogo.</summary>
    [Fact]
    public void CollideConUnAltra_scatta_anche_su_due_omonime_gia_in_elenco()
    {
        const string elenco = "## Campione\nL3 — Critico\n\n## Campione\nL3 — Altro";

        Assert.Equal(2, SubclassText.Leggi(elenco).Count);
        Assert.True(SubclassEditor.CollideConUnAltra(elenco, "Campione", "Campione"));
    }

    /// <summary>Un rinomino tiene la posizione della voce di partenza, non quella del nome nuovo:
    /// altrimenti rinominare sarebbe indistinguibile da «togli e riaggiungi in fondo».</summary>
    [Fact]
    public void Rinominare_una_sottoclasse_ne_conserva_la_posizione()
    {
        var esistente = SubclassText.Serializza(new[]
        {
            SubclassEditor.Costruisci("Campione", "", "L3 — Critico"),
            SubclassEditor.Costruisci("Cavaliere", "", "L3 — Manovre"),
        });

        var risultato = SubclassEditor.Aggiungi(
            esistente,
            SubclassEditor.Costruisci("Paladino del sale", "", "L3 — Critico"),
            nomePrecedente: "Campione");

        Assert.Equal(new[] { "Paladino del sale", "Cavaliere" },
            SubclassText.Leggi(risultato).Select(s => s.Name));
    }

    /// <summary>Salvare due volte la stessa sottoclasse non deve duplicarla: il confronto è
    /// normalizzato come nel resto dei cataloghi, quindi accenti, maiuscole e spazi ai bordi non
    /// bastano a farla sembrare una voce diversa.</summary>
    [Fact]
    public void Aggiungere_unomonima_la_sostituisce_invece_di_duplicarla()
    {
        var esistente = SubclassText.Serializza(new[]
        {
            SubclassEditor.Costruisci("Cammino del berserker", "vecchia descrizione", "L3 — Frenesia"),
        });

        var risultato = SubclassEditor.Aggiungi(
            esistente,
            SubclassEditor.Costruisci(
                "  CAMMINO DEL BERSERKER  ", "nuova descrizione", "L3 — Frenesia\nL6 — Ira incontenibile"));

        var voci = SubclassText.Leggi(risultato);

        Assert.Single(voci);
        Assert.Equal("nuova descrizione", voci[0].Description);
        Assert.Equal(2, voci[0].Levels.Count);
    }

    /// <summary>Il difetto che chiude: senza <c>nomePrecedente</c>, rinominare una sottoclasse
    /// dal mini-form aggiungerebbe la nuova voce accanto alla vecchia invece di sostituirla, perché
    /// i due nomi normalizzati sono diversi e nessuno dei due filtri di <c>Aggiungi</c> la
    /// troverebbe.</summary>
    [Fact]
    public void Rinominare_una_sottoclasse_toglie_la_voce_col_nome_precedente()
    {
        var esistente = SubclassText.Serializza(new[]
        {
            SubclassEditor.Costruisci("Cammino del berserker", "", "L3 — Frenesia"),
        });

        var risultato = SubclassEditor.Aggiungi(
            esistente,
            SubclassEditor.Costruisci("Cammino della furia", "", "L3 — Frenesia"),
            nomePrecedente: "Cammino del berserker");

        var voci = SubclassText.Leggi(risultato);

        Assert.Single(voci);
        Assert.Equal("Cammino della furia", voci[0].Name);
    }

    [Fact]
    public void Rimuovere_confronta_i_nomi_normalizzati()
    {
        var esistente = SubclassText.Serializza(new[]
        {
            SubclassEditor.Costruisci("Tradizione arcana", "", "L3 — Recupero arcano"),
            SubclassEditor.Costruisci("Cammino del berserker", "", "L3 — Frenesia"),
        });

        var risultato = SubclassEditor.Rimuovi(esistente, "  tradizione ARCANA  ");

        var voci = SubclassText.Leggi(risultato);

        Assert.Single(voci);
        Assert.Equal("Cammino del berserker", voci[0].Name);
    }

    [Fact]
    public void Rimuovere_da_un_elenco_vuoto_non_solleva_errori()
    {
        Assert.Equal(string.Empty, SubclassEditor.Rimuovi(null, "Campione"));
        Assert.Equal(string.Empty, SubclassEditor.Rimuovi("", "Campione"));
    }

    /// <summary>Se il campo è prosa che <see cref="SubclassText"/> non saprebbe restituire intatta
    /// (nessun blocco riconoscibile), aggiungere o rimuovere non deve riscriverlo: senza questa
    /// guardia un giro Leggi+Serializza butterebbe via la nota scritta a mano, perché
    /// <see cref="SubclassText.Leggi"/> ignora tutto ciò che precede il primo blocco.</summary>
    [Fact]
    public void Un_testo_che_non_e_un_elenco_non_va_distrutto()
    {
        const string nota = "Da noi la sottoclasse si sceglie al 2°, non al 3°.";

        var dopoAggiunta = SubclassEditor.Aggiungi(nota, SubclassEditor.Costruisci("Campione", "", "L3 — Critico"));
        var dopoRimozione = SubclassEditor.Rimuovi(nota, "Campione");

        Assert.Equal(nota, dopoAggiunta);
        Assert.Equal(nota, dopoRimozione);
        Assert.False(SubclassEditor.PuoModificare(nota));
    }

    /// <summary>Anche una nota scritta SOPRA un elenco altrimenti valido blocca la modifica
    /// guidata: la guardia giusta è <c>SoloElenco</c>, non <c>SembraElenco</c> — con quest'ultima
    /// basterebbe un blocco riconosciuto perché la nota in cima sparisse in silenzio al primo
    /// salvataggio.</summary>
    [Fact]
    public void Una_nota_sopra_un_elenco_valido_blocca_comunque_la_modifica_guidata()
    {
        const string testo = "Nota del tavolo.\n## Campione\nL3 — Critico";

        Assert.False(SubclassEditor.PuoModificare(testo));
        Assert.Equal(testo, SubclassEditor.Aggiungi(testo, SubclassEditor.Costruisci("Altra", "", "")));
    }

    [Fact]
    public void Costruisci_traduce_i_privilegi_per_livello_come_la_tabella_di_classe()
    {
        var sottoclasse = SubclassEditor.Costruisci("Mistico", "  Testo  ", "L3 — Trucchetti · Slot 4/2");

        Assert.Equal("Mistico", sottoclasse.Name);
        Assert.Equal("Testo", sottoclasse.Description);
        Assert.Single(sottoclasse.Levels);
        Assert.Equal(3, sottoclasse.Levels[0].Level);
        Assert.Equal(new[] { "Trucchetti" }, sottoclasse.Levels[0].Features);
        Assert.Equal(new[] { 4, 2 }, sottoclasse.Levels[0].SpellSlots);
    }
}
