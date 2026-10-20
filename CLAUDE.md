# Voice Without Borders (VWB)

Windows desktop app that shares microphone and system sound between two PCs on a LAN over encrypted UDP. Each side can send its mic and/or loopback of an output device, and play the peer's streams on chosen output devices (remote mic usually goes into a virtual cable so apps see it as a mic).

## Commands

| Task | Command | From |
|------|---------|------|
| Build | `dotnet build VoiceWithoutBorders.slnx` | repo root |
| Tests | `dotnet test VoiceWithoutBorders.slnx` | repo root |
| Portable build (single self-contained exe) | `dotnet publish VoiceWithoutBorders/VoiceWithoutBorders.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish/win-x64` | repo root |

**Do NOT launch the app from agent sessions** (no `dotnet run`): it is a GUI that opens audio devices and a UDP port. Build and test only. If real-device behaviour must be checked, use a throwaway console project in a scratch folder and play into a virtual device, never the user's speakers.

### Ports
- UDP `15300` (default, user-configurable). Both peers bind and send to the same port.

## Tech Stack

| Layer | Tech |
|-------|------|
| Runtime | .NET 10 (`net10.0-windows`) |
| UI | WPF, `ThemeMode="System"` (Fluent) |
| MVVM | CommunityToolkit.Mvvm (source-generated `[ObservableProperty]`, `[RelayCommand]`) |
| DI / logging / config | Microsoft.Extensions.Hosting (generic host) |
| Audio | NAudio 3 (`WasapiRecorder`, `WasapiPlayer`, `MMDeviceEnumerator`) |
| Transport | UDP (`UdpClient`), AES-256-GCM, PBKDF2-SHA256 |
| Settings | JSON in `%AppData%\VoiceWithoutBorders`, shared key via DPAPI |
| Tests | xUnit + FluentAssertions + Moq |

## Architecture Principles

### SOLID throughout
- **S**: each class one responsibility (capture, playback, framing/crypto, transport, orchestration, view model)
- **O**: new stream kinds or transports plug in behind existing interfaces
- **L**: fakes/mocks are drop-in for real implementations
- **I**: focused interfaces (`IAudioCapture`, `IAudioPlayer`, `IPeerTransport`…), no god interfaces
- **D**: depend on abstractions, inject via the DI container

### Feature-sliced layout
`Features/` holds user-facing slices (view, view model, service, settings). `Infrastructure/` holds the OS/network adapters they depend on. One type per file. See @.claude/docs/architecture.md for the structure tree and data flow.

### File size discipline
- **Hard limit: ~300 lines per file.**
- Window over ~300 lines of XAML → extract `UserControl`s into the feature folder.
- View model over ~300 lines → split into child view models.
- Same for services: split into focused sub-services.

## Coding Conventions

### C#
- `record` types for settings/DTOs; `readonly record struct` for small value snapshots.
- `async/await` throughout — no `.Result` / `.Wait()`.
- `ILogger<T>` for structured logging. Never log the shared key or derived key.
- Register all services via extension methods in `Extensions/ServiceCollectionExtensions.cs`.
- Validate user input at the boundary (`AppSettings.Validate()` before starting).
- Warnings are errors (`TreatWarningsAsErrors`).

### WPF / MVVM
- Views bind to view models; no business logic in code-behind. Code-behind is allowed only for view-only glue (e.g. `PasswordBox`, timers).
- View models never reference WPF types, so they are unit-testable without a dispatcher.
- Every input gets a `Label` with `Target` (keyboard access key + screen reader name).
- Shared styles live in `Window.Resources` / `App.xaml`; one-off tweaks inline.

### Audio and threading
- Wire format is fixed: 48 kHz, 16-bit, stereo PCM, 5 ms (960-byte) chunks (`WireFormat`).
- Capture events and network receive run on background threads. Anything they read must be safe to read concurrently (`StreamingService` swaps an immutable player map rather than mutating it).
- Capture buffers are reused: consumers must use a chunk before returning.

## Testing Requirements

- **Every change must include unit tests** — features, bug fixes, refactors.
- Bug fixes must include a regression test reproducing the original issue.
- Mock `IPeerTransport`, `IAudioEndpointFactory`, `IAudioDeviceService`, `ISettingsStore`, `IStreamingService` with Moq.
- Pure logic (`PacketCodec`, `WireFormat`, `LatencyCappedBuffer`, `AppSettings`) is tested directly, without devices or sockets.
- Tests must not open real audio devices or bind sockets.

## Security

- Every packet is AES-256-GCM authenticated; drop anything that fails `PacketCodec.TryDecode`.
- Never send, log or display the shared key. On disk it is DPAPI-protected (CurrentUser).
- Shared key minimum length is enforced (`PacketCodec.MinSharedKeyLength`).
- Changing the packet layout requires bumping `PacketCodec.Version`.
- LAN only: no NAT traversal, no relay, no listening on anything but the configured port.

## Domain Concepts

- **StreamKind**: `Microphone` or `SystemAudio`. Each machine sends at most one of each and plays each on its own device.
- **Peer**: the other computer (name, IPv4 or IPv6 address). Exactly one peer per session. The socket is dual-stack; names often resolve to IPv6 only (LLMNR/mDNS), so never filter to IPv4.
- **Session**: random id per codec instance; sequence numbers per stream kind reject duplicates and late packets.
- **Max latency**: jitter buffer cap; on overflow the buffer drops to half the cap.
