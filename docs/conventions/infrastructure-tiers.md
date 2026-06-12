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
