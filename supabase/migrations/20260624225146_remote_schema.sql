


SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;


COMMENT ON SCHEMA "public" IS 'standard public schema';



CREATE EXTENSION IF NOT EXISTS "pg_stat_statements" WITH SCHEMA "extensions";






CREATE EXTENSION IF NOT EXISTS "pgcrypto" WITH SCHEMA "extensions";






CREATE EXTENSION IF NOT EXISTS "supabase_vault" WITH SCHEMA "vault";






CREATE EXTENSION IF NOT EXISTS "uuid-ossp" WITH SCHEMA "extensions";






CREATE OR REPLACE FUNCTION "public"."find_campaign_by_invite_code"("p_code" "text") RETURNS "uuid"
    LANGUAGE "sql" STABLE SECURITY DEFINER
    SET "search_path" TO 'public'
    AS $$
    SELECT id FROM public.campaigns WHERE invite_code = p_code LIMIT 1;
$$;


ALTER FUNCTION "public"."find_campaign_by_invite_code"("p_code" "text") OWNER TO "postgres";


CREATE OR REPLACE FUNCTION "public"."is_campaign_master"("p_campaign_id" "uuid") RETURNS boolean
    LANGUAGE "sql" STABLE SECURITY DEFINER
    SET "search_path" TO 'public'
    AS $$
    SELECT EXISTS (
        SELECT 1 FROM public.campaign_members
        WHERE campaign_id = p_campaign_id
          AND user_id = auth.uid()
          AND role = 'master'
    );
$$;


ALTER FUNCTION "public"."is_campaign_master"("p_campaign_id" "uuid") OWNER TO "postgres";


CREATE OR REPLACE FUNCTION "public"."is_campaign_member"("p_campaign_id" "uuid") RETURNS boolean
    LANGUAGE "sql" STABLE SECURITY DEFINER
    SET "search_path" TO 'public'
    AS $$
    SELECT EXISTS (
        SELECT 1 FROM public.campaign_members
        WHERE campaign_id = p_campaign_id
          AND user_id = auth.uid()
    );
$$;


ALTER FUNCTION "public"."is_campaign_member"("p_campaign_id" "uuid") OWNER TO "postgres";


CREATE OR REPLACE FUNCTION "public"."join_campaign"("p_code" "text") RETURNS "uuid"
    LANGUAGE "plpgsql" SECURITY DEFINER
    SET "search_path" TO 'public'
    AS $$
declare v_campaign uuid;
begin
  select id into v_campaign from campaigns where invite_code = upper(trim(p_code));
  if v_campaign is null then return null; end if;           -- codice non valido
  insert into campaign_members (campaign_id, user_id, role, joined_at)
  values (v_campaign, auth.uid(), 'player', now())
  on conflict (campaign_id, user_id) do nothing;            -- già membro → no-op
  return v_campaign;
end; $$;


ALTER FUNCTION "public"."join_campaign"("p_code" "text") OWNER TO "postgres";


CREATE OR REPLACE FUNCTION "public"."rls_auto_enable"() RETURNS "event_trigger"
    LANGUAGE "plpgsql" SECURITY DEFINER
    SET "search_path" TO 'pg_catalog'
    AS $$
DECLARE
  cmd record;
BEGIN
  FOR cmd IN
    SELECT *
    FROM pg_event_trigger_ddl_commands()
    WHERE command_tag IN ('CREATE TABLE', 'CREATE TABLE AS', 'SELECT INTO')
      AND object_type IN ('table','partitioned table')
  LOOP
     IF cmd.schema_name IS NOT NULL AND cmd.schema_name IN ('public') AND cmd.schema_name NOT IN ('pg_catalog','information_schema') AND cmd.schema_name NOT LIKE 'pg_toast%' AND cmd.schema_name NOT LIKE 'pg_temp%' THEN
      BEGIN
        EXECUTE format('alter table if exists %s enable row level security', cmd.object_identity);
        RAISE LOG 'rls_auto_enable: enabled RLS on %', cmd.object_identity;
      EXCEPTION
        WHEN OTHERS THEN
          RAISE LOG 'rls_auto_enable: failed to enable RLS on %', cmd.object_identity;
      END;
     ELSE
        RAISE LOG 'rls_auto_enable: skip % (either system schema or not in enforced list: %.)', cmd.object_identity, cmd.schema_name;
     END IF;
  END LOOP;
END;
$$;


ALTER FUNCTION "public"."rls_auto_enable"() OWNER TO "postgres";

