-- Fetta A di "La scheda, alla pari con la carta"
-- (docs/superpowers/specs/2026-08-06-scheda-alla-pari-con-la-carta-design.md): risorse di classe,
-- addestramento e i due flag dell'arma che alimentano il bonus d'attacco calcolato. Il "perché" di
-- ogni scelta (D1-D8) sta nello spec; qui solo lo schema.
--
-- ⚠️ APPLICAZIONE ALL'HOSTED — VINCOLO CRITICO (v. CLAUDE.md, sezione «main è il ramo di rilascio»).
-- Le sette colonne qui sotto entrano nei Model di QUESTO commit (Character.cs, InventoryItem.cs):
-- applicare questa migrazione al progetto Supabase hosted PRIMA del push corrispondente. Appena il
-- client nuovo è online, ogni Update/Insert di postgrest-csharp serializza TUTTE le colonne mappate
-- dal Model — se anche una sola di queste non esiste ancora sul server, PostgREST risponde 400 e
-- SALTA OGNI SCRITTURA su characters/inventory, non solo quelle che toccano i campi nuovi.
--
-- Esecuzione: incollare il corpo di questo file (sotto questo blocco di commenti) nell'SQL editor
-- del progetto Supabase hosted, oppure `supabase db push` puntato al progetto giusto.
--
-- Verifica dopo l'applicazione (deve restituire 7 righe):
--   SELECT table_name, column_name, data_type, column_default, is_nullable
--   FROM information_schema.columns
--   WHERE table_schema = 'public'
--     AND ( (table_name = 'characters' AND column_name IN
--             ('class_resources', 'armor_training', 'weapon_proficiencies', 'tool_proficiencies'))
--        OR (table_name = 'inventory' AND column_name IN
--             ('is_finesse', 'is_ranged', 'is_not_proficient')) )
--   ORDER BY table_name, column_name;
--
-- COMPATIBILITÀ COL CLIENT GIÀ ONLINE (il service worker non fa skipWaiting: i due client
-- convivono sullo stesso database per un tempo indefinito). Tutte additive, tutte con DEFAULT:
-- il client vecchio non dichiara queste colonne nei suoi Model, quindi in lettura Newtonsoft le
-- ignora (nessun [Column] che le mappi) e in scrittura i suoi Update/Insert non le nominano affatto
-- — non può azzerarle né romperle, perché non sa che esistono. Stesso schema già usato per
-- class_subclasses (20260801000000) e per class_resources qui sotto rispetto a combat_state.
--
-- Idempotente e rieseguibile: ADD COLUMN IF NOT EXISTS su ogni colonna, nessuna DDL distruttiva.

-- =====================================================================================
-- 1. characters: risorse di classe (jsonb) e addestramento (testo libero, D4 nello spec).
-- =====================================================================================

-- Risorse di classe (Ira, Ispirazione bardica, Focus del monaco, ...): jsonb, lista di oggetti
-- {Nome, Max, Spesi, Ricarica}, mappata a List<ClassResource> (Models/ClassResource.cs) con lo
-- stesso pattern di combat_state.combatants (D2). Letta/scritta da Services/ClassResourceRules.cs,
-- che tollera qualunque malformazione: un jsonb che non si capisce diventa lista vuota, mai
-- un'eccezione, mai una scheda che non si apre.
ALTER TABLE "public"."characters"
    ADD COLUMN IF NOT EXISTS "class_resources" jsonb DEFAULT '[]'::jsonb NOT NULL;

COMMENT ON COLUMN "public"."characters"."class_resources" IS
    'Risorse di classe con i loro usi (Ira, Ispirazione bardica, ...): jsonb, lista di oggetti {Nome, Max, Spesi, Ricarica}. Letta/scritta da Services/ClassResourceRules.cs, tollerante al malformato (v. spec 2026-08-06, "Le risorse di classe").';

-- Addestramento (EQUIPMENT TRAINING & PROFICIENCIES sulla carta): tre colonne di testo libero, non
-- una griglia di caselle (D4) — il dato non alimenta alcun calcolo e si consulta due volte a
-- campagna, quindi strutturarlo sarebbe il "modulo da compilare" che la richiesta vuole evitare.
ALTER TABLE "public"."characters" ADD COLUMN IF NOT EXISTS "armor_training" text;
ALTER TABLE "public"."characters" ADD COLUMN IF NOT EXISTS "weapon_proficiencies" text;
ALTER TABLE "public"."characters" ADD COLUMN IF NOT EXISTS "tool_proficiencies" text;

COMMENT ON COLUMN "public"."characters"."armor_training" IS
    'Addestramento con le armature, testo libero (D4): consultazione, non alimenta calcoli.';
COMMENT ON COLUMN "public"."characters"."weapon_proficiencies" IS
    'Competenze con le armi, testo libero (D4): consultazione, non alimenta calcoli.';
COMMENT ON COLUMN "public"."characters"."tool_proficiencies" IS
    'Competenze con gli strumenti, testo libero (D4): consultazione, non alimenta calcoli.';

-- =====================================================================================
-- 2. inventory: due flag per il bonus d'attacco calcolato (Services/WeaponCalculations.cs) più
--    l'eccezione di competenza (D6).
-- =====================================================================================

ALTER TABLE "public"."inventory"
    ADD COLUMN IF NOT EXISTS "is_finesse" boolean DEFAULT false NOT NULL;
ALTER TABLE "public"."inventory"
    ADD COLUMN IF NOT EXISTS "is_ranged" boolean DEFAULT false NOT NULL;
ALTER TABLE "public"."inventory"
    ADD COLUMN IF NOT EXISTS "is_not_proficient" boolean DEFAULT false NOT NULL;

COMMENT ON COLUMN "public"."inventory"."is_finesse" IS
    'Arma accurata (finesse): il bonus d''attacco calcolato usa il migliore fra Forza e Destrezza invece della sola Forza.';
COMMENT ON COLUMN "public"."inventory"."is_ranged" IS
    'Arma a distanza: il bonus d''attacco calcolato usa Destrezza invece di Forza.';
COMMENT ON COLUMN "public"."inventory"."is_not_proficient" IS
    'Eccezione a D6 (la competenza con l''arma si assume vera): se true, il bonus di competenza non entra nel calcolo del bonus d''attacco.';
