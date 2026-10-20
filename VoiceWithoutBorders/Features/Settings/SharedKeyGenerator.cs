using System.Security.Cryptography;

namespace VoiceWithoutBorders.Features.Settings;

public static class SharedKeyGenerator
{
    // No 0/O or 1/I/L: the key is read off one screen and typed on another.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>Five groups of four, e.g. "K7QM-2XRT-9HCW-PV4N-B3ZE" (about 99 bits).</summary>
    public static string Generate() =>
        string.Join('-', Enumerable.Range(0, 5).Select(_ => new string(RandomNumberGenerator.GetItems<char>(Alphabet, 4))));
}
