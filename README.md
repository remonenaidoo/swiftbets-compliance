# swiftbets-compliance

SwiftBets responsible gambling: money limits, cooling-off and self-exclusion, session settings and, later, KYC and the audit trail. The wallet, identity, gateway and notifications enforce what it publishes.

| Part | What it does |
|---|---|
| `SwiftBets.Compliance.Api` | Customer `/me/compliance`, `/me/limits/{kind}/{period}` (PUT, DELETE), `/me/exclusions`, `/me/session-settings`; staff `/admin/users/{id}/compliance` (`compliance.read`), `/admin/audit` and `/admin/audit/verify` (`compliance.audit.read`) |
| `SwiftBets.Compliance.Domain` | South African rules (D99): lower limits apply at once, raises and removals after 24 hours; cooling-off 1-42 days; self-exclusion 6-60 months and never shortened |
| `SwiftBets.Compliance.Infrastructure` | Dapper over SQL Server (`SbCompliance`, schema `compliance`). Each change locks the account row and commits with its events, its snapshot and its audit entry through the outbox |
| `SwiftBets.Compliance.Migrator` | DbUp migrations with rollbacks (`Migrator:RollbackTo`) |

## Events

| Topic | When |
|---|---|
| `compliance.restrictions-changed.v1` | Every change. Compacted, keyed by user id: the latest record is the account's whole state, with any pending raise and when it applies (ADR 0006) |
| `compliance.limit-changed.v1` | A limit is set, lowered, raised or removed |
| `compliance.self-exclusion-started.v1` | A cooling-off or self-exclusion begins |
| `audit.audit-recorded.v1` | Every change, with before and after snapshots |

Images: `ghcr.io/remonenaidoo/swiftbets-compliance` and `swiftbets-compliance-migrator`.

## Build and test

```bash
../swiftbets-platform/scripts/fetch-shared-packages.sh .
dotnet test
```

Integration tests start SQL Server in Docker (Testcontainers).

## Audit trail

Every service publishes `AuditRecordedV1` through its outbox, in the transaction of the change it records. Compliance consumes them into one append-only, hash-chained trail (`compliance.AuditEntries`):

- each entry's hash is SHA-256 over the previous hash and every field;
- a head row records the last sequence and hash, so a removed tail is caught too;
- the app login is denied UPDATE and DELETE on the entries.

`/admin/audit/verify` recomputes the whole chain and names the first entry that does not hold.
