namespace VoiceWithoutBorders.Infrastructure.Updates;

public interface IUpdateChecker
{
    /// <summary>The newest release, or null when this copy is current or the check failed (offline is not news).</summary>
    Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default);
}
