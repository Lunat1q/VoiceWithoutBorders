# Changelog

## 0.3.2 — 2026-11-18

- New *Notifications* tab: turn the tray pop-ups off altogether, or only *Connected*, *Lost the connection* or *Different shared key*.

## 0.3.1 — 2026-11-18

- The *Connection* and *Overlay* tabs fit the window again without a scroll bar, also while streaming and after *Find* lists several computers. *Max latency* moved into the *Other computer* box, and *Reset position* sits next to *Unlock to move it*.
- *Check for updates* button on the *Stream* tab: looks for a new release right away instead of only when the app starts, and brings back an offer dismissed with *Not now*.

## 0.3.0 — 2026-11-18

- New *Audio* tab, live while streaming: volume of the other computer's microphone and sound as played here (0–200 %), and a noise gate that sends silence while your microphone stays below a threshold.
- *Push to talk* hotkey: once set, your microphone is sent only while you hold it.
- New *Overlay* tab (split from *Hotkeys & overlay*) with the overlay's look: indicators in a row or a column, size, background opacity (down to 0 %, icons only) and *Hide when quiet*, which fades the overlay out after 5 seconds without sound.
- *Find* on the Connection tab looks for the app on the other computers of your network (same port and key) and fills in the IP address, so you no longer need `ipconfig`. While not streaming, the app keeps its UDP port open to answer these searches.
- The tray icon shows the link with a coloured dot (green connected, amber waiting, red key mismatch), and Windows notifications report when the link comes up, drops or comes back while the window is not in use.
- A device that stops while streaming (headset unplugged, device disabled) no longer leaves that stream silent: streaming reopens everything, retrying for about a minute while the device comes back.
- *Start with Windows* now uses a per-user logon task instead of the Run key, so the app starts right at sign-in instead of after Explorer's startup delay (an existing setting moves over the next time the app opens). *Then connect by itself* retries sooner: after 1 s, 2 s, then every 5 s.

## 0.2.3 — 2026-11-05

- The tray icon's right-click menu no longer opens behind the taskbar, so *Exit* is always clickable.
- The overlay pill has its dark background and border again (lost in 0.2.2).

## 0.2.2 — 2026-10-27

- The overlay level bars react much more to quiet audio: they follow a decibel scale, so normal speech fills about half the bar instead of a sliver, and the bars are taller.
- The overlay shows *No connection* or *Key mismatch* in place of the indicators when the other computer stops answering.
- The overlay keeps its size while you drag it and when you unlock it.

## 0.2.1 — 2026-10-21

- Lower latency and no false "audio is being skipped" warnings: the jitter buffer drops audio that sat unused for a whole second (left behind by late packets or sound card clock drift) instead of letting delay build up to *Max latency*. *Max latency* is now only a ceiling, so 150–200 ms is fine even on a fast LAN.
- The overlay stays on top of other always-on-top windows (borderless games, video players, the taskbar): it raises itself again every second while shown.

## 0.2.0 — 2026-10-21

- Default *Max latency* is now 200 ms (existing settings keep their value).
- Audio resumes by itself after either computer sleeps or hibernates: when the other computer answers again, or this one wakes up, streaming reopens the devices and resolves the other computer's address again.
- *Start with Windows* (into the tray) and *Then connect by itself* on the Connection tab.
- Only one copy runs: launching it again brings up the running window.
- Installer (per user, no admin rights) next to the portable zip, and in-app updates from GitHub Releases: offered on the Stream tab, checked against the release's SHA-256 before running, reopened afterwards (streaming again if it was). The version is in the title bar.

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
