# Automation evidence

Actions artifacts are transport evidence only. Durable Review Results and
Organizer Disposition Records become authoritative only after their
content-addressed, append-only files are human-reviewed and merged to the default
branch under `reviews/` and `dispositions/` respectively.

Bootstrap human-readable disposition chain:

- [QF-ORG-MVS01-001 — R1](dispositions/QF-ORG-MVS01-001-controller-r1-disposition.md)
- [QF-ORG-MVS01-002 — R2](dispositions/QF-ORG-MVS01-002-controller-r2-disposition.md)
- [QF-ORG-MVS01-003 — R3 pre-REVERIFY](dispositions/QF-ORG-MVS01-003-controller-r3-disposition.md)
- [QF-ORG-MVS01-004 — R3 final Finding disposition](dispositions/QF-ORG-MVS01-004-controller-r3-final-disposition.md)

QF-ORG-MVS01-004 closes 21 independently verified Findings and leaves P3-015
`OPEN / DEFERRED`. It is not final Controller acceptance and remains Draft-only
until the durable default-branch publication and all enablement gates complete.

Role appointment evidence:

- [QF-APT-MVS01-001 — Independent Automation Release Reviewer appointment direction](appointments/QF-APT-MVS01-001-independent-automation-release-reviewer.md)

The Organizer direction and independent bootstrap signature are recorded, but
the role remains `VACANT / PENDING ACTIVATION` until the frozen appointment
contract and default-branch merge condition are satisfied.
