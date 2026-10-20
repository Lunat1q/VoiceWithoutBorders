# Changelog

## Unreleased

- Minimize to tray, with a tray menu for Start/Stop, toggles and Exit.
- Always-on-top, click-through overlay with live level bars per stream while streaming.
- Global hotkeys (default Ctrl+Alt+M / Ctrl+Alt+S) to turn the mic / sound stream on and off, plus an optional start/stop hotkey; all rebindable, with a clear message when a combination is taken.
- Window split into Stream, Connection, Devices and Hotkeys & overlay tabs; none needs scrolling.
- Overlay can be unlocked, dragged anywhere and locked again; position is remembered.
- Fix: tray menu shortcuts were glued to the item text.

- Shared key is shown in plain text, generated automatically on first run, and can be regenerated with **New**.
- Status shows the IP address the peer name resolved to.
- Fix: computer names that resolve only to IPv6 failed with "No such host is known". The socket is now dual-stack (IPv4 + IPv6).
- Clearer message when a computer name cannot be found.
- Warning with a VB-CABLE link when the computer has no playback devices.
- "Its microphone becomes" lists virtual microphones (e.g. CABLE Output) instead of playback devices; the app plays into the cable's matching playback side.

## 0.1.0 — 2026-10-20

- Send microphone and/or system sound (WASAPI loopback) to another PC over UDP.
- Play the peer's microphone and system sound on chosen output devices (virtual cable for remote mic).
- AES-256-GCM encrypted packets with a shared key; duplicate, late and reflected packets dropped.
- Latency-capped jitter buffer with configurable max latency.
- Settings saved to `%AppData%`, shared key protected with DPAPI.
- Live packet counters with a hint when the shared keys differ.