SET default_tablespace = '';

SET default_table_access_method = "heap";


CREATE TABLE IF NOT EXISTS "public"."campaign_members" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "campaign_id" "uuid" NOT NULL,
    "user_id" "uuid" NOT NULL,
    "role" "text" DEFAULT 'player'::"text" NOT NULL,
    "joined_at" timestamp with time zone DEFAULT "now"(),
    CONSTRAINT "campaign_members_role_check" CHECK (("role" = ANY (ARRAY['player'::"text", 'master'::"text"])))
);


ALTER TABLE "public"."campaign_members" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."campaigns" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "name" "text" NOT NULL,
    "description" "text",
    "owner_id" "uuid" NOT NULL,
    "invite_code" "text" NOT NULL,
    "created_at" timestamp with time zone DEFAULT "now"()
);


ALTER TABLE "public"."campaigns" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."character_spells" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "character_id" "uuid" NOT NULL,
    "spell_id" "uuid" NOT NULL,
    "is_prepared" boolean DEFAULT false,
    "created_at" timestamp with time zone DEFAULT "now"()
);


ALTER TABLE "public"."character_spells" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."characters" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "name" "text" NOT NULL,
    "class" "text",
    "race" "text",
    "level" integer DEFAULT 1,
    "hit_points" integer,
    "max_hit_points" integer,
    "armor_class" integer,
    "strength" integer,
    "dexterity" integer,
    "constitution" integer,
    "intelligence" integer,
    "wisdom" integer,
    "charisma" integer,
    "notes" "text",
    "created_at" timestamp with time zone DEFAULT "now"(),
    "background" "text",
    "subclass" "text",
    "alignment" "text",
    "experience_points" integer DEFAULT 0,
    "size" "text" DEFAULT 'Media'::"text",
    "speed" integer DEFAULT 9,
    "appearance" "text",
    "backstory" "text",
    "languages" "text",
    "temp_hit_points" integer DEFAULT 0,
    "hit_dice_max" "text",
    "hit_dice_spent" integer DEFAULT 0,
    "death_save_successes" integer DEFAULT 0,
    "death_save_failures" integer DEFAULT 0,
    "heroic_inspiration" boolean DEFAULT false,
    "prof_save_strength" boolean DEFAULT false,
    "prof_save_dexterity" boolean DEFAULT false,
    "prof_save_constitution" boolean DEFAULT false,
    "prof_save_intelligence" boolean DEFAULT false,
    "prof_save_wisdom" boolean DEFAULT false,
    "prof_save_charisma" boolean DEFAULT false,
    "prof_athletics" boolean DEFAULT false,
    "exp_athletics" boolean DEFAULT false,
    "prof_acrobatics" boolean DEFAULT false,
    "exp_acrobatics" boolean DEFAULT false,
    "prof_sleight_of_hand" boolean DEFAULT false,
    "exp_sleight_of_hand" boolean DEFAULT false,
    "prof_stealth" boolean DEFAULT false,
    "exp_stealth" boolean DEFAULT false,
    "prof_arcana" boolean DEFAULT false,
    "exp_arcana" boolean DEFAULT false,
    "prof_history" boolean DEFAULT false,
    "exp_history" boolean DEFAULT false,
    "prof_investigation" boolean DEFAULT false,
    "exp_investigation" boolean DEFAULT false,
    "prof_nature" boolean DEFAULT false,
    "exp_nature" boolean DEFAULT false,
    "prof_religion" boolean DEFAULT false,
    "exp_religion" boolean DEFAULT false,
    "prof_animal_handling" boolean DEFAULT false,
    "exp_animal_handling" boolean DEFAULT false,
    "prof_insight" boolean DEFAULT false,
    "exp_insight" boolean DEFAULT false,
    "prof_medicine" boolean DEFAULT false,
    "exp_medicine" boolean DEFAULT false,
    "prof_perception" boolean DEFAULT false,
    "exp_perception" boolean DEFAULT false,
    "prof_survival" boolean DEFAULT false,
    "exp_survival" boolean DEFAULT false,
    "prof_deception" boolean DEFAULT false,
    "exp_deception" boolean DEFAULT false,
    "prof_intimidation" boolean DEFAULT false,
    "exp_intimidation" boolean DEFAULT false,
    "prof_performance" boolean DEFAULT false,
    "exp_performance" boolean DEFAULT false,
    "prof_persuasion" boolean DEFAULT false,
    "exp_persuasion" boolean DEFAULT false,
    "species_traits" "text",
    "class_features" "text",
    "feats" "text",
    "copper_pieces" integer DEFAULT 0,
    "silver_pieces" integer DEFAULT 0,
    "electrum_pieces" integer DEFAULT 0,
    "gold_pieces" integer DEFAULT 0,
    "platinum_pieces" integer DEFAULT 0,
    "attuned_item_1" "text",
    "attuned_item_2" "text",
    "attuned_item_3" "text",
    "spellcasting_ability" "text",
    "spell_slots_1_max" integer DEFAULT 0,
    "spell_slots_1_used" integer DEFAULT 0,
    "spell_slots_2_max" integer DEFAULT 0,
    "spell_slots_2_used" integer DEFAULT 0,
    "spell_slots_3_max" integer DEFAULT 0,
    "spell_slots_3_used" integer DEFAULT 0,
    "spell_slots_4_max" integer DEFAULT 0,
    "spell_slots_4_used" integer DEFAULT 0,
    "spell_slots_5_max" integer DEFAULT 0,
    "spell_slots_5_used" integer DEFAULT 0,
    "spell_slots_6_max" integer DEFAULT 0,
    "spell_slots_6_used" integer DEFAULT 0,
    "spell_slots_7_max" integer DEFAULT 0,
    "spell_slots_7_used" integer DEFAULT 0,
    "spell_slots_8_max" integer DEFAULT 0,
    "spell_slots_8_used" integer DEFAULT 0,
    "spell_slots_9_max" integer DEFAULT 0,
    "spell_slots_9_used" integer DEFAULT 0,
    "damage_resistances" "text",
    "damage_immunities" "text",
    "damage_vulnerabilities" "text",
    "condition_immunities" "text",
    "owner_id" "uuid" NOT NULL,
    "campaign_id" "uuid" NOT NULL
);


