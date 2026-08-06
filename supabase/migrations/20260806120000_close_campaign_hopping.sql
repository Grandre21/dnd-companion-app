-- Chiude il varco RLS «campaign hopping» sulle policy di UPDATE — l'unico 🔴 di codice rimasto
-- come gate del lancio pubblico (docs/DA-FARE.md §2, dettaglio in docs/archivio/DA-FARE-chiuso.md §1).
--
-- IL DIFETTO. Su races/classes/spells/monsters/backgrounds la WITH CHECK di *_update è testualmente
-- identica alla USING: (added_by = auth.uid() OR is_campaign_master(campaign_id)). Su characters la
-- stessa struttura usa owner_id al posto di added_by. added_by/owner_id NON cambiano quando si
-- riassegna campaign_id: per l'autore/proprietario quel ramo della OR resta vero indipendentemente
-- da dove la riga finisce, quindi via REST diretto (non esposto dall'attuale UI, che non offre un
-- modo di cambiare campagna a una riga) chi ha scritto una riga può spostarla verso una campagna di
-- cui non è mai stato membro. Su notes manca anche il ramo master (USING/WITH CHECK è il solo
-- owner_id = auth.uid()): lì l'iniezione è irreversibile, perché nessuno nella campagna bersaglio —
-- nemmeno il suo master — ha un ramo per rimuovere la riga altrui. È il caso peggiore: una nota
-- condivisa iniettata da un estraneo che nessuno può più togliere.
--
-- Caso gemello, causato dalla stessa riga di codice: la USING originale non richiede la membership
-- CORRENTE del richiedente, solo che added_by/owner_id combini con l'id dell'utente. Un utente
-- rimosso da campaign_members conserva quindi la scrittura sulle proprie righe rimaste nella
-- campagna che ha lasciato — nessuno spostamento, solo l'assenza di un controllo di appartenenza
-- ancora valida.
--
-- LA CORREZIONE. Si lega il ramo "sei l'autore/proprietario" alla appartenenza CORRENTE alla
-- campagna della riga in esame — is_campaign_member(campaign_id), non più la sola uguaglianza su una
-- colonna che un movimento non tocca. Scrivendo la STESSA espressione sia in USING sia in WITH
-- CHECK, Postgres la valuta due volte con due set di valori diversi (comportamento nativo delle
-- policy di UPDATE, già in uso altrove in questo schema, es. is_campaign_master su characters):
--   USING     → valutata sulla riga VECCHIA: se l'autore non è più membro della campagna in cui la
--               riga si trova oggi, l'update non trova righe da toccare → chiude il caso gemello.
--   WITH CHECK → valutata sulla riga NUOVA: se la destinazione è una campagna di cui l'autore non è
--               membro, l'update viene respinto → chiude il campaign hopping.
-- Il ramo master resta invariato: is_campaign_master interroga già campaign_members con
-- role = 'master', quindi implica di per sé appartenenza corrente e non aveva questa lacuna.
--
-- RESIDUO ACCETTATO (già discusso in docs/archivio/DA-FARE-chiuso.md §1, "piste" indicata lì): un
-- autore può ancora spostare una propria riga verso una campagna di cui è GIÀ membro. Non è
-- l'accesso a dati altrui che questo gate deve chiudere — è spostare un proprio contenuto fra due
-- campagne alla cui appartenenza legittima l'autore ha già accesso in lettura e scrittura.
--
-- COMPATIBILITÀ COL CLIENT LIVE. Questa migrazione si applica a mano PRIMA del push (v. CLAUDE.md):
-- per un po' il client attualmente pubblicato parlerà con questo schema. Nessuna schermata attuale
-- permette di cambiare campaign_id a una riga esistente: ogni update che il client emette oggi lascia
-- quella colonna invariata, quindi per un membro corrente che modifica una propria riga, o un master
-- che modifica una riga della propria campagna, added_by/owner_id e campaign_id restano entrambi
-- gli stessi di prima e is_campaign_member(campaign_id) è vera esattamente come lo era la vecchia
-- condizione priva di quel controllo: nessuna operazione legittima cambia comportamento. Cambia solo
-- il caso che si voleva chiudere: un ex-membro perde la scrittura sulle proprie righe rimaste in una
-- campagna che ha lasciato.
--
-- Idempotente e rieseguibile: DROP POLICY IF EXISTS prima di ogni CREATE POLICY, nessuna DDL
-- distruttiva. Nessuna funzione SECURITY DEFINER nuova: si riusano is_campaign_member/
-- is_campaign_master, già esistenti e già SECURITY DEFINER dalla migrazione iniziale.

