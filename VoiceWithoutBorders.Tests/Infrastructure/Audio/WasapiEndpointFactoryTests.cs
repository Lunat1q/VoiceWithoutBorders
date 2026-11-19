using System.Runtime.InteropServices;
using FluentAssertions;
using VoiceWithoutBorders.Infrastructure.Audio;

namespace VoiceWithoutBorders.Tests.Infrastructure.Audio;

public class WasapiEndpointFactoryTests
{
    private sealed class FakeDevice(string id) : IDisposable
    {
        public string Id { get; } = id;
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private static FakeDevice NotFound(string _) =>
        throw new COMException("Core Audio error 0x80070490", WasapiEndpointFactory.ElementNotFound);

    [Fact]
    public void Resolve_LookupWorks_ReturnsItWithoutEnumerating()
    {
        var device = new FakeDevice("a");

        var result = WasapiEndpointFactory.Resolve("a", _ => device, () => throw new InvalidOperationException("enumerated"), d => d.Id);

        result.Should().BeSameAs(device);
    }

    // Regression: Sonar re-created its endpoints at sign-in; lookup by id kept failing until the app restarted.
    [Fact]
    public void Resolve_LookupSaysNotFound_FindsDeviceAmongActiveOnes_AndDisposesTheRest()
    {
        var other = new FakeDevice("{0.0.1.00000000}.{other}");
        var wanted = new FakeDevice("{0.0.1.00000000}.{00DD7D20}");

        var result = WasapiEndpointFactory.Resolve("{0.0.1.00000000}.{00dd7d20}", NotFound, () => [other, wanted], d => d.Id);

        result.Should().BeSameAs(wanted);
        wanted.Disposed.Should().BeFalse();
        other.Disposed.Should().BeTrue();
    }

    [Fact]
    public void Resolve_DeviceGoneEverywhere_ThrowsReadableMessage()
    {
        var other = new FakeDevice("b");

        var act = () => WasapiEndpointFactory.Resolve("a", NotFound, () => [other], d => d.Id);

        act.Should().Throw<InvalidOperationException>().WithMessage("*not available*");
        other.Disposed.Should().BeTrue();
    }

    [Fact]
    public void Resolve_OtherError_IsNotSwallowed()
    {
        var act = () => WasapiEndpointFactory.Resolve<FakeDevice>("a", _ => throw new COMException("busy", unchecked((int)0x88890004)),
            () => throw new InvalidOperationException("enumerated"), d => d.Id);

        act.Should().Throw<COMException>().WithMessage("busy");
    }
}
