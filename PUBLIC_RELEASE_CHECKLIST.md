# Public release checklist

## Repository

- Use `source/` as the Git repository root. Do not publish the parent working directory.
- Run `scripts/verify-public-tree.ps1` before the first push and in CI.
- Confirm no `com.uvd.bridge.json`, config, Downloads, browser profiles, logs or runtime executables are tracked.
- Keep `runtime-manifest.json` updated whenever a bundled executable changes.
- Enable GitHub secret scanning / push protection where available.
- Enable GitHub Security Advisories.

## Build / test

- Restore with the supported .NET SDK.
- Run the complete .NET test project.
- Run `npm test` under `UVD-Source-Capture`.
- Run CodeQL for C# and JavaScript/TypeScript.
- Review Dependabot updates rather than auto-merging them.
- Confirm generated extension `suite-version.json` matches `Directory.Build.props`.

## Binary release

- Build release artifacts from a clean checkout.
- Obtain runtime executables from their documented upstream release/source.
- Verify each executable against `runtime-manifest.json` before packaging.
- Never include `config.json`, `config.txt`, Native Messaging registration JSON, Logs, Downloads, NativeInbox, browser profile data or test media.
- Include `LICENSE`, `THIRD_PARTY_NOTICES.md`, and applicable third-party license texts.
- Generate the Native Messaging manifest at install time from the user's local install path and extension ID; do not ship a developer-machine manifest.
- Publish SHA256 for the final ZIP.

## Extension

- Explain why HTTP(S) host permissions, webRequest, scripting and nativeMessaging are needed.
- State that detection is scoped to an armed tab in application logic.
- Confirm Cookie and Authorization are not exported to the Native Host.
- Load the exact release extension as unpacked and repeat the Chrome -> Native Host -> UVD review flow.

## Legal / product

- Only download content the user is authorized to access.
- Do not market broad site support as a guarantee; site behavior can change without notice.
- Verify the exact licenses of third-party binaries for every release.
- If distributing GPL-covered executables, satisfy their license and corresponding-source obligations.