ALTER TABLE "public"."characters" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."classes" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "name" "text" NOT NULL,
    "description" "text",
    "hit_die" "text",
    "primary_ability" "text",
    "saving_throws" "text",
    "armor_proficiencies" "text",
    "weapon_proficiencies" "text",
    "skill_choices" "text",
    "features" "text",
    "created_at" timestamp with time zone DEFAULT "now"(),
    "added_by" "uuid",
    "campaign_id" "uuid" NOT NULL
);


ALTER TABLE "public"."classes" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."combat_state" (
    "campaign_id" "uuid" NOT NULL,
    "combatants" "jsonb" DEFAULT '[]'::"jsonb" NOT NULL,
    "current_turn_index" integer DEFAULT 0 NOT NULL,
    "round_number" integer DEFAULT 1 NOT NULL,
    "updated_at" timestamp with time zone DEFAULT "now"() NOT NULL
);


ALTER TABLE "public"."combat_state" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."inventory" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "character_id" "uuid" NOT NULL,
    "name" "text" NOT NULL,
    "quantity" integer DEFAULT 1,
    "weight" numeric(6,2),
    "description" "text",
    "item_type" "text",
    "is_equipped" boolean DEFAULT false,
    "created_at" timestamp with time zone DEFAULT "now"(),
    "attack_bonus" "text",
    "damage" "text",
    "damage_type" "text",
    "attack_notes" "text"
);


ALTER TABLE "public"."inventory" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."monsters" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "name" "text" NOT NULL,
    "size" "text",
    "type" "text",
    "alignment" "text",
    "armor_class" integer,
    "hit_points" "text",
    "speed" "text",
    "strength" integer,
    "dexterity" integer,
    "constitution" integer,
    "intelligence" integer,
    "wisdom" integer,
    "charisma" integer,
    "challenge_rating" "text",
    "abilities" "text",
    "description" "text",
    "added_by" "uuid",
    "campaign_id" "uuid" NOT NULL
);


ALTER TABLE "public"."monsters" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."notes" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "title" "text" NOT NULL,
    "content" "text",
    "is_shared" boolean DEFAULT false,
    "created_at" timestamp with time zone DEFAULT "now"(),
    "updated_at" timestamp with time zone DEFAULT "now"(),
    "owner_id" "uuid" NOT NULL,
    "campaign_id" "uuid" NOT NULL
);


ALTER TABLE "public"."notes" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."profiles" (
    "id" "uuid" NOT NULL,
    "display_name" "text",
    "created_at" timestamp with time zone DEFAULT "now"()
);


