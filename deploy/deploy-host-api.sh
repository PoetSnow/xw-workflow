#!/usr/bin/env bash
# 在目标服务器上执行：拉取并滚动主 Host 镜像
# 用法: HOST_API_IMAGE=ghcr.io/xxx/host-api:sha ./deploy-host-api.sh
set -euo pipefail

IMAGE="${HOST_API_IMAGE:?HOST_API_IMAGE is required}"
DEPLOY_DIR="${DEPLOY_DIR:-/opt/xw}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.host.yml}"

cd "$DEPLOY_DIR"
export HOST_API_IMAGE="$IMAGE"

echo "Deploying $HOST_API_IMAGE in $DEPLOY_DIR"
docker compose -f "$COMPOSE_FILE" pull host-api
docker compose -f "$COMPOSE_FILE" up -d host-api
docker compose -f "$COMPOSE_FILE" ps host-api
echo "Done."
