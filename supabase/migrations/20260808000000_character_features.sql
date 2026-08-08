-- Annotazioni dell'utente sui privilegi (v. docs/superpowers/specs/2026-08-08-vista-di-gioco-design.md).
-- I NOMI dei privilegi non stanno qui: si derivano dal pacchetto SRD a ogni render. Qui va solo ciò
-- che nessuna altra fonte può conoscere — le parole del giocatore.
-- Un jsonb e non cinque colonne: postgrest-csharp serializza OGNI colonna mappata a ogni Update,
-- quindi ogni colonna nuova è un'esposizione al difetto che il 2026-08-08 ha bloccato per due giorni
-- tutte le scritture su characters.
ALTER TABLE "public"."characters"
    ADD COLUMN IF NOT EXISTS "character_features" jsonb DEFAULT '[]'::jsonb NOT NULL;

COMMENT ON COLUMN "public"."characters"."character_features" IS
    'Annotazioni sui privilegi: nome (chiave), nota, tag di economia d''azione, risorsa collegata, attivabile.';
