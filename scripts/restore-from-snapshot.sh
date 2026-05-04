#!/usr/bin/env bash
# restore-from-snapshot.sh
#
# Find or recover content from a Litestream snapshot of the Umbraco DB.
# Doesn't touch prod — only inspects historical state and writes locally.
#
# Usage:
#   bash scripts/restore-from-snapshot.sh list
#       List all available snapshots (generation IDs + timestamps).
#
#   bash scripts/restore-from-snapshot.sh dump <generation>
#       Restore the named generation locally and open a sqlite shell on it.
#
#   bash scripts/restore-from-snapshot.sh find <text-to-search>
#       Restore the latest snapshot and grep umbracoNode.text for matches.
#       Use this when you don't know which snapshot still has the content.
#
#   bash scripts/restore-from-snapshot.sh export <generation> <node-id> [output.sql]
#       Restore the named generation, then dump just the rows for the named
#       node (and its children) to a SQL file. Review before applying.
#
# Requirements:
#   - litestream v0.3.13 at /tmp/ls013/litestream  (this script downloads it
#     if missing, since the repo was on 0.3.x at the time of writing)
#   - Azure CLI logged in with PIM-active access to ki-norge resource group

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK_DIR="${TMPDIR:-/tmp}/ki-norge-snapshot-restore"
LITESTREAM="/tmp/ls013/litestream"

ensure_litestream() {
  if [ ! -x "$LITESTREAM" ]; then
    echo "==> Downloading litestream v0.3.13 (matches prod backup format)"
    mkdir -p /tmp/ls013
    curl -sL "https://github.com/benbjohnson/litestream/releases/download/v0.3.13/litestream-v0.3.13-darwin-arm64.zip" -o /tmp/ls013/ls.zip
    (cd /tmp/ls013 && unzip -o ls.zip >/dev/null && rm ls.zip)
  fi
}

ensure_config() {
  mkdir -p "$WORK_DIR"
  STORAGE_KEY=$(az storage account keys list --account-name kinorgestorage --resource-group ki-norge --query '[0].value' -o tsv 2>/dev/null)
  if [ -z "$STORAGE_KEY" ]; then
    echo "ERROR: could not get storage account key. Have you activated Azure PIM? Run 'npm run azure:activate' then 'az login'."
    exit 1
  fi
  cat > "$WORK_DIR/litestream.yml" <<EOF
dbs:
  - path: $WORK_DIR/anchor.db
    replicas:
      - type: abs
        bucket: umbraco-db
        account-name: kinorgestorage
        account-key: ${STORAGE_KEY}
        path: Umbraco.sqlite.db
EOF
}

cmd_list() {
  ensure_litestream
  ensure_config
  LITESTREAM_AZURE_ACCOUNT_KEY="$STORAGE_KEY" "$LITESTREAM" snapshots -config "$WORK_DIR/litestream.yml" "$WORK_DIR/anchor.db"
}

cmd_dump() {
  local gen="$1"
  ensure_litestream
  ensure_config
  local out="$WORK_DIR/$gen.db"
  LITESTREAM_AZURE_ACCOUNT_KEY="$STORAGE_KEY" "$LITESTREAM" restore -config "$WORK_DIR/litestream.yml" -generation "$gen" -o "$out" "$WORK_DIR/anchor.db" 2>&1 | tail -2
  echo ""
  echo "Restored to: $out"
  echo "Opening sqlite shell. Type .quit to exit."
  sqlite3 "$out"
}

cmd_find() {
  local needle="$1"
  ensure_litestream
  ensure_config
  echo "==> Listing snapshots (newest first)"
  local gens
  gens=$(LITESTREAM_AZURE_ACCOUNT_KEY="$STORAGE_KEY" "$LITESTREAM" snapshots -config "$WORK_DIR/litestream.yml" "$WORK_DIR/anchor.db" 2>/dev/null | tail -n +2 | awk '{print $2}')
  for gen in $gens; do
    local db="$WORK_DIR/$gen.db"
    if [ ! -f "$db" ]; then
      LITESTREAM_AZURE_ACCOUNT_KEY="$STORAGE_KEY" "$LITESTREAM" restore -config "$WORK_DIR/litestream.yml" -generation "$gen" -o "$db" "$WORK_DIR/anchor.db" 2>/dev/null || continue
    fi
    local hits
    hits=$(sqlite3 "$db" "SELECT id||':'||text||' (parent='||parentId||',trashed='||trashed||')' FROM umbracoNode WHERE text LIKE '%${needle}%' AND nodeObjectType IN ('C66BA18E-EAF3-4CFF-8A22-41B16D66A972','B796F64C-1F99-4FFB-B886-4BF4BC011A9C') ORDER BY id;" 2>/dev/null)
    if [ -n "$hits" ]; then
      echo ""
      echo "=== $gen ==="
      echo "$hits"
    fi
  done
}

cmd_export() {
  local gen="$1"
  local node_id="$2"
  local out="${3:-$WORK_DIR/restore-node-$node_id.sql}"
  ensure_litestream
  ensure_config
  local db="$WORK_DIR/$gen.db"
  if [ ! -f "$db" ]; then
    LITESTREAM_AZURE_ACCOUNT_KEY="$STORAGE_KEY" "$LITESTREAM" restore -config "$WORK_DIR/litestream.yml" -generation "$gen" -o "$db" "$WORK_DIR/anchor.db" 2>&1 | tail -2
  fi
  # Resolve descendants: anything whose path contains ",$node_id" or starts with "$node_id,"
  echo "-- Restored from snapshot $gen"                                       > "$out"
  echo "-- Node id $node_id and all descendants"                             >> "$out"
  echo "-- Review before applying. Existing rows with the same id will fail" >> "$out"
  echo "-- the INSERT — you may need to DELETE them first or rewrite ids."   >> "$out"
  echo ""                                                                    >> "$out"
  for table in umbracoNode umbracoContent umbracoContentVersion umbracoDocument umbracoDocumentVersion umbracoPropertyData; do
    case "$table" in
      umbracoNode)
        sqlite3 "$db" ".mode insert $table" "SELECT * FROM $table WHERE id = $node_id OR path LIKE '%,$node_id,%' OR path LIKE '%,$node_id';" >> "$out"
        ;;
      *)
        sqlite3 "$db" ".mode insert $table" "SELECT * FROM $table WHERE nodeId IN (SELECT id FROM umbracoNode WHERE id = $node_id OR path LIKE '%,$node_id,%' OR path LIKE '%,$node_id');" 2>/dev/null >> "$out" || true
        ;;
    esac
    echo "" >> "$out"
  done
  echo "Wrote $out"
  echo ""
  echo "Next steps if you want to apply this to prod:"
  echo "  1. Read the SQL — check it doesn't conflict with current rows"
  echo "  2. Use scripts/restore-from-snapshot.sh dump <current-gen> first to inspect prod"
  echo "  3. Apply via Litestream restore + sqlite3 < restore-node-$node_id.sql + Litestream replicate"
  echo "  4. Or just re-create the content via the editor; usually safer."
}

case "${1:-}" in
  list)   cmd_list ;;
  dump)   shift; cmd_dump "$@" ;;
  find)   shift; cmd_find "$@" ;;
  export) shift; cmd_export "$@" ;;
  *)
    cat <<USAGE
Usage:
  $0 list
  $0 dump <generation>
  $0 find <text>
  $0 export <generation> <node-id> [out.sql]
USAGE
    exit 1
    ;;
esac
