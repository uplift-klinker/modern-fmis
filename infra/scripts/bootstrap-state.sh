#!/usr/bin/env bash
set -euo pipefail

# Idempotently provision the Pulumi self-managed state backend (azblob).
# Requires an authenticated `az` session (CI uses GitHub->Azure OIDC).
ENVIRONMENT="${ENVIRONMENT:-dev}"
LOCATION="${AZURE_LOCATION:-eastus}"
RESOURCE_GROUP="${RESOURCE_GROUP:-fmis-${ENVIRONMENT}-infra}"
STORAGE_ACCOUNT="${PULUMI_STATE_ACCOUNT:-fmis${ENVIRONMENT}tfstate}"
CONTAINER="${PULUMI_STATE_CONTAINER:-pulumi-state}"

echo "Ensuring resource group ${RESOURCE_GROUP}..."
az group create --name "${RESOURCE_GROUP}" --location "${LOCATION}" --output none

echo "Ensuring storage account ${STORAGE_ACCOUNT}..."
if ! az storage account show --name "${STORAGE_ACCOUNT}" --resource-group "${RESOURCE_GROUP}" --output none 2>/dev/null; then
  az storage account create \
    --name "${STORAGE_ACCOUNT}" \
    --resource-group "${RESOURCE_GROUP}" \
    --location "${LOCATION}" \
    --sku Standard_LRS \
    --kind StorageV2 \
    --min-tls-version TLS1_2 \
    --allow-blob-public-access false \
    --output none
fi

echo "Ensuring container ${CONTAINER}..."
az storage container create \
  --name "${CONTAINER}" \
  --account-name "${STORAGE_ACCOUNT}" \
  --auth-mode login \
  --output none

echo "State backend ready: azblob://${CONTAINER}?storage_account=${STORAGE_ACCOUNT}"
