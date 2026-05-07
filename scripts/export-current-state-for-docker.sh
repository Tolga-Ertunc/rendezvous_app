#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DB_DUMP_PATH="${ROOT_DIR}/docker/postgres/init/001-current-state.sql"
MEDIA_SOURCE="${MEDIA_SOURCE:-${ROOT_DIR}/src/Rendezvous.Api/App_Data/uploads}"
MEDIA_TARGET="${ROOT_DIR}/docker/media/uploads"

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGDATABASE="${PGDATABASE:-rendezvous_dev}"
PGUSER="${PGUSER:-${USER}}"

mkdir -p "$(dirname "${DB_DUMP_PATH}")" "${MEDIA_TARGET}"

pg_dump \
  --host "${PGHOST}" \
  --port "${PGPORT}" \
  --username "${PGUSER}" \
  --dbname "${PGDATABASE}" \
  --format plain \
  --clean \
  --if-exists \
  --no-owner \
  --no-privileges \
  --file "${DB_DUMP_PATH}"

find "${MEDIA_TARGET}" -mindepth 1 ! -name ".gitignore" -exec rm -rf {} +

if [ -d "${MEDIA_SOURCE}" ]; then
  cp -R "${MEDIA_SOURCE}/." "${MEDIA_TARGET}/"
fi

echo "Exported ${PGDATABASE} to ${DB_DUMP_PATH}"
echo "Copied media files from ${MEDIA_SOURCE} to ${MEDIA_TARGET}"
