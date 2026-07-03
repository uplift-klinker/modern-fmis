# Repository Rule: Infrastructure Tiers & Deletion Protection

**Applies to:** all Pulumi (C#) infrastructure in this repository, every phase.

Infrastructure is split into two explicitly separated tiers with different deletion-protection policies and lifecycles. Keep them separated in the Pulumi codebase (separate stacks/projects, or clearly separated component resources) so a routine application deploy can never tear down durable state.

## 1. Persistence tier — strict deletion prevention (REQUIRED)

Resources holding durable, hard-or-impossible-to-recreate state:

- The database (PostgreSQL / PostGIS).
- Storage accounts holding **durable data** (videos, zip files, uploads, documents — anything not regenerable from source).
- Queues and other stateful messaging infrastructure.

**Every persistence-tier resource MUST have both:**
- Pulumi resource option `protect` (guards against `pulumi destroy`), and
- an Azure `CanNotDelete` management lock (guards against deletion outside Pulumi).

This tier has its own lifecycle. It changes only via deliberate, reviewed runs — never as a side effect of an application deploy.

## 2. Application tier — no deletion prevention

Stateless, freely re-creatable resources:

- Container Apps / Function Apps (and other compute).
- Storage accounts holding **static web assets only** (html / css / js).
- Other config/compute rebuildable from source on the next deploy.

This tier deploys and redeploys freely. CI/CD touches **only** this tier on merge.

## Storage-account classification rule

Classify a storage account by **what it holds, not by being a storage account**:

| Contents | Tier | Deletion protection |
|---|---|---|
| Static web assets (html/css/js) | Application | No |
| Durable data (videos, zips, uploads, anything not regenerable from source) | Persistence | Yes |

A single phase may provision storage accounts in **both** tiers.

## Storage data-plane authorization

The default and preferred posture across this repository is **key-less, Microsoft Entra (Azure AD) authorization** — managed identities and Entra tokens, never account access keys. This holds unconditionally for the **persistence tier**: those accounts hold durable, sensitive data, so Shared Key access is disabled and every data-plane operation goes through Entra ID + a data-plane RBAC role.

The **application tier's static-site account is a deliberate, documented exception.** Its `$web` container holds only public, non-sensitive, fully regenerable assets (the built SPA bundle and a `config.json` of public runtime settings), so it keeps `AllowSharedKeyAccess = true` and its `dist`/`config.json` uploads authorize with the account key (obtained via `listKeys`) rather than an Entra token.

This exception exists for one reason: the deploy tooling requires it. Pulumi's `azure-native:storage:Blob` and `Pulumi.SyncedFolder.AzureBlobFolder` authenticate blob writes **only** with a Shared Key fetched via `listKeys` — they have no Entra/AAD data-plane mode (confirmed by `pulumi/pulumi-azure-native#3719`, which deliberately reverted the token-based path for backward compatibility). Because this content is public and non-critical, it does not warrant the persistence tier's governance, so accepting Shared Key here is an acceptable trade to keep the declarative deployment path. **Do not carry this exception into any account that holds sensitive or durable data** — those remain key-less/Entra-only.
