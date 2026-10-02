#!/usr/bin/env bash
# First-time HTTPS setup on the VPS. Run once from the repo root after DNS points to the server:
#   ./scripts/init-letsencrypt.sh
# Nginx cannot start without a certificate, so: dummy cert -> start nginx -> real cert -> reload.
set -euo pipefail

set -a; source .env; set +a
: "${DOMAIN:?Set DOMAIN in .env}"
: "${CERTBOT_EMAIL:?Set CERTBOT_EMAIL in .env}"

COMPOSE="docker compose -f docker-compose.prod.yml --env-file .env"
CONF=./deploy/certbot/conf
mkdir -p "$CONF/live/$DOMAIN" ./deploy/certbot/www

if [ -f "$CONF/live/$DOMAIN/fullchain.pem" ] && [ ! -f "$CONF/live/$DOMAIN/.dummy" ]; then
  echo "Certificate for $DOMAIN already exists. Nothing to do."; exit 0
fi

echo ">> Creating dummy certificate"
$COMPOSE run --rm --entrypoint "sh -c 'openssl req -x509 -nodes -newkey rsa:2048 -days 1 \
  -keyout /etc/letsencrypt/live/$DOMAIN/privkey.pem \
  -out /etc/letsencrypt/live/$DOMAIN/fullchain.pem -subj /CN=localhost && \
  touch /etc/letsencrypt/live/$DOMAIN/.dummy'" certbot

echo ">> Starting nginx"
$COMPOSE up -d --build nginx

echo ">> Removing dummy certificate"
$COMPOSE run --rm --entrypoint "rm -rf /etc/letsencrypt/live/$DOMAIN /etc/letsencrypt/archive/$DOMAIN /etc/letsencrypt/renewal/$DOMAIN.conf" certbot

echo ">> Requesting Let's Encrypt certificate"
$COMPOSE run --rm --entrypoint "certbot certonly --webroot -w /var/www/certbot \
  -d $DOMAIN -d www.$DOMAIN --email $CERTBOT_EMAIL --agree-tos --no-eff-email --non-interactive" certbot

echo ">> Reloading nginx"
$COMPOSE exec nginx nginx -s reload
echo "Done: https://$DOMAIN"
