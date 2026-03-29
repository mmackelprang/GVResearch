# Google Voice Cookie Analysis

**Date:** 2026-03-29
**Method:** CDP Network.getAllCookies on live authenticated Chrome session
**Total cookies:** 56 (45 Google/GV-related)

---

## Cookie Categories

### Tier 1: Authentication Core (Required for API calls)

| Cookie | Domain | Expires | Lifetime | HttpOnly | Secure | Purpose |
|---|---|---|---|---|---|---|
| **SAPISID** | `.google.com` | 2027-05-03 | ~13 months | N | Y | **SAPISIDHASH computation** — the key ingredient for Authorization header |
| **APISID** | `.google.com` | 2027-05-03 | ~13 months | N | N | API session (non-secure variant) |
| **SID** | `.google.com` | 2027-05-03 | ~13 months | N | N | Primary session identifier |
| **HSID** | `.google.com` | 2027-05-03 | ~13 months | Y | N | HTTP-only session (server-side validation) |
| **SSID** | `.google.com` | 2027-05-03 | ~13 months | Y | Y | Secure session |
| **__Secure-1PSID** | `.google.com` | 2027-05-03 | ~13 months | Y | Y | Secure primary SID (used in __Secure context) |
| **__Secure-3PSID** | `.google.com` | 2027-05-03 | ~13 months | Y | Y | Tertiary secure SID |

**Lifetime:** All core auth cookies expire ~13 months from creation. They are set during Google login and NOT rotated during normal usage.

### Tier 2: Voice-Specific (Required for GV services)

| Cookie | Domain | Expires | Lifetime | HttpOnly | Secure | Purpose |
|---|---|---|---|---|---|---|
| **COMPASS** | `voice.google.com` | 2026-04-08 | ~10 days | Y | Y | GV web frontend auth (158 chars) |
| **COMPASS** | `voice.clients6.google.com` | 2026-04-08 | ~10 days | Y | Y | GV API auth (129 chars) |
| **COMPASS** | `clients6.google.com` | 2026-04-08 | ~10 days | Y | Y | General clients API auth (129 chars) |
| **OSID** | `voice.google.com` | 2027-05-03 | ~13 months | Y | Y | Voice-specific session (153 chars) |
| **__Secure-OSID** | `voice.google.com` | 2027-05-03 | ~13 months | Y | Y | Secure variant of OSID |

**COMPASS is critical and short-lived!** It expires in ~10 days, not 13 months like the core cookies. This is the cookie most likely to cause auth failures if not refreshed.

COMPASS values differ by domain:
- `voice.google.com`: `voice-web-frontend=Cg...` (158 chars)
- `voice.clients6.google.com`: `voice-api=Cg...` (129 chars)
- `clients6.google.com`: `voice-api=Cg...` (129 chars)

### Tier 3: Session Rotation Cookies

| Cookie | Domain | Expires | Lifetime | Purpose |
|---|---|---|---|---|
| **SIDCC** | `.google.com` | 2027-03-29 | ~1 year | SID companion cookie (rotated periodically) |
| **__Secure-1PSIDCC** | `.google.com` | 2027-03-29 | ~1 year | Secure SID companion |
| **__Secure-3PSIDCC** | `.google.com` | 2027-03-29 | ~1 year | Tertiary secure SID companion |
| **__Secure-1PSIDRTS** | `.google.com` | 2026-03-29 | ~1 day | SID rotation timestamp |
| **__Secure-3PSIDRTS** | `.google.com` | 2026-03-29 | ~1 day | Tertiary rotation timestamp |
| **__Secure-1PSIDTS** | `.google.com` | 2027-03-29 | ~1 year | SID timestamp |
| **__Secure-3PSIDTS** | `.google.com` | 2027-03-29 | ~1 year | Tertiary SID timestamp |

**PSIDRTS cookies expire daily!** These are rotation timestamps — they tell Google when the session was last validated. If they expire, Google may invalidate the session.

### Tier 4: Account Management

