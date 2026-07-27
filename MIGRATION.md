# Migration Guide

## From ConfuserEx 0.x to 1.0.0-rc1

### Breaking Changes

- **Seed attribute required for deterministic output**: The `<project>` element now supports a `seed` attribute. Without it, each protection run produces a different binary. For reproducible builds, always specify a fixed seed.

- **CLI `-o` flag behavior**: The `-o` flag now correctly propagates the seed from the template CRPROJ. Previous versions silently ignored the seed, causing non-deterministic output.

### Profile Changes

| Profile | v0.x | v1.0.0-rc1 |
|---------|------|------------|
| balanced | rename only | rename + ctrl flow + constants |
| strong | - | rename + ctrl flow + constants (dynamic) |
| critical | - | rename + ctrl flow + constants (targeted) + anti tamper |

### Removed (deferred)

- **Signed integrity**: Merged into a separate branch (`feat/signed-integrity`). Will be included in a future release.
- **KoiVM selective virtualization**: Available in `feat/koi-integration` but not included in this RC. Requires separate build of `KoiVM.Confuser` plugin.

### Size Expectations

Protection adds significant code size. Plan for:
- balanced: 4-5.5×
- strong: 5-8.5×
- critical: 6-10×
