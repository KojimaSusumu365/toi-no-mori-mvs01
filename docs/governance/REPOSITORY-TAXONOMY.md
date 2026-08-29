# Repository physical taxonomy

Status: DRAFT — CI ENFORCED

## Canonical locations

| Concern | Location |
|---|---|
| onboarding | repository root |
| ADR / UML / DR / contracts | docs/architecture/ |
| governance and review rules | docs/governance/ |
| Stage specifications and manifests | docs/stages/stage6rNN/ |
| external review packet | docs/reviews/stage6r11r/ |
| execution evidence | docs/evidence/stage6rNN/ |
| non-current provenance | docs/archive/ |

The exact path move ledger is [REPOSITORY-TAXONOMY.json](REPOSITORY-TAXONOMY.json).

## Migration rules

- Git blobs for JSON evidence and historical evidence retain their recorded claims.
- Active Markdown links and CI path references use canonical locations.
- Root Manifest files and flat ADR, UML, Stage and Evidence paths are forbidden.
- Stage 6R-12 has a placeholder only and remains NOT STARTED.
- Archived files are explicitly non-normative.

## Enforcement

scripts/check-repository-taxonomy.py verifies required directories, rejects legacy flat paths, checks active text for stale path references and validates internal Markdown links.
