-- Le sottoclassi di una classe: una colonna testuale su `classes`, non una tabella dedicata.
--
-- Perché la colonna. Una tabella `subclasses` avrebbe richiesto quattro policy nuove sulla stessa
-- superficie RLS che ha ancora il varco «campaign hopping» aperto (DA-FARE §2), più un aggregato
-- intero da attraversare — repository, piano di import, piano di rimozione, merge dei cataloghi.
-- La colonna eredita le policy di `classes` così come sono, e tiene la sottoclasse dentro la riga
-- della classe, che è dove la provenienza (`source_id`) vive già: una sottoclasse importata dal
-- manuale e la classe che la porta si rimuovono insieme, senza codice nuovo.
--
-- Perché è compatibile col client già online (il service worker non fa skipWaiting: i due client
-- convivono sullo stesso database). In lettura: postgrest-csharp deserializza con Newtonsoft, che
-- ignora le colonne che il modello non dichiara. In scrittura: un `Update` serializza le sole
-- colonne mappate, quindi il client vecchio non può azzerare questa — non la conosce.
--
-- Il formato del testo è definito da Services/SubclassText.cs e riusa la sintassi già in uso per la
-- tabella dei livelli (`classes.features`, v. Services/ClassProgression.cs):
--
--   ## Cammino del berserker
--   id: srd-2024-it/sottoclasse/cammino-del-berserker
--   Chi percorre questo cammino incanala la furia in una violenza cieca.
--   L3 — Frenesia
--   L6 — Ira incontenibile
--
-- Resta leggibile a occhio in un textarea — chi non sa nulla del formato vede un elenco sensato —
-- e resta rileggibile senza perdite dall'export (l'`id:` esiste solo per quello).

ALTER TABLE "public"."classes" ADD COLUMN IF NOT EXISTS "subclasses" "text";

COMMENT ON COLUMN "public"."classes"."subclasses" IS 'Sottoclassi della classe, nel formato testuale di Services/SubclassText.cs: un blocco per sottoclasse, aperto da "## <nome>", con "id: <id>" facoltativo, la descrizione libera e i privilegi per livello nella forma "L<n> - <privilegi>". Nessun limite al numero di voci.';
