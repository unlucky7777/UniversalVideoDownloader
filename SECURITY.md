# Security

## Scope

UVD consists of a Windows desktop application, a Chrome Manifest V3 extension, and a Native Messaging host.

The extension only promotes media from a tab after the user starts detection or explicitly asks to send a page/media item to UVD. The Native Messaging host accepts framed JSON only from Chrome's registered Native Messaging channel.

## Data handling

- Cookie and Authorization headers are intentionally excluded from extension -> Native Host handoff.
- Allowed transport headers are limited to Referer, Origin, User-Agent, Accept, and Accept-Language.
- Signed media URLs can contain temporary access tokens. They are kept in Chrome session storage / the local Native Inbox only for the handoff lifecycle and are not intentionally written to application logs.
- Native Host error responses do not echo URL or header values.
- Machine-specific Native Messaging manifests, browser profiles, downloads, logs and configuration files are not source-controlled.
- The retired legacy Cloudflare-bypass path that could place a user-provided cookie in a temporary batch command is not part of UVD 1.0.0.

## Native boundary

The Native Host:

- limits one incoming message to 256 KiB;
- validates command type, media kind, quality and header count/value length;
- rejects CR/LF/NUL header injection;
- launches UVD directly with UseShellExecute=false;
- does not invoke cmd.exe or PowerShell for browser-originated commands;
- rejects loopback, .local, .internal and home.arpa targets;
- rejects literal private/link-local/special IP addresses;
- resolves ordinary hostnames before handoff and rejects them when the current DNS result contains private/link-local/special addresses.

## Browser permissions

The extension needs broad HTTP(S) host access because media frequently comes from third-party CDN origins different from the page origin. Network observations are filtered to the armed tab in code.

activeTab is intentionally not requested. The remaining permissions are used for network observation, tab lookup, script injection, session storage, the context-menu entry, clipboard copy actions and Native Messaging.

This permission model should be called out explicitly in the Chrome Web Store / GitHub documentation.

## Reporting a vulnerability

For a public GitHub repository, enable GitHub Security Advisories and ask reporters to use the repository's private "Report a vulnerability" flow rather than filing a public issue.

Do not include signed media URLs, cookies, account identifiers, browser profiles or private logs in public security reports.