ALTER TABLE "public"."profiles" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."races" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "name" "text" NOT NULL,
    "description" "text",
    "speed" integer DEFAULT 30,
    "str_bonus" integer DEFAULT 0,
    "dex_bonus" integer DEFAULT 0,
    "con_bonus" integer DEFAULT 0,
    "int_bonus" integer DEFAULT 0,
    "wis_bonus" integer DEFAULT 0,
    "cha_bonus" integer DEFAULT 0,
    "traits" "text",
    "languages" "text",
    "created_at" timestamp with time zone DEFAULT "now"(),
    "added_by" "uuid",
    "campaign_id" "uuid" NOT NULL
);


ALTER TABLE "public"."races" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."spells" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "name" "text" NOT NULL,
    "level" integer DEFAULT 0 NOT NULL,
    "school" "text",
    "casting_time" "text",
    "range" "text",
    "components" "text",
    "duration" "text",
    "description" "text",
    "classes" "text",
    "added_by" "uuid",
    "campaign_id" "uuid" NOT NULL
);


ALTER TABLE "public"."spells" OWNER TO "postgres";


ALTER TABLE ONLY "public"."campaign_members"
    ADD CONSTRAINT "campaign_members_campaign_id_user_id_key" UNIQUE ("campaign_id", "user_id");



ALTER TABLE ONLY "public"."campaign_members"
    ADD CONSTRAINT "campaign_members_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."campaigns"
    ADD CONSTRAINT "campaigns_invite_code_key" UNIQUE ("invite_code");



ALTER TABLE ONLY "public"."campaigns"
    ADD CONSTRAINT "campaigns_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."character_spells"
    ADD CONSTRAINT "character_spells_character_id_spell_id_key" UNIQUE ("character_id", "spell_id");



ALTER TABLE ONLY "public"."character_spells"
    ADD CONSTRAINT "character_spells_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."characters"
    ADD CONSTRAINT "characters_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."classes"
    ADD CONSTRAINT "classes_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."combat_state"
    ADD CONSTRAINT "combat_state_pkey" PRIMARY KEY ("campaign_id");



ALTER TABLE ONLY "public"."inventory"
    ADD CONSTRAINT "inventory_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."monsters"
    ADD CONSTRAINT "monsters_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."notes"
    ADD CONSTRAINT "notes_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."profiles"
    ADD CONSTRAINT "profiles_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."races"
    ADD CONSTRAINT "races_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."spells"
    ADD CONSTRAINT "spells_pkey" PRIMARY KEY ("id");



CREATE INDEX "idx_campaign_members_campaign" ON "public"."campaign_members" USING "btree" ("campaign_id");



CREATE INDEX "idx_campaign_members_user" ON "public"."campaign_members" USING "btree" ("user_id");



CREATE INDEX "idx_characters_campaign" ON "public"."characters" USING "btree" ("campaign_id");



CREATE INDEX "idx_characters_owner" ON "public"."characters" USING "btree" ("owner_id");



CREATE INDEX "idx_classes_campaign" ON "public"."classes" USING "btree" ("campaign_id");



CREATE INDEX "idx_monsters_campaign" ON "public"."monsters" USING "btree" ("campaign_id");



CREATE INDEX "idx_notes_campaign" ON "public"."notes" USING "btree" ("campaign_id");



CREATE INDEX "idx_notes_owner" ON "public"."notes" USING "btree" ("owner_id");



CREATE INDEX "idx_races_campaign" ON "public"."races" USING "btree" ("campaign_id");



CREATE INDEX "idx_spells_campaign" ON "public"."spells" USING "btree" ("campaign_id");



ALTER TABLE ONLY "public"."campaign_members"
    ADD CONSTRAINT "campaign_members_campaign_id_fkey" FOREIGN KEY ("campaign_id") REFERENCES "public"."campaigns"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."campaign_members"
    ADD CONSTRAINT "campaign_members_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "auth"."users"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."campaigns"
    ADD CONSTRAINT "campaigns_owner_id_fkey" FOREIGN KEY ("owner_id") REFERENCES "auth"."users"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."character_spells"
    ADD CONSTRAINT "character_spells_character_id_fkey" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."character_spells"
    ADD CONSTRAINT "character_spells_spell_id_fkey" FOREIGN KEY ("spell_id") REFERENCES "public"."spells"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."characters"
    ADD CONSTRAINT "characters_campaign_id_fkey" FOREIGN KEY ("campaign_id") REFERENCES "public"."campaigns"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."characters"
    ADD CONSTRAINT "characters_owner_id_fkey" FOREIGN KEY ("owner_id") REFERENCES "auth"."users"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."classes"
    ADD CONSTRAINT "classes_added_by_fkey" FOREIGN KEY ("added_by") REFERENCES "auth"."users"("id") ON DELETE SET NULL;



