# Google Voice Internal API Reference

**Date:** 2026-03-27
**Method:** Playwright MCP browser interception + WebRTC monkey-patching
**Sessions:** 5 capture sessions, 56 total requests, 20 unique endpoints

---

## Protocol Overview

All Voice Client API endpoints use:
- **Base URL:** `https://clients6.google.com/voice/v1/voiceclient/`
- **Method:** POST
- **Content-Type:** `application/json+protobuf` (protobuf serialized as JSON arrays)
- **Query Params:** `alt=protojson&key={GV_API_KEY}`
- **Note:** `{GV_API_KEY}` is Google Voice's public browser-embedded API key. It is visible in the GV web app's JavaScript source to any authenticated user. Store it in configuration (e.g., `appsettings.json` or environment variable `GV_API_KEY`), not in source.
- **Auth:** Cookie-based (GAPS token, SAPISID hash) — injected by browser session

gRPC-Web services use:
- **Base URLs:** `people-pa.clients6.google.com`, `peoplestack-pa.clients6.google.com`, `voice.clients6.google.com`
- **Path prefix:** `/$rpc/<ServiceName>/<MethodName>`
- **Content-Type:** `application/x-protobuf` or `application/grpc-web+proto`

---

## Voice Client API Endpoints (14)

### 1. `account/get`
**Purpose:** Full account configuration including phone numbers, devices, linked numbers, settings.
**Request:** `[null,1]`
**Response:** 28KB protobuf-JSON with nested arrays:
- `[0]` — Primary phone number (e.g., `+19196706660`)
- `[2]` — Array of phone configurations with feature flags
- `[4]` — Country settings
- `[5]` — Linked devices (name, type, forwarding settings)
**Notes:** This is the single source of truth for all settings. The Settings UI renders entirely from this response.

### 2. `account/update`
**Purpose:** Update account settings.
**Request:** Sparse array with only changed fields populated. Example (first VoIP use):
```json
[[null,null,null,null,null,null,null,[[[
  "moya-voip-audio-settings-first-account-use"
]]]]]
```
**Response:** Full account object (same as `account/get`).

### 3. `api2thread/list`
**Purpose:** List conversation threads by type with pagination.
**Request:** `[<type>,<pageSize>,<flags>,<cursor>,null,[null,1,1,1]]`
**Type codes:**
| Code | Tab |
|---|---|
| 1 | Calls |
| 2 | Messages (all) |
| 3 | Messages (SMS only) |
| 4 | Voicemail |
| 5 | Spam |
| 6 | Archive |

**Response:** Array of thread objects. Each thread contains:
- `[0]` — Thread ID (`t.+1XXXXXXXXXX` for SMS, `c.<base64>` for calls, `g.Group Message.<id>` for MMS)
- `[1]` — Read status (0=unread, 1=read)
- `[2]` — Array of message/event objects within the thread

**Message object fields:**
- `[0]` — Message hash ID
- `[1]` — Timestamp (epoch ms)
- `[2]` — Account phone number
- `[3]` — Remote party info `[phone, phone, null, null, null, null, 0]`
- `[4]` — Message type code (see below)
- `[5]` — Read status
- `[6]` — Voicemail transcription (array of `[confidence, [[word, null, null, confidence], ...]]`)
- `[7]` — (unused)
- `[8]` — Duration in seconds (for calls)
- `[9]` — Tag/label
- `[10]` — Voicemail audio URL (for older messages)
- `[11]` — SMS text content
- `[14]` — Direction code
- `[15]` — Read flag

**Message type codes:**
| Code | Type |
|---|---|
| 1 | Incoming call (answered) |
| 2 | Voicemail |
| 10 | Received SMS |
| 11 | Sent SMS |
| 14 | Outgoing call |

### 4. `api2thread/get`
**Purpose:** Get a single thread with full message history (paginated).
**Request:** `["<threadId>",<pageSize>,"<cursor>",[null,1,1]]`
**Response:** Same structure as individual thread in `api2thread/list`.

### 5. `api2thread/sendsms`
**Purpose:** Send an SMS message.
**Request:** `[null,null,null,null,"<message text>","<threadId>",null,null,[<deviceId>]]`
- Thread ID format: `t.+1XXXXXXXXXX`
- Device ID: numeric (e.g., `207431086612864`)
**Response:** `[null,"<threadId>","<messageHash>",<timestamp>,[1,2]]`

### 6. `api2thread/search`
**Purpose:** Full-text search across all threads.
**Request:** `["<query>",<maxResults>]`
**Response:** Array of matching thread objects (same format as `api2thread/list`).

### 7. `thread/batchupdateattributes`
**Purpose:** Multi-purpose thread management. The attribute position determines the operation.
**Request:** `[[["<threadId>",<attr1>,<attr2>,<attr3>,<attr4>,<attr5>],[<mask1>,<mask2>,<mask3>,<mask4>,<mask5>],1]]]`

