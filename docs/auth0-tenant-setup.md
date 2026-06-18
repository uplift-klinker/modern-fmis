# Auth0 Tenant Setup — Manual Bootstrap Runbook

Use this guide to set up a new Auth0 tenant and the manual prerequisites Pulumi and CI cannot create for themselves. This is required **once per environment** (e.g., once for `dev`, once for `staging`, etc.) before the automated pipeline can run.

---

## 1. Create the Auth0 Tenant

1. Sign in to [auth0.com](https://auth0.com) with your organization account.
2. In the top-left tenant picker, choose **Create tenant**.
3. Name it following the pattern `modern-fmis-<env>` (e.g., `modern-fmis-dev`).
4. Select your region, then click **Create**.
5. Record the **tenant domain** — it looks like `modern-fmis-dev.us.auth0.com`. This is the value you will use for `AUTH0_DOMAIN` later.

> **Do not delete the `Username-Password-Authentication` database connection** that Auth0 creates by default. The Pulumi stack sets it as the tenant `default_directory`; removing it will cause `pulumi up` to fail.

---

## 2. Create the Management M2M Application

Pulumi needs programmatic access to the Auth0 Management API to create clients, resource servers, and users on your behalf.

1. In the Auth0 dashboard, go to **Applications → Applications → Create Application**.
2. Name it `fmis-<env>-management` (e.g., `fmis-dev-management`).
3. Choose **Machine to Machine Applications**, then click **Create**.
4. In the authorization dialog, select **Auth0 Management API** from the dropdown.
5. Grant the following scopes (search for each and tick it):

   - `read:clients`
   - `create:clients`
   - `update:clients`
   - `delete:clients`
   - `read:client_keys`
   - `read:resource_servers`
   - `create:resource_servers`
   - `update:resource_servers`
   - `delete:resource_servers`
   - `read:users`
   - `create:users`
   - `update:users`
   - `delete:users`
   - `read:connections`
   - `update:connections`
   - `read:tenant_settings`
   - `update:tenant_settings`

6. Click **Authorize**.
7. Open the application's **Settings** tab and record:
   - **Domain** → `AUTH0_DOMAIN` (same as step 1 above)
   - **Client ID** → `AUTH0_CLIENT_ID`
   - **Client Secret** → `AUTH0_CLIENT_SECRET`

These three values are what the CI workflow injects as Pulumi provider config (`auth0:domain`, `auth0:clientId`, `auth0:clientSecret`).

---

## 3. Create the GitHub → Azure OIDC Identity

The CI workflow authenticates to Azure via GitHub's OIDC token rather than a stored secret. Because the workflow job sets `environment: dev`, the OIDC token's `sub` claim is always `repo:<org>/modern-fmis:environment:dev` — for both `pull_request` and `push` events. A single federated credential with that subject is sufficient.

Run the following commands with an Azure session that has sufficient permissions (e.g., Owner or User Access Administrator on the subscription):

```bash
az ad app create --display-name "fmis-dev-github-oidc"
APP_ID=$(az ad app list --display-name "fmis-dev-github-oidc" --query "[0].appId" -o tsv)

az ad sp create --id "$APP_ID"

az ad app federated-credential create --id "$APP_ID" --parameters '{
  "name": "fmis-dev-github-env",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<org>/modern-fmis:environment:dev",
  "audiences": ["api://AzureADTokenExchange"]
}'

SUBSCRIPTION_ID=<subscription-id>

# Control-plane: allows bootstrap-state.sh to create the resource group and storage account
az role assignment create --assignee "$APP_ID" --role "Contributor" \
  --scope "/subscriptions/$SUBSCRIPTION_ID"

# Data-plane: required for `pulumi login azblob://...` to read and write state blobs
az role assignment create --assignee "$APP_ID" --role "Storage Blob Data Contributor" \
  --scope "/subscriptions/$SUBSCRIPTION_ID"
```

Replace `<org>` with your GitHub organization or username and `<subscription-id>` with the target Azure subscription ID.

**Why both roles?**

- **Contributor** (control plane) lets `bootstrap-state.sh` create the resource group and storage account via ARM. Without it the bootstrap script fails on the very first run.
- **Storage Blob Data Contributor** (data plane) is required so that `pulumi login azblob://...` can read and write state files inside the container. Azure's `--auth-mode login` path bypasses storage account keys entirely; the ARM Contributor role does **not** grant access to blob data under this mode.

Record the following values — you will need them as GitHub secrets in the next step:

- `APP_ID` (the value printed above) → `AZURE_CLIENT_ID`
- Your Azure tenant ID (`az account show --query tenantId -o tsv`) → `AZURE_TENANT_ID`
- `SUBSCRIPTION_ID` (the value you set above) → `AZURE_SUBSCRIPTION_ID`

---

## 4. Configure GitHub

1. In the GitHub repository, go to **Settings → Environments → New environment**.
2. Name it `dev`.
3. Add the following secrets to the `dev` environment (**Settings → Environments → dev → Environment secrets**):

   | Secret name              | Value                                                                                     |
   |--------------------------|-------------------------------------------------------------------------------------------|
   | `AZURE_CLIENT_ID`        | The `APP_ID` from step 3                                                                  |
   | `AZURE_TENANT_ID`        | Your Azure tenant ID                                                                      |
   | `AZURE_SUBSCRIPTION_ID`  | Your Azure subscription ID                                                                |
   | `PULUMI_CONFIG_PASSPHRASE` | A strong random passphrase — generate one with `openssl rand -base64 32` and store it in your password manager |
   | `AUTH0_DOMAIN`           | The Auth0 tenant domain from step 2 (e.g., `modern-fmis-dev.us.auth0.com`)               |
   | `AUTH0_CLIENT_ID`        | The management M2M application Client ID from step 2                                      |
   | `AUTH0_CLIENT_SECRET`    | The management M2M application Client Secret from step 2                                  |

   Example passphrase generation:

   ```bash
   openssl rand -base64 32
   ```

   Keep this passphrase safe — it encrypts all Pulumi secret config values. Losing it means you cannot decrypt existing state.

---

## 5. Adding Another Environment Later

Repeat steps 1–4 for the new environment (e.g., `staging`), substituting `<env>` throughout. Then:

**Create the Pulumi stack:**

```bash
cd infra/auth
pulumi stack init <env>
```

**Create `infra/auth/Pulumi.<env>.yaml`:**

```yaml
config:
  fmis-auth:enableE2eUser: "false"
```

Set `enableE2eUser` to `"true"` only for environments that run the Playwright end-to-end suite (typically `dev`). All other environments should leave it `"false"`.

**Create the GitHub environment:**

Add a GitHub environment named `<env>` and add the same seven secrets with values specific to that environment.

**Extend the workflow:**

Update `.github/workflows/infra.yml` to target the new environment — either by adding an environment variable toggle or by extracting the job into a matrix. Follow the existing naming pattern:

- Stack name: `<env>`
- Resource group: `fmis-<env>-infra`
- State storage account: `fmis<env>tfstate`
- Auth0 audience: `https://<env>.api.modern-fmis`
- All resource names follow `fmis-<env>-<layer>-<resource>`

---

## 6. Local Development

To run `pulumi preview` or `pulumi up` locally against the `dev` stack:

```bash
# Authenticate to Azure (requires Storage Blob Data Contributor on the state account)
az login

# Point Pulumi at the azblob state backend
pulumi login "azblob://pulumi-state?storage_account=fmisdevtfstate"

# From the auth project directory
cd infra/auth

# Export the passphrase (use the same value stored in GitHub)
export PULUMI_CONFIG_PASSPHRASE=<your-passphrase>

# Select the dev stack
pulumi stack select dev

# Set Auth0 provider config (only needed once per local clone; stored in Pulumi.<stack>.yaml)
pulumi config set auth0:domain "modern-fmis-dev.us.auth0.com"
pulumi config set auth0:clientId "<management-client-id>"
pulumi config set --secret auth0:clientSecret "<management-client-secret>"

# Preview changes
pulumi preview
```

> The `pulumi login azblob://...` command uses `--auth-mode login` implicitly, which relies on your `az login` session. Ensure the account you log in with has **Storage Blob Data Contributor** on the `fmisdevtfstate` storage account (see step 3).

---

## 7. What the Stack Outputs Are For

Once the stack has been deployed, `pulumi stack output` (or the CI pipeline reading outputs) exposes:

| Output          | Used by                                                                                      |
|-----------------|----------------------------------------------------------------------------------------------|
| `domain`        | App runtime config (`config.json`) and backend JWT validation middleware (Phase 3b)          |
| `spaClientId`   | App runtime config (`config.json`) — the Auth0 SPA application the browser authenticates as |
| `audience`      | App runtime config and backend JWT validation — the expected `aud` claim on access tokens   |
| `e2eClientId`   | Playwright suite (Phase 3c) — the non-interactive client used for the ROPG token exchange   |
| `e2eClientSecret` | Playwright suite — the client secret for the ROPG token exchange                          |
| `e2eUsername`   | Playwright suite — the email address of the seeded e2e test user                            |
| `e2ePassword`   | Playwright suite — the password of the seeded e2e test user                                 |

The `e2e*` outputs are only populated when `fmis-auth:enableE2eUser` is `"true"` for the stack.
