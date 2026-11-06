# Security Policy

## Supported versions

| Version | Supported |
| --- | --- |
| 1.0.x | Yes |
| < 1.0 | No |

## Reporting a vulnerability

Please do not open a public issue for a security problem.

Use GitHub's private vulnerability reporting:

<https://github.com/zgyhbcz/AudioDeck/security/advisories/new>

If private reporting is unavailable, contact the maintainer
[@zgyhbcz](https://github.com/zgyhbcz) through GitHub and ask for a private
channel before sharing details.

Please include:

- Affected version and Windows build.
- A description of the impact.
- Reproduction steps or a proof of concept.
- Any suggested mitigation.

You can expect an initial response within a few days. Please allow time for a
fix and coordinated disclosure before publishing details.

## Scope

AudioDeck is a local desktop utility. It does not make network requests, does
not collect telemetry, and does not require administrator rights. Relevant
security areas include:

- Unsafe handling of audio device identifiers or COM objects.
- DLL search-order or binary planting issues.
- Installer/uninstaller path or registry handling.
- Privilege escalation through the installer or auto-start entry.

Reports about third-party components of Windows itself should go to Microsoft.
