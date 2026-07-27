# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.0.0-rc1 | ✅ Release candidate |
| develop | ⚠️ Development |

## Reporting a Vulnerability

**Do not report security vulnerabilities through public GitHub issues.**

Please report sensitive security issues to the maintainers directly.

## Known Security Considerations

### Deterministic Seeds

The V1 matrix seeds (`v1-alpha` through `v1-echo`) are PUBLIC and exist solely for reproducibility testing. Never use these seeds in production. Generate a unique, secret seed for each production deployment.

### Windows Defender

Protected binaries may trigger antivirus heuristics. This is expected behavior for obfuscated .NET assemblies. Add build output directories to Defender exclusions during development and testing. Do not permanently disable security controls on production hosts.

### No Signed Integrity in RC1

This release candidate does not include the signed integrity protection. Tampered protected binaries will not be detected at runtime. For production use requiring tamper detection, wait for a release that includes the `feat/signed-integrity` merge.
