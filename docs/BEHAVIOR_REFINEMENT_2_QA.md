# Behaviour Refinement 2 — Static QA and Regression Scope

## Added regression coverage

Six tests cover the new bottleneck model:

- a calm, assertive, delayed pedestrian can enter `Press` mode;
- a low-assertiveness pedestrian enters `SeekGap` rather than pressing;
- a crawling pedestrian always yields;
- leaving the doorway influence releases the committed mode and decays pressure;
- pressing compresses social spacing without removing body-contact forces;
- scenario JSON round-trips the three bottleneck tuning values.

Existing tests also verify deterministic traits, zero-variation population centres, scenario persistence, exit anti-banging, panic propagation, body-clear routing, explosion effects and numerical guardrails.

Expected total after this refinement: **60 tests**.

## Static checks performed in the packaging environment

- C# delimiter and comment/string-aware balance scan;
- project XML parsing;
- scenario JSON parsing;
- duplicate/placeholder scan;
- generated binary/cache exclusion check;
- ZIP integrity verification;
- source diff review against Behaviour Refinement 1.

The packaging environment does not contain the .NET SDK. Therefore `dotnet build` and `dotnet test` must be run on the target Windows/VS Code machine and remain the authoritative validation.
