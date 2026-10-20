# <img src="VoiceWithoutBorders/Assets/app.png" width="48" align="top" alt=""> Voice Without Borders

Share a microphone and system sound between two Windows PCs on the same network. Think *Mouse Without Borders*, but for audio: plug one headset into one computer and use it for both.

Each computer can **send** its microphone and/or whatever it is playing, and **play** what the other one sends on any output device.

## Typical setup: one headset, two computers

Headset is plugged into **Desk PC**. You also want to talk and listen on **Laptop**.

| | Desk PC (headset here) | Laptop |
|---|---|---|
| My microphone | Headset mic | Off |
| Sound playing on | Off | Speakers (the device apps play to) |
| Play its microphone into | Off | **CABLE Input** (virtual cable) |
| Play its sound on | Headset | Off |

On the laptop, apps (Teams, Discord, Zoom…) then use **CABLE Output** as their microphone. In Windows sound settings, make **CABLE Output** both the *default* and the *default communications* recording device, so apps that just use "default" pick it too.

The laptop's own microphone is not mixed in: Windows never combines microphones, and each app records from exactly one input. Only the headset mic reaches the call, even if you sit right next to the laptop.

### Why a virtual cable?

Windows only lets drivers create microphones. To make the remote mic look like a local one, install a free virtual audio cable such as [VB-CABLE](https://vb-audio.com/Cable/). Play the incoming mic into **CABLE Input**; apps record from **CABLE Output**.

That is why *Play its microphone into* lists playback devices, not microphones: the app plays into the cable's playback side, and the cable hands it to apps as a recording device.

If you pick real speakers for *Play its microphone into* instead, the other computer's voice plays out loud and this computer's own microphone can pick it up again, mixed with the room. The app warns when the device you picked does not look like a virtual cable.

VB-CABLE is not bundled (its license does not allow redistribution); install it from the link above or in the app.

The same applies to a computer with no sound card at all (a server, a VM): it has nothing to play on until a virtual cable is installed. The app shows a warning when it finds no playback devices.

### Avoid feedback loops

- Do not send the sound of a device you also play the incoming sound on: it would be sent straight back.
- Do not play the incoming mic on your speakers while sending their sound.

## Getting started

1. Install the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) on both PCs (or build from source, see below).
2. Run `VoiceWithoutBorders.exe` on both PCs.
3. On each PC enter the **other** computer's name or IP address and the same **UDP port** (default `15300`). The first PC generates a random **shared key**: type that same key on the other PC (or press **New** for a fresh one).
4. Choose the devices, then press **Start** on both.
5. Allow the app through Windows Firewall when asked. The prompt only allows **private** networks by default: if Windows marks your network as *Public* (Settings → Network & internet → your connection → Network profile type), switch it to *Private*, or incoming audio is blocked.

The status line shows the address the name resolved to, plus packets sent, received and rejected. Lots of rejected packets and nothing received usually means the shared keys differ. If the address is wrong (a computer name resolving to a VPN or virtual adapter), enter the IP address instead of the name.

**Max latency** caps the jitter buffer. Lower values feel snappier; raise it if you hear crackles on Wi‑Fi.

## How it works

- Audio is captured with WASAPI (loopback capture for system sound) and converted by Windows to 48 kHz, 16-bit stereo PCM.
- 5 ms chunks are sent over UDP. Lost packets are skipped rather than retransmitted.
- The receiver buffers up to *max latency* and plays on the chosen device. When clock drift fills the buffer it skips ahead.
- Bandwidth is about 1.6 Mbit/s per active stream (uncompressed).

## Security

- Every packet is encrypted and authenticated with **AES-256-GCM**. The key is derived from the shared key with PBKDF2-SHA256 (210,000 iterations).
- Packets that fail authentication, duplicates, late packets and reflected copies of your own packets are dropped.
- The shared key is stored encrypted with Windows DPAPI under your user account (`%AppData%\VoiceWithoutBorders\settings.json`).
- There is no discovery or handshake. Only packets made with the shared key are played.
- Known limitation: someone on your network who records traffic could replay an *earlier* session's audio to you. Use a strong key and keep the port off the internet.
- Designed for a trusted LAN. Do not forward the port through your router.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet build VoiceWithoutBorders.slnx
dotnet test VoiceWithoutBorders.slnx
dotnet run --project VoiceWithoutBorders
```

## Limitations and ideas

- Two computers only; no automatic discovery (enter the name or IP).
- Uncompressed audio. Opus would cut bandwidth by about 20x.
- Unplugging a device while streaming needs a Stop/Start.
- No tray icon or auto-start yet.

## Tech stack

.NET 10 · WPF (Fluent theme) · [NAudio](https://github.com/naudio/NAudio) · CommunityToolkit.Mvvm · Microsoft.Extensions.Hosting · xUnit, FluentAssertions, Moq