| Cookie | Domain | Expires | Lifetime | Purpose |
|---|---|---|---|---|
| **ACCOUNT_CHOOSER** | `accounts.google.com` | 2027-05-03 | ~13 months | Remembers which account is selected |
| **LSID** | `accounts.google.com` | 2027-05-03 | ~13 months | Login session ID |
| **__Host-GAPS** | `accounts.google.com` | 2027-05-03 | ~13 months | Google Accounts session |
| **__Host-GAPSTS** | `accounts.google.com` | 2027-05-03 | ~13 months | GAPS timestamp |
| **__Host-1PLSID** | `accounts.google.com` | 2027-05-03 | ~13 months | Primary LSID |
| **__Host-3PLSID** | `accounts.google.com` | 2027-05-03 | ~13 months | Tertiary LSID |
| **SMSV** | `accounts.google.com` | 2027-05-03 | ~13 months | SMS verification state |

### Tier 5: Tracking/Analytics (Not needed for API)

| Cookie | Domain | Expires | Purpose |
|---|---|---|---|
| **NID** | `.google.com` | 2026-09-28 | Google preferences (694 chars!) |
| **S** | `.google.com` | session | Session-level tracking |
| **OTZ** | various | 2026-04-28 | One-tap sign-in state |
| **_ga** / **_ga_*** | various | varies | Google Analytics |
| **__utm*** | `.workspace.google.com` | varies | UTM tracking |

---

## Cookie Refresh Strategy

### What Rotates Automatically

| Cookie | Rotation | Mechanism |
|---|---|---|
| **COMPASS** | Every ~10 days | Set by GV server on API responses |
| **__Secure-*PSIDRTS** | Daily | Set by Google on session validation |
| **SIDCC** / **PSIDCC** | Periodically | Set by Google on requests |
| **S** | Per session | Session cookie |

### What Doesn't Rotate

| Cookie | Lifetime | Notes |
|---|---|---|
| SAPISID | 13 months | Set once at login, never rotated |
| SID/HSID/SSID | 13 months | Set once at login |
| APISID | 13 months | Set once at login |
| OSID | 13 months | Voice-specific, set at first GV visit |

### Refresh Endpoints Discovered

| Endpoint | Purpose | Frequency |
|---|---|---|
| `signaler-pa.../punctual/v1/refreshCreds` | Refresh signaler auth | Every ~4 minutes |
| `accounts.google.com/RotateCookies` | Rotate session cookies | Unobserved (may be browser-initiated) |
| API response `Set-Cookie` headers | COMPASS rotation | Every ~10 days (set by server) |

---

## Minimum Cookie Set for Headless API Calls

For making GV API calls without a browser, you need:

### Required (must have)
```
Cookie: SID={sid}; HSID={hsid}; SSID={ssid}; APISID={apisid}; SAPISID={sapisid}; COMPASS={compass_api}
Authorization: SAPISIDHASH {timestamp}_{sha1(timestamp + " " + sapisid + " " + origin)}
```

### Also needed (for specific domains)
- `OSID` / `__Secure-OSID` → for `voice.google.com` direct requests
- `COMPASS` (voice.google.com variant) → for web frontend requests
- `__Secure-1PSID` / `__Secure-3PSID` → for some API endpoints

### Refresh priority
1. **COMPASS** — 10-day lifetime, must refresh before expiry
2. **PSIDRTS** — daily rotation timestamps
3. **SIDCC/PSIDCC** — periodic rotation
4. **Core auth (SAPISID, SID, etc.)** — 13 months, no rush

---

## IAET Cookie Analysis Feature Scope

Based on this investigation, IAET should support:

### Cookie Capture
- Extract cookies from CDP (Chrome debug port) — proven approach
- Extract from HAR files (cookies are in the HAR spec)
- Extract from browser extension exports (.iaet.json could include cookies)

### Cookie Analysis
- Categorize by purpose (auth, session, tracking, GV-specific)
- Identify expiration timeline (which cookies expire first?)
- Track rotation patterns (which cookies change between captures?)
- Detect which cookies are required vs optional for API access
- Diff cookies between two capture sessions to see what changed

### Cookie Storage (for headless usage)
- Encrypt at rest (AES-256, existing `TokenEncryption.cs`)
- Health check: test if stored cookies still work
- Refresh strategy: prioritize short-lived cookies (COMPASS > PSIDRTS > SIDCC)
- Alert when cookies are within N days of expiry