-- =====================================================================================
-- 1. Cataloghi con colonna "added_by": races, classes, spells, monsters, backgrounds.
-- =====================================================================================

DROP POLICY IF EXISTS "races_update" ON "public"."races";
CREATE POLICY "races_update" ON "public"."races"
    FOR UPDATE USING (
        ("added_by" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id"))
        OR "public"."is_campaign_master"("campaign_id")
    ) WITH CHECK (
        ("added_by" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id"))
        OR "public"."is_campaign_master"("campaign_id")
    );

DROP POLICY IF EXISTS "classes_update" ON "public"."classes";
CREATE POLICY "classes_update" ON "public"."classes"
    FOR UPDATE USING (
        ("added_by" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id"))
        OR "public"."is_campaign_master"("campaign_id")
    ) WITH CHECK (
        ("added_by" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id"))
        OR "public"."is_campaign_master"("campaign_id")
    );

DROP POLICY IF EXISTS "spells_update" ON "public"."spells";
CREATE POLICY "spells_update" ON "public"."spells"
    FOR UPDATE USING (
        ("added_by" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id"))
        OR "public"."is_campaign_master"("campaign_id")
    ) WITH CHECK (
        ("added_by" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id"))
        OR "public"."is_campaign_master"("campaign_id")
    );

DROP POLICY IF EXISTS "monsters_update" ON "public"."monsters";
CREATE POLICY "monsters_update" ON "public"."monsters"
    FOR UPDATE USING (
        ("added_by" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id"))
        OR "public"."is_campaign_master"("campaign_id")
    ) WITH CHECK (
        ("added_by" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id"))
        OR "public"."is_campaign_master"("campaign_id")
    );

DROP POLICY IF EXISTS "backgrounds_update" ON "public"."backgrounds";
CREATE POLICY "backgrounds_update" ON "public"."backgrounds"
    FOR UPDATE USING (
        ("added_by" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id"))
        OR "public"."is_campaign_master"("campaign_id")
    ) WITH CHECK (
        ("added_by" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id"))
        OR "public"."is_campaign_master"("campaign_id")
    );

-- =====================================================================================
-- 2. characters: stessa struttura del punto 1, colonna "owner_id" al posto di "added_by".
-- =====================================================================================

DROP POLICY IF EXISTS "characters_update" ON "public"."characters";
CREATE POLICY "characters_update" ON "public"."characters"
    FOR UPDATE USING (
        ("owner_id" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id"))
        OR "public"."is_campaign_master"("campaign_id")
    ) WITH CHECK (
        ("owner_id" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id"))
        OR "public"."is_campaign_master"("campaign_id")
    );

-- =====================================================================================
-- 3. notes: nessun ramo master, per design (le note restano dominio del proprietario; il master
--    non ha mai avuto un ramo di modifica/cancellazione sulle note altrui, a differenza dei
--    cataloghi e dei personaggi). Aggiungerne uno qui allargherebbe i permessi oltre lo scopo di
--    questa migrazione, che è chiudere una lacuna, non introdurne di nuove: la sola appartenenza
--    corrente basta comunque a chiudere il caso peggiore, perché blocca l'iniezione alla radice
--    (l'autore non può più spostare la nota in una campagna di cui non è membro).
-- =====================================================================================

DROP POLICY IF EXISTS "notes_update" ON "public"."notes";
CREATE POLICY "notes_update" ON "public"."notes"
    FOR UPDATE USING (
        "owner_id" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id")
    ) WITH CHECK (
        "owner_id" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id")
    );
