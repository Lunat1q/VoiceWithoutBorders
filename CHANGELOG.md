# Changelog

## Unreleased

- Default *Max latency* is now 200 ms (existing settings keep their value).
- Audio resumes by itself after either computer sleeps or hibernates: when the other computer answers again, or this one wakes up, streaming reopens the devices and resolves the other computer's address again.
- *Start with Windows* (into the tray) and *Then connect by itself* on the Connection tab.
- Only one copy runs: launching it again brings up the running window.

## 0.1.0 — 2026-10-20

First public release, licensed under GPL-3.0-or-later.

### Audio
- Send your microphone and/or whatever an output device is playing (WASAPI loopback) to another PC on the network.
- The other PC's microphone shows up as a microphone here through a virtual cable (e.g. VB-CABLE's "CABLE Output"); its sound plays on any output device.
- Per-stream on/off while streaming; a stream that is off is neither sent nor played.
- Latency-capped jitter buffer with a configurable *Max latency*.

### Connection
- AES-256-GCM encrypted UDP with a shared key (generated for you, shown so you can type it on the other PC).
- Heartbeat every second: Connected / Waiting / Different key status, and it opens the Windows Firewall path both ways so audio never waits for the other side to speak first.
- Works with computer names that resolve to IPv4 or IPv6, or a plain IP address.

### Seeing that it works
- Sending / Receiving activity lights.
- Latency readout: measured network round trip plus the estimated delay of each incoming stream, colour-coded, with a warning when audio has to be skipped to keep up.

### Everyday use
- Tabs: Stream, Connection, Devices, Hotkeys & overlay.
- Minimize to tray; tray menu for Start/Stop, toggles and Exit.
- Always-on-top, click-through overlay with live level bars; unlock it to drag it anywhere.
- Global hotkeys (default Ctrl+Alt+M mic, Ctrl+Alt+S sound, optional start/stop), all rebindable, with a clear message when a combination is taken.
- Settings save as you change them; the shared key is stored encrypted with Windows DPAPI.
