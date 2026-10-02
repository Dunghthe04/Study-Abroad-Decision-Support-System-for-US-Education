#!/usr/bin/env bash
# Load advisor/data/processed/*.jsonl into the RAG knowledge base (runs inside the advisor container).
#   ./scripts/ingest-knowledge.sh            # dev stack
#   ./scripts/ingest-knowledge.sh prod       # production stack
set -euo pipefail

FILE=docker-compose.yml
[ "${1:-}" = "prod" ] && FILE=docker-compose.prod.yml

docker compose -f "$FILE" --env-file .env run --rm \
  -v "$(pwd)/advisor/data:/app/data:ro" advisor python -m scripts.ingest "${@:2}"
