# Google Voice Signaler Protocol — Wire Format Analysis

**Date:** 2026-03-29
**Method:** CDP Fetch interception on live Chrome (debug port 9222)
**Captures:** 2 calls (1 incoming, 1 outgoing) + idle monitoring

---

## Protocol Overview

The signaler uses Google's proprietary **BrowserChannel** long-poll protocol (not WebSocket). Communication flows over HTTP GET (server→client) and POST (client→server) with a persistent session.

### Session Lifecycle

```
1. POST /punctual/v1/chooseServer
   → Server assignment + session token

2. POST /punctual/multi-watch/channel?VER=8&RID={rid}&CVER=22
   Body: URL-encoded subscription requests (count=N&ofs=0&req0___data__=...)
   → SID (session ID) + initial ack

3. GET /punctual/multi-watch/channel?VER=8&SID={sid}&AID={aid}&RID=rpc&TYPE=xmlhttp
   → Long-poll (blocks until data available, ~4 minute timeout)
   → Returns: sequenced event array [[seqNum, payload], ...]

4. POST /punctual/v1/refreshCreds (periodic, ~4 minutes)
   → Refreshes auth credentials for the channel session
```

---

## Wire Format

### Server → Client (GET long-poll responses)

Responses are length-prefixed, concatenated JSON arrays:

```
{length}\n{json_array}
```

Example idle response:
```
14
[[9,["noop"]]]
```

Example event with data:
```
272
[[10,[[[["3",[null,[[[null,"W10=","1774799438679","1774799438997055",
  ["1774799438970839","1774799438970459","1774799438971122",
   "1774799438996250","1774799438997055","1774799439072006",
   "1774799439072202",null,"1774799439072240"],
  [[[null,"-933742000135554229"]]],
  0]]]]]]]]]]
```

### Message Structure

Each response contains: `[[seqNum, payload], ...]`

**Sequence numbers** are monotonically increasing per session. The client must ACK via `AID` parameter on next GET.

### Event Types Observed

| Payload | Type | Purpose |
|---|---|---|
| `["noop"]` | Keepalive | Sent every ~30 seconds to maintain connection |
| `["c","sessionId","",8,14,30000]` | Session init | Session created, config (VER=8, timeout=30000ms) |
| `[[null,null,["token"]]]` | Auth token | Session-level auth token |
| `[[[["N",[...]]]]]` | Channel data | Data for subscription channel N |
| `[[[["N",[null,null,["timestamp"]]]],...]]` | State sync | All channels get timestamp update |

### Channel Subscriptions

The initial POST subscribes to **6 channels** (numbered 1-6):

| Channel | Likely Purpose |
|---|---|
| 1 | Call signaling (SDP, hangup, ringing) |
| 2 | Messages/SMS notifications |
| 3 | Voicemail notifications |
| 4 | Thread state changes |
| 5 | Presence/availability |
| 6 | Account state changes |

Evidence: After a call, channel 3 fires first (with `W10=` base64 data and timestamps), then channels 1, 4, 5, 2 follow with the same timestamp, indicating a fan-out notification.

### Call Signaling Data

When a call occurs, the signaler pushes data with this structure:
```json
["3",[null,[[[null,
  "W10=",                           // Base64 payload (often "[]" = empty)
  "1774799438679",                  // Event timestamp (epoch ms)
  "1774799438997055",               // Server timestamp (microseconds)
  ["1774799438970839",              // Timing array (internal routing timestamps)
   "1774799438970459",
   "1774799438971122",
   "1774799438996250",
   "1774799438997055",
   "1774799439072006",
   "1774799439072202",
   null,
   "1774799439072240"],
  [[[null,"-933742000135554229"]]],  // Correlation ID (negative = outgoing?)
  0                                  // Status/flags
]]]]]
```

The `W10=` is base64 for `[]` (empty array). For SDP data, this would contain the actual SDP offer/answer payload.

### State Sync Messages

After call completion, all 6 channels receive a timestamp sync:
```json
[[[["4",[null,null,["1774799457439230"]]],
   ["2",[null,null,["1774799457439230"]]],
   ["1",[null,null,["1774799457439230"]]],
   ["3",[null,null,["1774799457439230"]]],
   ["6",[null,null,["1774799457439230"]]],
   ["5",[null,null,["1774799457439230"]]]]]]
```
This tells the client "all channels are current up to this timestamp."

