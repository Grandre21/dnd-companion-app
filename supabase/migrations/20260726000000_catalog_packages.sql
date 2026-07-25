-- Modello 2024 + import dei dati — migrazioni additive.
-- Spec: docs/superpowers/specs/2026-07-25-modello-2024-import-dati-design.md
-- Nessuna migrazione di dati: personaggi e cataloghi esistenti restano intatti.
--
-- ATTENZIONE: si applica UNA SOLA VOLTA. Le colonne usano IF NOT EXISTS, ma vincoli e policy no
-- (PostgreSQL non lo prevede per ADD CONSTRAINT né per CREATE POLICY): rieseguire questo file su
-- un database dove è già passato fallisce a metà. Applicalo con `supabase db reset`, che riparte
-- da zero, oppure una sola volta a mano.

-- 1. Provenienza delle voci importate (§4.3).
ALTER TABLE "public"."races"    ADD COLUMN IF NOT EXISTS "source_id" text;
ALTER TABLE "public"."classes"  ADD COLUMN IF NOT EXISTS "source_id" text;
ALTER TABLE "public"."spells"   ADD COLUMN IF NOT EXISTS "source_id" text;
ALTER TABLE "public"."monsters" ADD COLUMN IF NOT EXISTS "source_id" text;

-- Una sola riga per provenienza in una campagna. Le righe digitate a mano hanno source_id NULL,
-- e in PostgreSQL più NULL non violano un UNIQUE: le righe esistenti non sono toccate.
ALTER TABLE "public"."races"
    ADD CONSTRAINT "races_campaign_source_key"    UNIQUE ("campaign_id", "source_id");
ALTER TABLE "public"."classes"
    ADD CONSTRAINT "classes_campaign_source_key"  UNIQUE ("campaign_id", "source_id");
ALTER TABLE "public"."spells"
    ADD CONSTRAINT "spells_campaign_source_key"   UNIQUE ("campaign_id", "source_id");
ALTER TABLE "public"."monsters"
    ADD CONSTRAINT "monsters_campaign_source_key" UNIQUE ("campaign_id", "source_id");

-- 2. Unità della velocità (§4.5). Default 'ft': le razze già inserite restano in piedi.
ALTER TABLE "public"."races"
    ADD COLUMN IF NOT EXISTS "speed_unit" text NOT NULL DEFAULT 'ft';

-- 3. Ripartizione dei bonus di background scelta dal giocatore (§4.7).
ALTER TABLE "public"."characters"
    ADD COLUMN IF NOT EXISTS "background_ability_choice" text;

-- 4. Catalogo dei background (§4.2).
CREATE TABLE IF NOT EXISTS "public"."backgrounds" (
    "id" uuid DEFAULT gen_random_uuid() NOT NULL,
    "name" text NOT NULL,
    "description" text,
    "ability_scores" text,
    "origin_feat" text,
    "skill_proficiencies" text,
    "tool_proficiency" text,
    "equipment" text,
    "source_id" text,
    "added_by" uuid,
    "campaign_id" uuid NOT NULL,
    "created_at" timestamp with time zone DEFAULT now(),
    CONSTRAINT "backgrounds_pkey" PRIMARY KEY ("id"),
    CONSTRAINT "backgrounds_campaign_source_key" UNIQUE ("campaign_id", "source_id")
);

ALTER TABLE "public"."backgrounds"
    ADD CONSTRAINT "backgrounds_campaign_id_fkey" FOREIGN KEY ("campaign_id")
    REFERENCES "public"."campaigns"("id") ON DELETE CASCADE;

ALTER TABLE "public"."backgrounds"
    ADD CONSTRAINT "backgrounds_added_by_fkey" FOREIGN KEY ("added_by")
    REFERENCES "auth"."users"("id") ON DELETE SET NULL;

ALTER TABLE "public"."backgrounds" ENABLE ROW LEVEL SECURITY;

-- Policy ricalcate su quelle di races: lettura ai membri della campagna, inserimento a qualunque
-- membro che si dichiari autore, modifica e cancellazione all'autore o al master.
CREATE POLICY "backgrounds_select" ON "public"."backgrounds"
    FOR SELECT USING ("public"."is_campaign_member"("campaign_id"));

CREATE POLICY "backgrounds_insert" ON "public"."backgrounds"
    FOR INSERT WITH CHECK (
        "added_by" = "auth"."uid"() AND "public"."is_campaign_member"("campaign_id")
    );

-- La WITH CHECK non è decorativa: la USING dice quali righe si possono aggiornare, la WITH CHECK
-- vincola i valori risultanti. Senza, un membro potrebbe aggiornare una propria riga riscrivendo
-- campaign_id verso una campagna di cui non fa parte. races_update ha entrambe: ricalcarla vuol
-- dire copiarle tutte e due.
CREATE POLICY "backgrounds_update" ON "public"."backgrounds"
    FOR UPDATE USING (
        "added_by" = "auth"."uid"() OR "public"."is_campaign_master"("campaign_id")
    ) WITH CHECK (
        "added_by" = "auth"."uid"() OR "public"."is_campaign_master"("campaign_id")
    );

CREATE POLICY "backgrounds_delete" ON "public"."backgrounds"
    FOR DELETE USING (
        "added_by" = "auth"."uid"() OR "public"."is_campaign_master"("campaign_id")
    );
