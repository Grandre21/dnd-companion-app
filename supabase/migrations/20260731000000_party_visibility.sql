-- Visibilità dei personaggi + pagina Party.
-- Bug segnalato: ogni membro della campagna leggeva TUTTI i personaggi (characters_select faceva
-- OR con is_campaign_member), quindi in Personaggi un giocatore vedeva anche i PG altrui.
-- Decisione di prodotto: un giocatore vede SOLO i propri PG; il master li vede TUTTI. Il nuovo
-- menu Party mostra a ogni membro una riga per PG del gruppo con SOLE stat sintetiche (nome,
-- specie/razza, classe, livello, CA, PF attuali/max, percezione passiva, velocità, nickname del
-- proprietario) tramite una RPC dedicata, non tramite una SELECT diretta sulla tabella.
--
-- Idempotente e rieseguibile: DROP POLICY IF EXISTS + CREATE POLICY, CREATE OR REPLACE FUNCTION.

-- =====================================================================================
-- 1. Restringe la lettura dei personaggi: proprietario o master, non più "qualunque membro".
-- =====================================================================================
DROP POLICY IF EXISTS "characters_select" ON "public"."characters";
CREATE POLICY "characters_select" ON "public"."characters"
    FOR SELECT USING (("owner_id" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id"));

-- ---------------------------------------------------------------------------------------
-- ANALISI D'IMPATTO A CASCATA (obbligatoria: le policy RLS si applicano anche alle
-- subquery dentro altre policy, non solo alle query dirette sulla tabella).
--
-- "inventory_select" e "character_spells_select" leggono characters con un EXISTS:
--   inventory_select:        EXISTS (SELECT 1 FROM characters c WHERE c.id = inventory.character_id
--                                     AND (c.owner_id = auth.uid() OR is_campaign_member(c.campaign_id)))
--   character_spells_select: stessa forma, su character_spells.character_id.
-- Quella SELECT 1 FROM characters non gira "a parte": è comunque un accesso alla tabella
-- characters fatto con lo stesso ruolo (authenticated) della query esterna, quindi SUBISCE anche
-- lei la RLS di characters_select appena ristretta. L'esistenza della riga richiede perciò,
-- ORA, contemporaneamente:
--   (a) la condizione scritta nella policy di inventory/character_spells (owner_id = auth.uid()
--       OR is_campaign_member(c.campaign_id) — invariata, non la tocchiamo in questa migrazione)
--   (b) la condizione IMPOSTA da characters_select (owner_id = auth.uid() OR
--       is_campaign_master(c.campaign_id))
-- (a) AND (b): per chi non è né proprietario né master, (b) è falsa anche quando (a) è vera
-- (is_campaign_member vero per qualunque membro) → la riga in characters resta invisibile alla
-- subquery → EXISTS falso → niente inventario/incantesimi del PG altrui per un giocatore. Per il
-- master (b) resta vera via is_campaign_master, quindi il master continua a vedere inventario e
-- incantesimi di TUTTI i PG della campagna, come richiesto. Effetto verificato riga per riga sullo
-- schema in 20260624225146_remote_schema.sql: NESSUNA modifica a inventory_select/
-- character_spells_select è quindi necessaria — cascata già corretta grazie al solo punto 1 sopra.
--
-- Le policy di SCRITTURA (inventory_insert/update/delete, character_spells_insert/update/delete)
-- già usavano is_campaign_master (non is_campaign_member) nella propria condizione: la stessa
-- cascata le AND-a con una condizione identica a quella già scritta, quindi non cambia nulla lì.
--
-- Impatto sul resto del client (verificato leggendo il codice, nessuna modifica necessaria):
-- - Pages/Combat.razor + Services/CombatImport.cs: l'unico punto che legge TUTTI i personaggi
--   della campagna (CharacterRepository.GetCharactersForCampaignAsync in ImportCharactersAsync) è
--   raggiungibile solo dal master (bottone e metodo dietro `if (!CurrentUser.IsMaster) return;`),
--   che soddisfa is_campaign_master → nessuna regressione sull'import in iniziativa.
-- - Pages/Home.razor: LEGGE characters (GetCharactersForCampaignAsync, per sapere se l'utente ha
--   già un personaggio e spuntare il passo del percorso guidato), ma è insensibile alla
--   restrizione perché filtra comunque su OwnerId = utente corrente: il giocatore riceve solo i
--   propri PG e la risposta non cambia; il master li riceve tutti e poi scarta gli altri. Nessuna
--   modifica necessaria. (Nota di efficienza, non di correttezza: per il master la chiamata
--   scarica ora l'intero elenco dei PG di campagna per rispondere a un booleano.)
-- - Services/CampaignExport.cs: esporta solo razze/classi/background/incantesimi/mostri
--   (CampaignCatalogs), mai characters → nessun impatto.
-- ---------------------------------------------------------------------------------------

-- =====================================================================================
-- 2. RPC "panoramica del gruppo": sole colonne sintetiche, solo per chi è membro della campagna.
--    SECURITY DEFINER con search_path fisso (senza, sarebbe una vulnerabilità: un search_path
--    mutabile permetterebbe a un oggetto con lo stesso nome in uno schema precedente nel path di
--    dirottare le chiamate a funzione fatte qui dentro senza schema-qualificarle).
-- =====================================================================================
CREATE OR REPLACE FUNCTION "public"."get_party_overview"("p_campaign_id" "uuid")
RETURNS TABLE (
    "character_id" "uuid",
    "name" "text",
    "race" "text",
    "class" "text",
    "level" integer,
    "armor_class" integer,
    "hit_points" integer,
    "max_hit_points" integer,
    "passive_perception" integer,
    "speed" integer,
    "owner_id" "uuid",
    "owner_nickname" "text"
)
    LANGUAGE "sql" STABLE SECURITY DEFINER
    SET "search_path" TO 'public', 'pg_temp'
    AS $$
    -- Percezione passiva ricalcolata qui (stessa formula di CharacterCalculations.GetPassivePerception
    -- lato client: 10 + mod(Saggezza) + bonus competenza se competente/expertise, bonus raddoppiato
    -- se entrambi) perché il perimetro voluto per questa RPC è "sole colonne sintetiche": niente
    -- saggezza o pallini di competenza grezzi restituiti al chiamante, solo il numero finale.
    WITH "calc" AS (
        SELECT
            "c"."id",
            "c"."name",
            "c"."race",
            "c"."class",
            "c"."level",
            "c"."armor_class",
            "c"."hit_points",
            "c"."max_hit_points",
            "c"."speed",
            "c"."owner_id",
            FLOOR(("c"."wisdom" - 10) / 2.0)::integer AS "wis_mod",
            (2 + FLOOR((LEAST(GREATEST("c"."level", 1), 20) - 1) / 4.0)::integer) AS "prof_bonus",
            "c"."prof_perception",
            "c"."exp_perception"
        FROM "public"."characters" "c"
        WHERE "c"."campaign_id" = "p_campaign_id"
          -- Guardia di appartenenza: un non-membro non ottiene righe (non un'eccezione, per
          -- coerenza con is_campaign_member/is_campaign_master, che rispondono false anziché
          -- sollevare). is_campaign_member è costante per la chiamata: o è vera per tutte le righe
          -- o per nessuna.
          AND "public"."is_campaign_member"("p_campaign_id")
    )
    SELECT
        "calc"."id",
        "calc"."name",
        "calc"."race",
        "calc"."class",
        "calc"."level",
        "calc"."armor_class",
        "calc"."hit_points",
        "calc"."max_hit_points",
        10 + "calc"."wis_mod"
            + (CASE WHEN "calc"."prof_perception" THEN "calc"."prof_bonus" ELSE 0 END)
            + (CASE WHEN "calc"."exp_perception" THEN "calc"."prof_bonus" ELSE 0 END),
        "calc"."speed",
        "calc"."owner_id",
        COALESCE("pr"."display_name", 'Utente')
    FROM "calc"
    LEFT JOIN "public"."profiles" "pr" ON "pr"."id" = "calc"."owner_id"
    ORDER BY "calc"."name";
$$;

ALTER FUNCTION "public"."get_party_overview"("p_campaign_id" "uuid") OWNER TO "postgres";

-- SECURITY DEFINER gira coi privilegi del proprietario (postgres): senza revoca esplicita,
-- l'esecuzione resta concessa a PUBLIC/anon per default di Postgres su CREATE FUNCTION.
REVOKE ALL ON FUNCTION "public"."get_party_overview"("p_campaign_id" "uuid") FROM PUBLIC;
REVOKE ALL ON FUNCTION "public"."get_party_overview"("p_campaign_id" "uuid") FROM "anon";
GRANT EXECUTE ON FUNCTION "public"."get_party_overview"("p_campaign_id" "uuid") TO "authenticated";