ALTER TABLE ONLY "public"."classes"
    ADD CONSTRAINT "classes_campaign_id_fkey" FOREIGN KEY ("campaign_id") REFERENCES "public"."campaigns"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."combat_state"
    ADD CONSTRAINT "combat_state_campaign_id_fkey" FOREIGN KEY ("campaign_id") REFERENCES "public"."campaigns"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."inventory"
    ADD CONSTRAINT "inventory_character_id_fkey" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."monsters"
    ADD CONSTRAINT "monsters_added_by_fkey" FOREIGN KEY ("added_by") REFERENCES "auth"."users"("id") ON DELETE SET NULL;



ALTER TABLE ONLY "public"."monsters"
    ADD CONSTRAINT "monsters_campaign_id_fkey" FOREIGN KEY ("campaign_id") REFERENCES "public"."campaigns"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."notes"
    ADD CONSTRAINT "notes_campaign_id_fkey" FOREIGN KEY ("campaign_id") REFERENCES "public"."campaigns"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."notes"
    ADD CONSTRAINT "notes_owner_id_fkey" FOREIGN KEY ("owner_id") REFERENCES "auth"."users"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."profiles"
    ADD CONSTRAINT "profiles_id_fkey" FOREIGN KEY ("id") REFERENCES "auth"."users"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."races"
    ADD CONSTRAINT "races_added_by_fkey" FOREIGN KEY ("added_by") REFERENCES "auth"."users"("id") ON DELETE SET NULL;



ALTER TABLE ONLY "public"."races"
    ADD CONSTRAINT "races_campaign_id_fkey" FOREIGN KEY ("campaign_id") REFERENCES "public"."campaigns"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."spells"
    ADD CONSTRAINT "spells_added_by_fkey" FOREIGN KEY ("added_by") REFERENCES "auth"."users"("id") ON DELETE SET NULL;



ALTER TABLE ONLY "public"."spells"
    ADD CONSTRAINT "spells_campaign_id_fkey" FOREIGN KEY ("campaign_id") REFERENCES "public"."campaigns"("id") ON DELETE CASCADE;



ALTER TABLE "public"."campaign_members" ENABLE ROW LEVEL SECURITY;