**Operations by attribute position:**
| Position | Value | Mask | Operation | Response Flag |
|---|---|---|---|---|
| 3rd | `1` | `1` | Mark as read | `[1,4]` or `[1,2]` |
| 5th | `1` | `1` | Archive | `[6]`, archived=`1` |
| 2nd | `1` | `1` | Mark as spam | `[5]`, flag=`2` |
| 1st | `1` | `1` | Mark as unread | unread=`1` |

### 8. `thread/batchdelete`
**Purpose:** Permanently delete one or more threads.
**Request:** `[[["<threadId1>","<threadId2>",...]]]`
**Response:** `[]` (empty on success)

### 9. `thread/markallread`
**Purpose:** Mark all threads as read.
**Request:** `[]`
**Response:** `[]`

### 10. `threadinginfo/get`
**Purpose:** Get unread counts per tab.
**Request:** `[]`
**Response:** `[[[[<calls>,null,<count>],[<vm>,null,<count>],[<msgs>,null,<count>],[<spam>,null,<count>],[<archive>,null,<count>]]]]`
- Position 0: Calls (type 1)
- Position 1: Voicemail (type 4)
- Position 2: Messages (type 3)
- Position 3: Spam (type 5)
- Position 4: Archive (type 2)
- Position 5: Archive (type 6, appears after archiving)

### 11. `sipregisterinfo/get`
**Purpose:** Get SIP registration tokens for WebRTC VoIP.
**Request:** `[3,"<deviceId>"]`
**Response:** `[["<sipToken>",<expiry>],null,null,["<authToken>","<cryptoKey>"]]`

### 12. `getnumberportinfo`
**Purpose:** Check number porting status.
**Request/Response:** `[]` (empty when no port in progress)

### 13. `numbertransfer/list`
**Purpose:** Check number transfer status.
**Request/Response:** `[]` (empty when no transfer in progress)

### 14. `inboundcallrule/list`
**Purpose:** List custom call forwarding rules.
**Request:** `[]`
**Response:** Array of rule objects.

---

## gRPC-Web Services (6 endpoints)

### PeopleStack Autocomplete
- `Autocomplete` — Contact typeahead search (powers search bar dropdown)
- `Lookup` — Resolve contact details by ID
- `Warmup` — Pre-warm contact search index

### Internal People Service
- `GetContactGroups` — Get contact group definitions
- `ListContactGroups` — List all contact groups

### Ring Groups
- `ListRingGroups` — List multi-device ring group configurations

---

## WebRTC Call Signaling

### STUN Server
`stun:stun.l.google.com:19302`

### Outgoing Call Flow
1. Browser creates SDP **offer** (audio-only, Opus/G722/PCMU/PCMA)
2. Offer sent via signaler long-poll channel
3. Google's SIP UA "xavier" responds with SDP **answer** from `74.125.39.0/24`
4. `ice-lite` — Google presents fixed media relay endpoint
5. DTLS-SRTP handshake, then Opus audio flows
6. Connection time: ~1.3 seconds

### Incoming Call Flow
1. Google sends SDP **offer** via signaler push
2. Browser creates SDP **answer** (initially `recvonly`)
3. Audio connected in ~260ms
4. **SDP renegotiation** after ~6 seconds (Google re-offers, browser responds with full ICE candidates)
5. Different Google media server IP than outgoing calls

### Audio Codecs (negotiated)
- `opus/48000/2` (primary — 48kHz stereo)
- `red/48000/2` (redundancy for FEC)
- `G722/8000` (fallback)
- `PCMU/8000` (fallback)
- `PCMA/8000` (fallback)
- `telephone-event/48000` + `telephone-event/8000` (DTMF)

### Google Media Relay
- IPs: `74.125.39.0/24` (observed: .87 outgoing, .157 incoming)
- Port: `26500` (UDP)
- IPv6: `2001:4860:4864:2::*:26500`
- Role: SIP-to-WebRTC gateway bridging to PSTN

---

## Real-Time Push (Signaler)

**Server selection:** `POST signaler-pa.clients6.google.com/punctual/v1/chooseServer`
**Channel:** `GET/POST signaler-pa.clients6.google.com/punctual/multi-watch/channel`
- Uses Google's proprietary long-polling protocol (VER=8, CVER=22)
- Carries: incoming call signaling, SMS notifications, presence updates, SDP offers/answers
- Session maintained via `gsessionid` + `SID` + `AID` parameters

---

## Voicemail Audio Delivery

Audio is **not** fetched via a separate API call. It's delivered as a signed URL embedded in the `api2thread/list` response:
```
https://www.google.com/voice/media/svm/<messageId>/<signedToken>
```
The browser uses Web Audio API (AudioContext) to decode and play — no `<audio>` element is created.

---

## Thread ID Formats

| Prefix | Type | Example |
|---|---|---|
| `t.+1XXXXXXXXXX` | SMS thread (phone number) | `t.+19193718044` |
| `t.XXXXX` | SMS thread (short code) | `t.22000` |
| `c.<base64>` | Call/voicemail thread | `c.PCIGVERSUKVGE4...` |
| `g.Group Message.<id>` | Group MMS thread | `g.Group Message.kFNgB...` |