---

## Client → Server (POST messages)

### chooseServer Request
```json
[[null,null,null,[9,5],null,[null,[null,1],[[["3"]]]]],0.06286,1774799437529365,0,0]
```
- `[9,5]` = version/protocol identifiers
- `0.06286` = client load/timing metric
- Timestamp in microseconds

### chooseServer Response
```json
["UCKpzAzKmkzz_4hxZqhU7NxSWIl2hB2fUlzqh1iEw9g",3,null,"1774799437516918","1774799437517008"]
```
- `[0]` = Session token (gsessionid)
- `[1]` = Server assignment (3 = server pool index)
- `[3]` = Server start timestamp
- `[4]` = Server ack timestamp

### Channel Open Request (POST, URL-encoded)
```
count=6&ofs=0
&req0___data__=[[subscription_config_channel_1]]
&req1___data__=[[subscription_config_channel_2]]
&req2___data__=[[subscription_config_channel_3]]
&req3___data__=[[subscription_config_channel_4]]
&req4___data__=[[subscription_config_channel_5]]
&req5___data__=[[subscription_config_channel_6]]
```

### Channel Open Response
```json
[[0,["c","MXCIgJxRxH56Vmv_7U0BIQ","",8,14,30000]]]
```
- `"c"` = channel type
- `"MXCIgJxRxH56Vmv_7U0BIQ"` = SID (session ID)
- `8` = VER
- `14` = CVER
- `30000` = timeout (ms)

### refreshCreds
```json
Request: ["qkhFDq7R"]
Response: []
```
The request sends a token; empty response = success.

---

## Call Sequence (observed)

### Outgoing Call
```
T+0s    POST chooseServer → session token
T+0s    POST channel (subscribe 6 channels) → SID
T+0s    GET channel (long-poll starts)
T+1s    [Channel 3 fires] Call initiation data (W10= + timestamps + correlation ID)
T+1s    [Channel 1 fires] Same event echoed to channel 1
T+2s    [Channels 4,2,5 fire] Event propagation
T+5s    POST threadinginfo/get → unread count refresh
T+5s    POST api2thread/list → call list refresh (new outgoing call record)
T+8s    [noop] Keepalive
T+10s   [All channels] State sync timestamp
```

### Incoming Call
```
T+0s    [Channel 3 fires] Incoming call notification (different correlation ID format)
T+0s    [Channel 1 fires] Call signaling
T+2s    [Channels 4,2,5 fire] Event propagation
T+5s    POST threadinginfo/get → unread count refresh
T+5s    POST api2thread/list → call list refresh (new incoming call record, duration=7s)
T+8s    [noop] Keepalive
T+10s   [All channels] State sync timestamp
```

### Distinguishing Call Direction
- Outgoing call: correlation ID is **negative** (e.g., `-933742000135554229`)
- Incoming call: correlation ID is **positive** (e.g., `3814686511807353743`)

---

## Key Protocol Parameters

| Parameter | Location | Purpose |
|---|---|---|
| `VER=8` | Query string | BrowserChannel protocol version |
| `CVER=22` | Query string | Client version |
| `SID` | Query string | Session ID (from channel open) |
| `AID` | Query string | Acknowledgment ID (last received seqNum) |
| `RID` | Query string | Request ID (incrementing per POST) |
| `gsessionid` | Query string | Global session (from chooseServer) |
| `TYPE=xmlhttp` | Query string | Response format (chunked) |
| `key` | Query string | API key |

---

## New Endpoints Discovered

| Endpoint | Method | Purpose |
|---|---|---|
| `punctual/v1/refreshCreds` | POST | Refresh auth credentials for signaler session |
| `punctual/v1/chooseServer` | POST | Server assignment (now with full request/response format) |
| `punctual/multi-watch/channel` | POST | Open channel + subscribe (with URL-encoded subscription data) |
| `punctual/multi-watch/channel` | GET | Long-poll for events (TYPE=xmlhttp) |
