#!/usr/bin/env bash
# Verifica che le colonne dichiarate nei Model C# (Models/*.cs, [Table]/[Column]) esistano
# davvero sul database Supabase di produzione, interrogando PostgREST invece di fidarsi di
# una dichiarazione scritta a mano. Nato dall'incidente del 2026-08-08: sette colonne
# "applicate" solo sulla carta hanno rotto per due giorni ogni salvataggio di characters e
# inventory. Uso: bash supabase/verifica-schema.sh (dalla radice del repo). L'elenco delle
# colonne attese si deriva dai Model: non va mai scritto a mano in questo file.

set -u

CONFIG="wwwroot/appsettings.json"
MODELS_DIR="Models"

if ! command -v curl >/dev/null 2>&1; then
  echo "Errore: 'curl' non è disponibile in questo ambiente." >&2
  exit 2
fi

if [ ! -f "$CONFIG" ]; then
  echo "Errore: non trovo '$CONFIG'. Esegui lo script dalla radice del repo." >&2
  exit 2
fi

if [ ! -d "$MODELS_DIR" ]; then
  echo "Errore: non trovo la cartella '$MODELS_DIR'. Esegui lo script dalla radice del repo." >&2
  exit 2
fi

SUPABASE_URL=$(grep -m1 '"Url"' "$CONFIG" | sed -E 's/.*"Url"[[:space:]]*:[[:space:]]*"([^"]*)".*/\1/')
ANON_KEY=$(grep -m1 '"AnonKey"' "$CONFIG" | sed -E 's/.*"AnonKey"[[:space:]]*:[[:space:]]*"([^"]*)".*/\1/')
SUPABASE_URL="${SUPABASE_URL%/}"

if [ -z "$SUPABASE_URL" ] || [ -z "$ANON_KEY" ]; then
  echo "Errore: '$CONFIG' non contiene sia Supabase.Url che Supabase.AnonKey." >&2
  exit 2
fi

TMP_BODY="$(mktemp)"
trap 'rm -f "$TMP_BODY"' EXIT

TOTAL_TABLES=0
TABLES_WITH_ISSUES=0
TOTAL_MISSING_COLUMNS=0
HAD_INCONCLUSIVE=0

report_missing() {
  local table="$1"; shift
  local count=$#
  local joined
  joined=$(IFS=,; echo "$*")
  joined=$(printf '%s' "$joined" | sed 's/,/, /g')
  echo "❌ $table — mancano: $joined"
  TABLES_WITH_ISSUES=$((TABLES_WITH_ISSUES+1))
  TOTAL_MISSING_COLUMNS=$((TOTAL_MISSING_COLUMNS+count))
}

verify_table() {
  local file="$1"
  local table
  table=$(grep -m1 '\[Table("' "$file" | sed -E 's/.*\[Table\("([^"]+)"\).*/\1/')
  [ -z "$table" ] && return

  local columns=()
  while IFS= read -r col; do
    [ -n "$col" ] && columns+=("$col")
  done < <(grep -oE '\[Column\("[^"]+"\)' "$file" | sed -E 's/.*"([^"]+)".*/\1/')

  TOTAL_TABLES=$((TOTAL_TABLES+1))

  local total_columns=${#columns[@]}
  if [ "$total_columns" -eq 0 ]; then
    echo "⚠️  $table — nessuna colonna [Column] trovata in $file, verifica saltata."
    HAD_INCONCLUSIVE=1
    return
  fi

  local remaining=("${columns[@]}")
  local missing=()
  local iter=0

  while [ "${#remaining[@]}" -gt 0 ]; do
    iter=$((iter+1))
    if [ "$iter" -gt 60 ]; then
      echo "⚠️  $table — superate 60 iterazioni, verifica interrotta per questa tabella."
      HAD_INCONCLUSIVE=1
      return
    fi

    local select_param
    select_param=$(IFS=,; echo "${remaining[*]}")

    local http_code
    http_code=$(curl -sS --max-time 30 -o "$TMP_BODY" -w '%{http_code}' \
      -H "apikey: $ANON_KEY" \
      -H "Authorization: Bearer $ANON_KEY" \
      "$SUPABASE_URL/rest/v1/$table?select=$select_param&limit=0")
    local curl_rc=$?

    if [ "$curl_rc" -ne 0 ]; then
      echo "Errore di rete: curl non è riuscito a contattare $SUPABASE_URL (exit $curl_rc)." >&2
      exit 2
    fi

    if [ "$http_code" = "200" ]; then
      if [ "${#missing[@]}" -eq 0 ]; then
        echo "✅ $table ($total_columns colonne)"
      else
        report_missing "$table" "${missing[@]}"
      fi
      return
    fi

    local body
    body=$(cat "$TMP_BODY")

    if printf '%s' "$body" | grep -q 'PGRST205'; then
      echo "❌ $table — tabella assente sul server."
      TABLES_WITH_ISSUES=$((TABLES_WITH_ISSUES+1))
      return
    fi

    if ! printf '%s' "$body" | grep -qE 'does not exist|Could not find'; then
      echo "⚠️  $table — risposta HTTP $http_code non riconosciuta, verifica inconclusiva."
      HAD_INCONCLUSIVE=1
      return
    fi

    local bad_col
    bad_col=$(printf '%s' "$body" | grep -oE 'column [A-Za-z0-9_]+\.[A-Za-z0-9_]+ does not exist' | sed -E 's/.*\.([A-Za-z0-9_]+) does not exist/\1/')
    if [ -z "$bad_col" ]; then
      bad_col=$(printf '%s' "$body" | grep -oE "Could not find the '[A-Za-z0-9_]+' column" | sed -E "s/Could not find the '([A-Za-z0-9_]+)' column/\1/")
    fi
    if [ -z "$bad_col" ]; then
      echo "⚠️  $table — risposta HTTP $http_code non riconosciuta, verifica inconclusiva."
      HAD_INCONCLUSIVE=1
      return
    fi

    missing+=("$bad_col")
    local new_remaining=()
    local c
    for c in "${remaining[@]}"; do
      [ "$c" = "$bad_col" ] || new_remaining+=("$c")
    done
    remaining=("${new_remaining[@]}")
  done

  report_missing "$table" "${missing[@]}"
}

for file in "$MODELS_DIR"/*.cs; do
  if grep -q '\[Table("' "$file"; then
    verify_table "$file"
  fi
done

echo
if [ "$HAD_INCONCLUSIVE" -eq 1 ]; then
  echo "Verifica incompleta: una o più tabelle non sono state controllate fino in fondo (vedi avvisi sopra)."
  exit 2
elif [ "$TABLES_WITH_ISSUES" -gt 0 ]; then
  echo "MANCANO $TOTAL_MISSING_COLUMNS colonne su $TABLES_WITH_ISSUES tabelle (su $TOTAL_TABLES verificate)."
  exit 1
else
  echo "Schema allineato ai Model."
  exit 0
fi