CREATE POLICY "campaign_members_delete" ON "public"."campaign_members" FOR DELETE USING ((("user_id" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id")));



CREATE POLICY "campaign_members_insert" ON "public"."campaign_members" FOR INSERT WITH CHECK ((("user_id" = "auth"."uid"()) AND ("role" = 'master'::"text") AND (EXISTS ( SELECT 1
   FROM "public"."campaigns" "c"
  WHERE (("c"."id" = "campaign_members"."campaign_id") AND ("c"."owner_id" = "auth"."uid"()))))));



CREATE POLICY "campaign_members_select" ON "public"."campaign_members" FOR SELECT USING ((("user_id" = "auth"."uid"()) OR "public"."is_campaign_member"("campaign_id")));



ALTER TABLE "public"."campaigns" ENABLE ROW LEVEL SECURITY;


CREATE POLICY "campaigns_delete" ON "public"."campaigns" FOR DELETE USING (("auth"."uid"() = "owner_id"));



CREATE POLICY "campaigns_insert" ON "public"."campaigns" FOR INSERT WITH CHECK (("auth"."uid"() = "owner_id"));



CREATE POLICY "campaigns_select" ON "public"."campaigns" FOR SELECT USING (("public"."is_campaign_member"("id") OR ("owner_id" = "auth"."uid"())));



CREATE POLICY "campaigns_update" ON "public"."campaigns" FOR UPDATE USING (("auth"."uid"() = "owner_id")) WITH CHECK (("auth"."uid"() = "owner_id"));



ALTER TABLE "public"."character_spells" ENABLE ROW LEVEL SECURITY;


CREATE POLICY "character_spells_delete" ON "public"."character_spells" FOR DELETE USING ((EXISTS ( SELECT 1
   FROM "public"."characters" "c"
  WHERE (("c"."id" = "character_spells"."character_id") AND (("c"."owner_id" = "auth"."uid"()) OR "public"."is_campaign_master"("c"."campaign_id"))))));



CREATE POLICY "character_spells_insert" ON "public"."character_spells" FOR INSERT WITH CHECK ((EXISTS ( SELECT 1
   FROM "public"."characters" "c"
  WHERE (("c"."id" = "character_spells"."character_id") AND (("c"."owner_id" = "auth"."uid"()) OR "public"."is_campaign_master"("c"."campaign_id"))))));



CREATE POLICY "character_spells_select" ON "public"."character_spells" FOR SELECT USING ((EXISTS ( SELECT 1
   FROM "public"."characters" "c"
  WHERE (("c"."id" = "character_spells"."character_id") AND (("c"."owner_id" = "auth"."uid"()) OR "public"."is_campaign_member"("c"."campaign_id"))))));



CREATE POLICY "character_spells_update" ON "public"."character_spells" FOR UPDATE USING ((EXISTS ( SELECT 1
   FROM "public"."characters" "c"
  WHERE (("c"."id" = "character_spells"."character_id") AND (("c"."owner_id" = "auth"."uid"()) OR "public"."is_campaign_master"("c"."campaign_id"))))));



ALTER TABLE "public"."characters" ENABLE ROW LEVEL SECURITY;


CREATE POLICY "characters_delete" ON "public"."characters" FOR DELETE USING ((("owner_id" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id")));



CREATE POLICY "characters_insert" ON "public"."characters" FOR INSERT WITH CHECK ((("owner_id" = "auth"."uid"()) AND "public"."is_campaign_member"("campaign_id")));



CREATE POLICY "characters_select" ON "public"."characters" FOR SELECT USING ((("owner_id" = "auth"."uid"()) OR "public"."is_campaign_member"("campaign_id")));



CREATE POLICY "characters_update" ON "public"."characters" FOR UPDATE USING ((("owner_id" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id"))) WITH CHECK ((("owner_id" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id")));



ALTER TABLE "public"."classes" ENABLE ROW LEVEL SECURITY;


CREATE POLICY "classes_delete" ON "public"."classes" FOR DELETE USING ((("added_by" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id")));



CREATE POLICY "classes_insert" ON "public"."classes" FOR INSERT WITH CHECK ((("added_by" = "auth"."uid"()) AND "public"."is_campaign_member"("campaign_id")));



CREATE POLICY "classes_select" ON "public"."classes" FOR SELECT USING ("public"."is_campaign_member"("campaign_id"));



CREATE POLICY "classes_update" ON "public"."classes" FOR UPDATE USING ((("added_by" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id"))) WITH CHECK ((("added_by" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id")));



ALTER TABLE "public"."combat_state" ENABLE ROW LEVEL SECURITY;


CREATE POLICY "combat_state_insert" ON "public"."combat_state" FOR INSERT WITH CHECK ("public"."is_campaign_master"("campaign_id"));



CREATE POLICY "combat_state_select" ON "public"."combat_state" FOR SELECT USING ("public"."is_campaign_member"("campaign_id"));



CREATE POLICY "combat_state_update" ON "public"."combat_state" FOR UPDATE USING ("public"."is_campaign_master"("campaign_id")) WITH CHECK ("public"."is_campaign_master"("campaign_id"));



ALTER TABLE "public"."inventory" ENABLE ROW LEVEL SECURITY;


CREATE POLICY "inventory_delete" ON "public"."inventory" FOR DELETE USING ((EXISTS ( SELECT 1
   FROM "public"."characters" "c"
  WHERE (("c"."id" = "inventory"."character_id") AND (("c"."owner_id" = "auth"."uid"()) OR "public"."is_campaign_master"("c"."campaign_id"))))));



CREATE POLICY "inventory_insert" ON "public"."inventory" FOR INSERT WITH CHECK ((EXISTS ( SELECT 1
   FROM "public"."characters" "c"
  WHERE (("c"."id" = "inventory"."character_id") AND (("c"."owner_id" = "auth"."uid"()) OR "public"."is_campaign_master"("c"."campaign_id"))))));



CREATE POLICY "inventory_select" ON "public"."inventory" FOR SELECT USING ((EXISTS ( SELECT 1
   FROM "public"."characters" "c"
  WHERE (("c"."id" = "inventory"."character_id") AND (("c"."owner_id" = "auth"."uid"()) OR "public"."is_campaign_member"("c"."campaign_id"))))));



CREATE POLICY "inventory_update" ON "public"."inventory" FOR UPDATE USING ((EXISTS ( SELECT 1
   FROM "public"."characters" "c"
  WHERE (("c"."id" = "inventory"."character_id") AND (("c"."owner_id" = "auth"."uid"()) OR "public"."is_campaign_master"("c"."campaign_id"))))));



ALTER TABLE "public"."monsters" ENABLE ROW LEVEL SECURITY;


CREATE POLICY "monsters_delete" ON "public"."monsters" FOR DELETE USING ((("added_by" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id")));



CREATE POLICY "monsters_insert" ON "public"."monsters" FOR INSERT WITH CHECK ((("added_by" = "auth"."uid"()) AND "public"."is_campaign_member"("campaign_id")));



CREATE POLICY "monsters_select" ON "public"."monsters" FOR SELECT USING ("public"."is_campaign_member"("campaign_id"));



CREATE POLICY "monsters_update" ON "public"."monsters" FOR UPDATE USING ((("added_by" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id"))) WITH CHECK ((("added_by" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id")));



ALTER TABLE "public"."notes" ENABLE ROW LEVEL SECURITY;


CREATE POLICY "notes_delete" ON "public"."notes" FOR DELETE USING (("owner_id" = "auth"."uid"()));



CREATE POLICY "notes_insert" ON "public"."notes" FOR INSERT WITH CHECK ((("owner_id" = "auth"."uid"()) AND "public"."is_campaign_member"("campaign_id")));



CREATE POLICY "notes_select" ON "public"."notes" FOR SELECT USING ((("owner_id" = "auth"."uid"()) OR (("is_shared" = true) AND "public"."is_campaign_member"("campaign_id"))));



CREATE POLICY "notes_update" ON "public"."notes" FOR UPDATE USING (("owner_id" = "auth"."uid"())) WITH CHECK (("owner_id" = "auth"."uid"()));



ALTER TABLE "public"."profiles" ENABLE ROW LEVEL SECURITY;


CREATE POLICY "profiles_insert" ON "public"."profiles" FOR INSERT WITH CHECK (("id" = "auth"."uid"()));



CREATE POLICY "profiles_select" ON "public"."profiles" FOR SELECT USING ((("id" = "auth"."uid"()) OR (EXISTS ( SELECT 1
   FROM ("public"."campaign_members" "cm1"
     JOIN "public"."campaign_members" "cm2" ON (("cm1"."campaign_id" = "cm2"."campaign_id")))
  WHERE (("cm1"."user_id" = "auth"."uid"()) AND ("cm2"."user_id" = "profiles"."id"))))));



CREATE POLICY "profiles_update" ON "public"."profiles" FOR UPDATE USING (("id" = "auth"."uid"())) WITH CHECK (("id" = "auth"."uid"()));



ALTER TABLE "public"."races" ENABLE ROW LEVEL SECURITY;


CREATE POLICY "races_delete" ON "public"."races" FOR DELETE USING ((("added_by" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id")));



CREATE POLICY "races_insert" ON "public"."races" FOR INSERT WITH CHECK ((("added_by" = "auth"."uid"()) AND "public"."is_campaign_member"("campaign_id")));



CREATE POLICY "races_select" ON "public"."races" FOR SELECT USING ("public"."is_campaign_member"("campaign_id"));



CREATE POLICY "races_update" ON "public"."races" FOR UPDATE USING ((("added_by" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id"))) WITH CHECK ((("added_by" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id")));



ALTER TABLE "public"."spells" ENABLE ROW LEVEL SECURITY;


CREATE POLICY "spells_delete" ON "public"."spells" FOR DELETE USING ((("added_by" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id")));



CREATE POLICY "spells_insert" ON "public"."spells" FOR INSERT WITH CHECK ((("added_by" = "auth"."uid"()) AND "public"."is_campaign_member"("campaign_id")));



CREATE POLICY "spells_select" ON "public"."spells" FOR SELECT USING ("public"."is_campaign_member"("campaign_id"));



CREATE POLICY "spells_update" ON "public"."spells" FOR UPDATE USING ((("added_by" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id"))) WITH CHECK ((("added_by" = "auth"."uid"()) OR "public"."is_campaign_master"("campaign_id")));





ALTER PUBLICATION "supabase_realtime" OWNER TO "postgres";


GRANT USAGE ON SCHEMA "public" TO "postgres";
GRANT USAGE ON SCHEMA "public" TO "anon";
GRANT USAGE ON SCHEMA "public" TO "authenticated";
GRANT USAGE ON SCHEMA "public" TO "service_role";






















































































































































GRANT ALL ON FUNCTION "public"."find_campaign_by_invite_code"("p_code" "text") TO "anon";
GRANT ALL ON FUNCTION "public"."find_campaign_by_invite_code"("p_code" "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."find_campaign_by_invite_code"("p_code" "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."is_campaign_master"("p_campaign_id" "uuid") TO "anon";
GRANT ALL ON FUNCTION "public"."is_campaign_master"("p_campaign_id" "uuid") TO "authenticated";
GRANT ALL ON FUNCTION "public"."is_campaign_master"("p_campaign_id" "uuid") TO "service_role";



GRANT ALL ON FUNCTION "public"."is_campaign_member"("p_campaign_id" "uuid") TO "anon";
GRANT ALL ON FUNCTION "public"."is_campaign_member"("p_campaign_id" "uuid") TO "authenticated";
GRANT ALL ON FUNCTION "public"."is_campaign_member"("p_campaign_id" "uuid") TO "service_role";



GRANT ALL ON FUNCTION "public"."join_campaign"("p_code" "text") TO "anon";
GRANT ALL ON FUNCTION "public"."join_campaign"("p_code" "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."join_campaign"("p_code" "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."rls_auto_enable"() TO "anon";
GRANT ALL ON FUNCTION "public"."rls_auto_enable"() TO "authenticated";
GRANT ALL ON FUNCTION "public"."rls_auto_enable"() TO "service_role";


















GRANT ALL ON TABLE "public"."campaign_members" TO "anon";
GRANT ALL ON TABLE "public"."campaign_members" TO "authenticated";
GRANT ALL ON TABLE "public"."campaign_members" TO "service_role";



GRANT ALL ON TABLE "public"."campaigns" TO "anon";
GRANT ALL ON TABLE "public"."campaigns" TO "authenticated";
GRANT ALL ON TABLE "public"."campaigns" TO "service_role";



GRANT ALL ON TABLE "public"."character_spells" TO "anon";
GRANT ALL ON TABLE "public"."character_spells" TO "authenticated";
GRANT ALL ON TABLE "public"."character_spells" TO "service_role";



GRANT ALL ON TABLE "public"."characters" TO "anon";
GRANT ALL ON TABLE "public"."characters" TO "authenticated";
GRANT ALL ON TABLE "public"."characters" TO "service_role";



GRANT ALL ON TABLE "public"."classes" TO "anon";
GRANT ALL ON TABLE "public"."classes" TO "authenticated";
GRANT ALL ON TABLE "public"."classes" TO "service_role";



GRANT ALL ON TABLE "public"."combat_state" TO "anon";
GRANT ALL ON TABLE "public"."combat_state" TO "authenticated";
GRANT ALL ON TABLE "public"."combat_state" TO "service_role";



GRANT ALL ON TABLE "public"."inventory" TO "anon";
GRANT ALL ON TABLE "public"."inventory" TO "authenticated";
GRANT ALL ON TABLE "public"."inventory" TO "service_role";



GRANT ALL ON TABLE "public"."monsters" TO "anon";
GRANT ALL ON TABLE "public"."monsters" TO "authenticated";
GRANT ALL ON TABLE "public"."monsters" TO "service_role";



GRANT ALL ON TABLE "public"."notes" TO "anon";
GRANT ALL ON TABLE "public"."notes" TO "authenticated";
GRANT ALL ON TABLE "public"."notes" TO "service_role";



GRANT ALL ON TABLE "public"."profiles" TO "anon";
GRANT ALL ON TABLE "public"."profiles" TO "authenticated";
GRANT ALL ON TABLE "public"."profiles" TO "service_role";



GRANT ALL ON TABLE "public"."races" TO "anon";
GRANT ALL ON TABLE "public"."races" TO "authenticated";
GRANT ALL ON TABLE "public"."races" TO "service_role";



GRANT ALL ON TABLE "public"."spells" TO "anon";
GRANT ALL ON TABLE "public"."spells" TO "authenticated";
GRANT ALL ON TABLE "public"."spells" TO "service_role";









ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON SEQUENCES TO "postgres";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON SEQUENCES TO "anon";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON SEQUENCES TO "authenticated";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON SEQUENCES TO "service_role";






ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON FUNCTIONS TO "postgres";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON FUNCTIONS TO "anon";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON FUNCTIONS TO "authenticated";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON FUNCTIONS TO "service_role";






ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON TABLES TO "postgres";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON TABLES TO "anon";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON TABLES TO "authenticated";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON TABLES TO "service_role";



































