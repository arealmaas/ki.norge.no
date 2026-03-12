#!/usr/bin/env bash
set -euo pipefail

# Deploy ki.norge.no to Azure Container Apps
# Usage: ./scripts/deploy-azure.sh

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Configuration
RESOURCE_GROUP="ki-norge"
LOCATION="norwayeast"
CONTAINERAPPS_ENV="ki-norge-no-env"
ACR_NAME="kinorgeacr"
UMBRACO_APP_NAME="ki-norge-cms"
FRONTEND_APP_NAME="ki-norge-frontend"
IMAGE_TAG="$(date +%Y%m%d%H%M%S)"

echo "=== ki.norge.no Azure deployment ==="
echo "Resource group: ${RESOURCE_GROUP}"
echo "Location: ${LOCATION}"
echo "Image tag: ${IMAGE_TAG}"
echo

# --- Prerequisites ---
echo "==> Registering Azure providers..."
az extension add --name containerapp --upgrade --only-show-errors 2>/dev/null || true
az provider register --namespace Microsoft.App --wait >/dev/null
az provider register --namespace Microsoft.OperationalInsights --wait >/dev/null
az provider register --namespace Microsoft.ContainerRegistry --wait >/dev/null
echo "OK"

# --- Resource group ---
echo "==> Ensuring resource group: ${RESOURCE_GROUP}"
az group show --name "${RESOURCE_GROUP}" >/dev/null 2>&1 || \
  az group create --name "${RESOURCE_GROUP}" --location "${LOCATION}" >/dev/null
echo "OK"

# --- Container Registry ---
echo "==> Ensuring container registry: ${ACR_NAME}"
if ! az acr show --resource-group "${RESOURCE_GROUP}" --name "${ACR_NAME}" >/dev/null 2>&1; then
  az acr create \
    --resource-group "${RESOURCE_GROUP}" \
    --name "${ACR_NAME}" \
    --location "${LOCATION}" \
    --sku Basic \
    --admin-enabled true >/dev/null
fi
az acr update --resource-group "${RESOURCE_GROUP}" --name "${ACR_NAME}" --admin-enabled true --only-show-errors >/dev/null
echo "OK"

ACR_LOGIN_SERVER="$(az acr show --resource-group "${RESOURCE_GROUP}" --name "${ACR_NAME}" --query loginServer -o tsv)"
ACR_USERNAME="$(az acr credential show --resource-group "${RESOURCE_GROUP}" --name "${ACR_NAME}" --query username -o tsv)"
ACR_PASSWORD="$(az acr credential show --resource-group "${RESOURCE_GROUP}" --name "${ACR_NAME}" --query passwords[0].value -o tsv)"

UMBRACO_IMAGE="${ACR_LOGIN_SERVER}/ki-norge/cms:${IMAGE_TAG}"
FRONTEND_IMAGE="${ACR_LOGIN_SERVER}/ki-norge/frontend:${IMAGE_TAG}"

# --- Build images (remote ACR build) ---
echo "==> Building CMS image: ${UMBRACO_IMAGE}"
az acr build \
  --registry "${ACR_NAME}" \
  --image "ki-norge/cms:${IMAGE_TAG}" \
  --file "${REPO_ROOT}/apps/cms-umbraco/Dockerfile" \
  "${REPO_ROOT}/apps/cms-umbraco" 2>&1 | tail -5
echo "OK"

echo "==> Building frontend image: ${FRONTEND_IMAGE}"
az acr build \
  --registry "${ACR_NAME}" \
  --image "ki-norge/frontend:${IMAGE_TAG}" \
  --file "${REPO_ROOT}/apps/frontend/Dockerfile" \
  "${REPO_ROOT}/apps/frontend" 2>&1 | tail -5
echo "OK"

# --- Container Apps Environment ---
echo "==> Ensuring Container Apps environment: ${CONTAINERAPPS_ENV}"
if ! az containerapp env show --resource-group "${RESOURCE_GROUP}" --name "${CONTAINERAPPS_ENV}" >/dev/null 2>&1; then
  az containerapp env create \
    --resource-group "${RESOURCE_GROUP}" \
    --name "${CONTAINERAPPS_ENV}" \
    --location "${LOCATION}" >/dev/null
fi
echo "OK"

# --- Generate secrets ---
DELIVERY_API_KEY="$(openssl rand -base64 32 | tr -d '\n')"
PREVIEW_SECRET="$(openssl rand -base64 32 | tr -d '\n')"

# --- Deploy CMS ---
echo "==> Deploying CMS: ${UMBRACO_APP_NAME}"
if az containerapp show --resource-group "${RESOURCE_GROUP}" --name "${UMBRACO_APP_NAME}" >/dev/null 2>&1; then
  az containerapp update \
    --resource-group "${RESOURCE_GROUP}" \
    --name "${UMBRACO_APP_NAME}" \
    --image "${UMBRACO_IMAGE}" >/dev/null
