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
STORAGE_ACCOUNT="kinorgestorage"
BLOB_CONTAINER="umbraco-db"
IMAGE_TAG="$(date +%Y%m%d%H%M%S)"

echo "=== ki.norge.no Azure deployment ==="
echo "Resource group: ${RESOURCE_GROUP}"
echo "Location: ${LOCATION}"
echo "Image tag: ${IMAGE_TAG}"
echo

# --- Prerequisites ---
echo "==> Registering Azure providers..."
az extension add --name containerapp --upgrade --only-show-errors 2>/dev/null || true
az provider register --namespace Microsoft.App --wait >/dev/null 2>&1 || echo "  (skipped Microsoft.App — already registered or insufficient permissions)"
az provider register --namespace Microsoft.OperationalInsights --wait >/dev/null 2>&1 || echo "  (skipped Microsoft.OperationalInsights)"
az provider register --namespace Microsoft.ContainerRegistry --wait >/dev/null 2>&1 || echo "  (skipped Microsoft.ContainerRegistry)"
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

# --- Storage account for Litestream ---
echo "==> Ensuring storage account: ${STORAGE_ACCOUNT}"
if ! az storage account show --name "${STORAGE_ACCOUNT}" --resource-group "${RESOURCE_GROUP}" >/dev/null 2>&1; then
  az storage account create \
    --name "${STORAGE_ACCOUNT}" \
    --resource-group "${RESOURCE_GROUP}" \
    --location "${LOCATION}" \
    --sku Standard_LRS >/dev/null
fi
echo "OK"

echo "==> Ensuring blob container: ${BLOB_CONTAINER}"
STORAGE_KEY="$(az storage account keys list --account-name "${STORAGE_ACCOUNT}" --resource-group "${RESOURCE_GROUP}" --query "[0].value" -o tsv)"
az storage container create \
  --name "${BLOB_CONTAINER}" \
  --account-name "${STORAGE_ACCOUNT}" \
  --account-key "${STORAGE_KEY}" \
  --only-show-errors >/dev/null 2>&1 || true
echo "OK"

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

# --- Secrets (only generate on first deploy, reuse existing) ---
CMS_EXISTS=false
if az containerapp show --resource-group "${RESOURCE_GROUP}" --name "${UMBRACO_APP_NAME}" >/dev/null 2>&1; then
  CMS_EXISTS=true
fi

FRONTEND_EXISTS=false
if az containerapp show --resource-group "${RESOURCE_GROUP}" --name "${FRONTEND_APP_NAME}" >/dev/null 2>&1; then
  FRONTEND_EXISTS=true
fi

if [ "${CMS_EXISTS}" = false ]; then
  DELIVERY_API_KEY="$(openssl rand -base64 32 | tr -d '\n')"
  PREVIEW_SECRET="$(openssl rand -base64 32 | tr -d '\n')"
  echo
  echo "Generated new secrets (save these):"
  echo "  DELIVERY_API_KEY: ${DELIVERY_API_KEY}"
  echo "  PREVIEW_SECRET:   ${PREVIEW_SECRET}"
  echo
fi

# --- Deploy CMS ---
echo "==> Deploying CMS: ${UMBRACO_APP_NAME}"
if [ "${CMS_EXISTS}" = true ]; then
  # Update existing — use YAML to reliably set all env vars
  az containerapp secret set \
    --name "${UMBRACO_APP_NAME}" \
    --resource-group "${RESOURCE_GROUP}" \
    --secrets "litestream-azure-account-key=${STORAGE_KEY}" \
    --only-show-errors >/dev/null 2>&1 || true

  cat > /tmp/cms-deploy.yaml <<YAMLDOC
properties:
  template:
    containers:
    - env:
      - name: ASPNETCORE_ENVIRONMENT
        value: Production
      - name: ASPNETCORE_URLS
        value: http://0.0.0.0:8080
      - name: ASPNETCORE_FORWARDEDHEADERS_ENABLED
        value: 'true'
      - name: ConnectionStrings__umbracoDbDSN
        value: Data Source=/app/umbraco/Data/Umbraco.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True
      - name: ConnectionStrings__umbracoDbDSN_ProviderName
        value: Microsoft.Data.Sqlite
      - name: UMBRACO__CMS__DELIVERYAPI__ENABLED
        value: 'true'
      - name: UMBRACO__CMS__DELIVERYAPI__PUBLICACCESS
        value: 'true'
      - name: UMBRACO__CMS__DELIVERYAPI__APIKEY
        secretRef: delivery-api-key
      - name: UMBRACO__CMS__DELIVERYAPI__RICHTEXTOUTPUTASJSON
        value: 'true'
      - name: UMBRACO__CMS__GLOBAL__MAINDOMLOCK
        value: FileSystemMainDomLock
      - name: UMBRACO__CMS__UNATTENDED__INSTALLUNATTENDED
        value: 'true'
      - name: UMBRACO__CMS__UNATTENDED__UNATTENDEDUSERNAME
        value: 'admin'
      - name: UMBRACO__CMS__UNATTENDED__UNATTENDEDUSEREMAIL
        value: 'admin@ki.norge.no'
      - name: UMBRACO__CMS__UNATTENDED__UNATTENDEDUSERPASSWORD
        value: 'KiNorge2025!'
      - name: LITESTREAM_AZURE_ACCOUNT_KEY
        secretRef: litestream-azure-account-key
      - name: Serilog__MinimumLevel__Default
        value: Information
      - name: Serilog__WriteTo__0__Name
        value: Console
      image: ${UMBRACO_IMAGE}
      name: ki-norge-cms
      resources:
        cpu: 0.5
        memory: 1Gi
    volumes: []
YAMLDOC

  az containerapp update \
    --resource-group "${RESOURCE_GROUP}" \
    --name "${UMBRACO_APP_NAME}" \
    --yaml /tmp/cms-deploy.yaml >/dev/null
  rm -f /tmp/cms-deploy.yaml
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
    --secrets \
      "delivery-api-key=${DELIVERY_API_KEY}" \
      "preview-secret=${PREVIEW_SECRET}" \
      "litestream-azure-account-key=${STORAGE_KEY}" \
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
      "UMBRACO__CMS__UNATTENDED__INSTALLUNATTENDED=true" \
      "UMBRACO__CMS__UNATTENDED__UNATTENDEDUSERNAME=admin" \
      "UMBRACO__CMS__UNATTENDED__UNATTENDEDUSEREMAIL=admin@ki.norge.no" \
      "UMBRACO__CMS__UNATTENDED__UNATTENDEDUSERPASSWORD=KiNorge2025!" \
      "LITESTREAM_AZURE_ACCOUNT_KEY=secretref:litestream-azure-account-key" \
      "Serilog__MinimumLevel__Default=Information" \
      "Serilog__WriteTo__0__Name=Console" \
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
if [ "${FRONTEND_EXISTS}" = true ]; then
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
