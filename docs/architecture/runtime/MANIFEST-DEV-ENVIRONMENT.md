# Stage 6R-1 Development Environment Package

- Package: `15_toi-no-mori-mvs01-stage6r1-local-toolchain-v0.1.zip`
- Date: 2026-08-20
- Toolchain: .NET SDK 10.0.400 / PostgreSQL 18.6
- Build result: Release build, 0 warnings, 0 errors
- Existing non-PG suites: 53 passed, TC-055 expected RED 1
- Stage 6R-1 contracts: 22 expected RED, 0 harness errors
- PostgreSQL/DR: binaries installed; tests not run because this container forbids effective-user switching

The archive intentionally excludes `.tools`, NuGet caches, `bin`, and `obj`. Run `scripts/install-local-toolchain.sh` to reproduce the verified local environment.