else
  az containerapp create \
    --resource-group "${RESOURCE_GROUP}" \
    --name "${UMBRACO_APP_NAME}" \
    --environment "${CONTAINERAPPS_ENV}" \
    --image "${UMBRACO_IMAGE}" \
    --ingress external \
    --target-port 8080 \
    --min-replicas 1 \
    --max-replicas 1 \
    --registry-server "${ACR_LOGIN_SERVER}" \
    --registry-username "${ACR_USERNAME}" \
    --registry-password "${ACR_PASSWORD}" \
    --secrets "delivery-api-key=${DELIVERY_API_KEY}" "preview-secret=${PREVIEW_SECRET}" \
    --env-vars \
      "ASPNETCORE_ENVIRONMENT=Production" \
      "ASPNETCORE_URLS=http://0.0.0.0:8080" \
      "ASPNETCORE_FORWARDEDHEADERS_ENABLED=true" \
      "ConnectionStrings__umbracoDbDSN=Data Source=/app/umbraco/Data/Umbraco.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True" \
      "ConnectionStrings__umbracoDbDSN_ProviderName=Microsoft.Data.Sqlite" \
      "UMBRACO__CMS__DELIVERYAPI__ENABLED=true" \
      "UMBRACO__CMS__DELIVERYAPI__PUBLICACCESS=true" \
      "UMBRACO__CMS__DELIVERYAPI__APIKEY=secretref:delivery-api-key" \
      "UMBRACO__CMS__DELIVERYAPI__RICHTEXTOUTPUTASJSON=true" \
      "UMBRACO__CMS__GLOBAL__MAINDOMLOCK=FileSystemMainDomLock" \
    >/dev/null
fi

az containerapp revision set-mode \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${UMBRACO_APP_NAME}" \
  --mode single >/dev/null 2>&1 || true

UMBRACO_FQDN="$(az containerapp show --resource-group "${RESOURCE_GROUP}" --name "${UMBRACO_APP_NAME}" --query properties.configuration.ingress.fqdn -o tsv)"
echo "OK — CMS URL: https://${UMBRACO_FQDN}"

# --- Deploy Frontend ---
CONTAINERAPPS_DEFAULT_DOMAIN="$(az containerapp env show --resource-group "${RESOURCE_GROUP}" --name "${CONTAINERAPPS_ENV}" --query properties.defaultDomain -o tsv)"
UMBRACO_INTERNAL_URL="https://${UMBRACO_APP_NAME}.internal.${CONTAINERAPPS_DEFAULT_DOMAIN}"

echo "==> Deploying frontend: ${FRONTEND_APP_NAME}"
if az containerapp show --resource-group "${RESOURCE_GROUP}" --name "${FRONTEND_APP_NAME}" >/dev/null 2>&1; then
  az containerapp update \
    --resource-group "${RESOURCE_GROUP}" \
    --name "${FRONTEND_APP_NAME}" \
    --image "${FRONTEND_IMAGE}" >/dev/null
else
  az containerapp create \
    --resource-group "${RESOURCE_GROUP}" \
    --name "${FRONTEND_APP_NAME}" \
    --environment "${CONTAINERAPPS_ENV}" \
    --image "${FRONTEND_IMAGE}" \
    --ingress external \
    --target-port 4321 \
    --min-replicas 1 \
    --max-replicas 1 \
    --registry-server "${ACR_LOGIN_SERVER}" \
    --registry-username "${ACR_USERNAME}" \
    --registry-password "${ACR_PASSWORD}" \
    --secrets "delivery-api-key=${DELIVERY_API_KEY}" "preview-secret=${PREVIEW_SECRET}" \
    --env-vars \
      "HOST=0.0.0.0" \
      "PORT=4321" \
      "UMBRACO_URL=${UMBRACO_INTERNAL_URL}" \
      "UMBRACO_API_KEY=secretref:delivery-api-key" \
      "PREVIEW_SECRET=secretref:preview-secret" \
      "SITE_URL=https://${FRONTEND_APP_NAME}.${CONTAINERAPPS_DEFAULT_DOMAIN}" \
    >/dev/null
fi

FRONTEND_FQDN="$(az containerapp show --resource-group "${RESOURCE_GROUP}" --name "${FRONTEND_APP_NAME}" --query properties.configuration.ingress.fqdn -o tsv)"
echo "OK — Frontend URL: https://${FRONTEND_FQDN}"

echo
echo "=== Deployment complete ==="
echo "CMS:      https://${UMBRACO_FQDN}"
echo "Frontend: https://${FRONTEND_FQDN}"
echo
echo "Generated secrets (save these):"
echo "  DELIVERY_API_KEY: ${DELIVERY_API_KEY}"
echo "  PREVIEW_SECRET:   ${PREVIEW_SECRET}"
