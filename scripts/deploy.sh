#!/usr/bin/env bash
# Pull latest code and (re)deploy the production stack. Run on the VPS from the repo root.
set -euo pipefail

COMPOSE="docker compose -f docker-compose.prod.yml --env-file .env"

git pull --ff-only
$COMPOSE build
$COMPOSE up -d --remove-orphans
$COMPOSE run --rm ollama-init      # no-op if the models are already pulled
docker image prune -f
$COMPOSE ps
