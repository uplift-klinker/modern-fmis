# CD federated credentials

Main CD runs `gate` (with `environment: dev` for the single required-reviewer
approval) followed by `deploy` and `verify`, which do **not** carry an
`environment`. Their GitHub OIDC token subject is therefore the branch ref, not
`environment:dev`, so Azure needs a federated credential matching it.

## Required (one-time, per Azure app registration)

Add a federated credential to the CD service principal's app registration:

- Issuer: `https://token.actions.githubusercontent.com`
- Subject: `repo:uplift-klinker/modern-fmis:ref:refs/heads/main`
- Audience: `api://AzureADTokenExchange`

The existing `environment:dev` federated credential stays for CI preview.

## Security note

A branch-scoped credential means the `gate` job enforces **approval and
ordering**, not a hard Azure-credential boundary: any workflow running on the
protected `main` branch can obtain these credentials. This is an accepted
trade-off for single-approval automatic post-deploy verification on a protected
`main`.
