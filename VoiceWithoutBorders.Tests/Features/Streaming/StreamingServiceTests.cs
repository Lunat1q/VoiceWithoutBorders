using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VoiceWithoutBorders.Features.Settings;
using VoiceWithoutBorders.Features.Streaming;
using VoiceWithoutBorders.Infrastructure.Audio;
using VoiceWithoutBorders.Infrastructure.Network;
using VoiceWithoutBorders.Models;

namespace VoiceWithoutBorders.Tests.Features.Streaming;

public class StreamingServiceTests
{
    private static readonly AppSettings Settings = new()
    {
        PeerHost = "OFFICE-PC",
        Port = 15300,
        SharedKey = "0123456789abcdef",
        MaxLatencyMs = 80,
        MicCaptureDeviceId = "mic",
        SystemPlaybackDeviceId = "headset",
    };

    private static readonly IPEndPoint Peer = new(IPAddress.Parse("192.168.1.20"), 15300);

    private readonly Mock<IPeerTransport> _transport = new();
    private readonly Mock<IAudioEndpointFactory> _endpoints = new();
    private readonly Mock<IAudioCapture> _capture = new();
    private readonly Mock<IAudioPlayer> _player = new();
    private readonly StreamingService _sut;

    public StreamingServiceTests()
    {
        _transport.Setup(t => t.OpenAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peer);
        _endpoints.Setup(e => e.CreateCapture("mic", false)).Returns(_capture.Object);
        _endpoints.Setup(e => e.CreatePlayer("headset", TimeSpan.FromMilliseconds(80))).Returns(_player.Object);
        _sut = new StreamingService(_transport.Object, _endpoints.Object, NullLogger<StreamingService>.Instance);
    }

    [Fact]
    public async Task StartAsync_OpensTransportAndOnlyEnabledDevices()
    {
        var peer = await _sut.StartAsync(Settings);

        peer.Should().Be(Peer);
        _sut.IsRunning.Should().BeTrue();
        _transport.Verify(t => t.OpenAsync("OFFICE-PC", 15300, "0123456789abcdef", It.IsAny<CancellationToken>()));
        _capture.Verify(c => c.Start());
        _endpoints.Verify(e => e.CreateCapture(It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
        _endpoints.Verify(e => e.CreatePlayer(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact]
    public async Task CapturedChunk_IsSentAsItsStreamKind()
    {
        await _sut.StartAsync(Settings);

        _capture.Raise(c => c.ChunkReady += null, new ArraySegment<byte>([1, 2, 3, 4]));

        _transport.Verify(t => t.Send(StreamKind.Microphone, It.Is<ArraySegment<byte>>(s => s.Count == 4)));
    }

    [Fact]
    public async Task ReceivedAudio_PlaysOnDeviceForThatStream_AndIgnoresDisabledStream()
    {
        await _sut.StartAsync(Settings);

        _transport.Raise(t => t.AudioReceived += null, StreamKind.SystemAudio, new ArraySegment<byte>([1, 2, 3, 4]));
        _transport.Raise(t => t.AudioReceived += null, StreamKind.Microphone, new ArraySegment<byte>([5, 6, 7, 8]));

        _player.Verify(p => p.Write(It.IsAny<ArraySegment<byte>>()), Times.Once);
    }

    [Fact]
    public async Task Stop_ReleasesEverythingAndStopsPlayback()
    {
        await _sut.StartAsync(Settings);

        _sut.Stop();
        _transport.Raise(t => t.AudioReceived += null, StreamKind.SystemAudio, new ArraySegment<byte>([1, 2, 3, 4]));

        _sut.IsRunning.Should().BeFalse();
        _capture.Verify(c => c.Dispose());
        _player.Verify(p => p.Dispose());
        _player.Verify(p => p.Write(It.IsAny<ArraySegment<byte>>()), Times.Never);
        _transport.Verify(t => t.Close(), Times.AtLeast(2)); // once on start (reset), once on stop
    }

    [Fact]
    public async Task StartAsync_DeviceFails_ClosesWhatWasOpenedAndRethrows()
    {
        _endpoints.Setup(e => e.CreateCapture("mic", false)).Throws(new InvalidOperationException("device gone"));

        var act = () => _sut.StartAsync(Settings);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("device gone");
        _sut.IsRunning.Should().BeFalse();
        _player.Verify(p => p.Dispose());
    }

    [Fact]
    public async Task Stop_DisposeFailure_StillClosesTransport()
    {
        _capture.Setup(c => c.Dispose()).Throws(new InvalidOperationException("unplugged"));
        await _sut.StartAsync(Settings);

        _sut.Stop();

        _player.Verify(p => p.Dispose());
        _sut.IsRunning.Should().BeFalse();
    }
}
