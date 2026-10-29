using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace XIVLauncher.Common.Util;

/// <summary>
/// Minimal RFC 6238 TOTP generator, configured from an otpauth://totp/ URI.
/// </summary>
public sealed class Totp
{
    private const string BASE32_ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private readonly byte[] secret;
    private readonly string algorithm;
    private readonly int digits;
    private readonly int period;

    private Totp(byte[] secret, string algorithm, int digits, int period)
    {
        this.secret = secret;
        this.algorithm = algorithm;
        this.digits = digits;
        this.period = period;
    }

    public static bool TryParse(string? uri, out Totp? totp)
    {
        totp = null;

        if (string.IsNullOrWhiteSpace(uri) || !Uri.TryCreate(uri!.Trim(), UriKind.Absolute, out var parsed))
            return false;

        if (!parsed.Scheme.Equals("otpauth", StringComparison.OrdinalIgnoreCase) ||
            !parsed.Host.Equals("totp", StringComparison.OrdinalIgnoreCase))
            return false;

        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in parsed.Query.TrimStart('?').Split('&'))
        {
            var idx = pair.IndexOf('=');
            if (idx <= 0)
                continue;

            query[Uri.UnescapeDataString(pair.Substring(0, idx))] = Uri.UnescapeDataString(pair.Substring(idx + 1));
        }

        if (!query.TryGetValue("secret", out var secretText) || !TryDecodeBase32(secretText, out var secret))
            return false;

        var algorithm = query.TryGetValue("algorithm", out var algorithmText) ? algorithmText.ToUpperInvariant() : "SHA1";
        if (algorithm != "SHA1" && algorithm != "SHA256" && algorithm != "SHA512")
            return false;

        var digits = 6;
        if (query.TryGetValue("digits", out var digitsText) && (!int.TryParse(digitsText, out digits) || digits < 1 || digits > 9))
            return false;

        var period = 30;
        if (query.TryGetValue("period", out var periodText) && (!int.TryParse(periodText, out period) || period < 1))
            return false;

        totp = new Totp(secret, algorithm, digits, period);
        return true;
    }

    public string Compute(DateTimeOffset now)
    {
        var counter = now.ToUnixTimeSeconds() / this.period;

        var counterBytes = new byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counterBytes[i] = (byte)(counter & 0xFF);
            counter >>= 8;
        }

        byte[] hash;
        using (var hmac = CreateHmac())
            hash = hmac.ComputeHash(counterBytes);

        var offset = hash[hash.Length - 1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | (hash[offset + 1] << 16)
                     | (hash[offset + 2] << 8)
                     | hash[offset + 3];

        var code = binary % (int)Math.Pow(10, this.digits);
        return code.ToString().PadLeft(this.digits, '0');
    }

    public TimeSpan TimeUntilNextStep(DateTimeOffset now)
    {
        var seconds = now.ToUnixTimeSeconds();
        return TimeSpan.FromSeconds(this.period - (seconds % this.period));
    }

    private HMAC CreateHmac()
    {
        return this.algorithm switch
        {
            "SHA256" => new HMACSHA256(this.secret),
            "SHA512" => new HMACSHA512(this.secret),
            _ => new HMACSHA1(this.secret),
        };
    }

    private static bool TryDecodeBase32(string text, out byte[] result)
    {
        result = Array.Empty<byte>();

        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in text)
        {
            if (c == '=' || c == ' ')
                continue;

            var value = BASE32_ALPHABET.IndexOf(char.ToUpperInvariant(c));
            if (value < 0)
                return false;

            buffer = (buffer << 5) | value;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        if (output.Count == 0)
            return false;

        result = output.ToArray();
        return true;
    }
}
