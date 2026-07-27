# Changelog

## 1.0.0-rc1 — Core Protections (2026-07-27)

### Added
- Deterministic renaming: same seed + same input = bit-identical output (b43319f)
- V1 release matrix: 3 profiles × 5 seeds × 3 samples = 45 combinations
- V1 gate verification: behavior, reproducibility, structural diversity, secrets, budget
- CI pipeline with 9 stages: build, unit-tests, build-corpus, behavior-tests, reproducibility, structural-diversity, integrity-corruption, koivm-selective, quality-gates
- Build profiles: balanced, strong, critical
- Lab evaluation pipeline: 5 deobfuscation tools, isolated execution, output inspection

### Changed
- RandomService.GetRandomGenerator clones seed array to prevent mutation across phases
- CLI `-o` mode propagates `seed` attribute from template CRPROJ
- RenamePhase sorts candidates canonically before shuffle
- ExportMapPhase writes sorted symbol map entries
- Profile budgets revised: balanced 5.5×, strong 8.5×, critical 10.0×

### Known Limitations
- Signed integrity (feat/signed-integrity) not merged
- KoiVM selective virtualization not included
- KoiVmSample not executed in this RC
- Size ratios exceed initial budgets (see release-summary.json for justification)
