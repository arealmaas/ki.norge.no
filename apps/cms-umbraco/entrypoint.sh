#!/bin/sh
set -e

mkdir -p /app/umbraco/Data
chmod 777 /app/umbraco/Data

# Restore database from Azure Blob Storage (if a backup exists and no local DB yet)
litestream restore -if-replica-exists -if-db-not-exists /app/umbraco/Data/Umbraco.sqlite.db

# Start Umbraco under Litestream replication
exec litestream replicate -exec "dotnet KiNorge.Cms.dll"
