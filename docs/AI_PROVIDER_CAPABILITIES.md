# AI provider capabilities — verification status (2026-11-07)

Rule in force: **a provider card exists only if used + limit + remaining can
be retrieved from an authorized, documented source.** Everything else stays
excluded rather than estimated.

| Provider | Account type | Data source | Real limit? | Real reset? | Status |
|---|---|---|---|---|---|
| OpenAI | Organization API (Admin key) | Official Admin endpoints: `/v1/organization/spend_limit`, `/v1/organization/costs`, `/v1/organization/usage/completions` | Yes — enforcing monthly USD hard limit | Yes (monthly window) | **Implemented** (`OpenAiProvider`); card appears only when a limit is verified enforcing; DPAPI-stored key |
| ChatGPT Plus/Pro | Consumer subscription | No documented quota API | No | No | Unsupported — never shown |
| Anthropic / Claude (incl. Claude Code) | Subscription or API | Usage/cost reports don't establish subscription remaining allowance | No | No | Unsupported — never shown |
| Codex CLI | Local client | Local session stats ≠ upstream quota | No | No | Unsupported — never shown |
| OpenCode | Local client | Local session stats ≠ upstream quota | No | No | Unsupported — never shown |
| Z.ai / ZCode plan | Vendor plan | No third-party connector documented | No | No | Unsupported — never shown |

Provenance labels used internally: `VERIFIED_OFFICIAL`, `VERIFIED_LOCAL`,
`ESTIMATED`, `MANUAL_BUDGET`, `UNAVAILABLE`, `STALE`, `ERROR`. Only
`VERIFIED_OFFICIAL` with a positive limit supports a quota card; the UI
aggregator drops anything else.

Security: keys are encrypted per Windows user with DPAPI
(`Data\credentials.bin`), never logged, never sent anywhere except the
official api.openai.com endpoints. Removing the key in Settings wipes the
entry.

Refresh policy: manual only (button + tray Refresh). No background polling;
errors surface as text states, never as fabricated numbers.

Reference: OpenAI platform docs — Organization usage/costs and spend limits
APIs (admin-scoped keys). Re-verify endpoints before each release.
