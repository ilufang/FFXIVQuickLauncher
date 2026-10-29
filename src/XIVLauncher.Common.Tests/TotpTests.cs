using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Tests
{
    [TestClass]
    public class TotpTests
    {
        // RFC 6238 Appendix B seeds
        private const string SEED_SHA1 = "12345678901234567890";
        private const string SEED_SHA256 = "12345678901234567890123456789012";
        private const string SEED_SHA512 = "1234567890123456789012345678901234567890123456789012345678901234";

        private static string Base32Encode(string ascii)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var sb = new StringBuilder();
            int buffer = 0, bitsLeft = 0;

            foreach (var b in Encoding.ASCII.GetBytes(ascii))
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;

                while (bitsLeft >= 5)
                {
                    bitsLeft -= 5;
                    sb.Append(alphabet[(buffer >> bitsLeft) & 0x1F]);
                }
            }

            if (bitsLeft > 0)
                sb.Append(alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);

            return sb.ToString();
        }

        private static Totp Parse(string uri)
        {
            Assert.IsTrue(Totp.TryParse(uri, out var totp), uri);
            return totp!;
        }

        [DataTestMethod]
        [DataRow("SHA1", SEED_SHA1, 59L, "94287082")]
        [DataRow("SHA1", SEED_SHA1, 1111111109L, "07081804")]
        [DataRow("SHA1", SEED_SHA1, 20000000000L, "65353130")]
        [DataRow("SHA256", SEED_SHA256, 59L, "46119246")]
        [DataRow("SHA256", SEED_SHA256, 1111111109L, "68084774")]
        [DataRow("SHA256", SEED_SHA256, 20000000000L, "77737706")]
        [DataRow("SHA512", SEED_SHA512, 59L, "90693936")]
        [DataRow("SHA512", SEED_SHA512, 1111111109L, "25091201")]
        [DataRow("SHA512", SEED_SHA512, 20000000000L, "47863826")]
        public void ComputesRfc6238Vectors(string algorithm, string seed, long time, string expected)
        {
            var totp = Parse($"otpauth://totp/Test?secret={Base32Encode(seed)}&algorithm={algorithm}&digits=8");
            Assert.AreEqual(expected, totp.Compute(DateTimeOffset.FromUnixTimeSeconds(time)));
        }

        [TestMethod]
        public void DefaultsToSixDigitsSha1()
        {
            var totp = Parse("otpauth://totp/Test?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ");
            Assert.AreEqual("287082", totp.Compute(DateTimeOffset.FromUnixTimeSeconds(59)));
            Assert.AreEqual("081804", totp.Compute(DateTimeOffset.FromUnixTimeSeconds(1111111109)));
        }

        [TestMethod]
        public void ComputesTimeUntilNextStep()
        {
            var totp = Parse("otpauth://totp/Test?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ");
            Assert.AreEqual(TimeSpan.FromSeconds(1), totp.TimeUntilNextStep(DateTimeOffset.FromUnixTimeSeconds(59)));
            Assert.AreEqual(TimeSpan.FromSeconds(30), totp.TimeUntilNextStep(DateTimeOffset.FromUnixTimeSeconds(60)));

            var custom = Parse("otpauth://totp/Test?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&period=60");
            Assert.AreEqual(TimeSpan.FromSeconds(50), custom.TimeUntilNextStep(DateTimeOffset.FromUnixTimeSeconds(70)));
        }

        [TestMethod]
        public void ParsesTolerantInput()
        {
            var expected = Parse("otpauth://totp/Test?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ").Compute(DateTimeOffset.FromUnixTimeSeconds(59));

            Assert.AreEqual(expected, Parse("otpauth://totp/Test?secret=gezdgnbvgy3tqojqgezdgnbvgy3tqojq").Compute(DateTimeOffset.FromUnixTimeSeconds(59)));
            Assert.AreEqual(expected, Parse("OTPAUTH://TOTP/Test?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ====").Compute(DateTimeOffset.FromUnixTimeSeconds(59)));
            Assert.AreEqual(expected, Parse("  otpauth://totp/SQUARE%20ENIX:user%40example?issuer=SQUARE%20ENIX&secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&image=foo  ")
                                          .Compute(DateTimeOffset.FromUnixTimeSeconds(59)));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ")]
        [DataRow("https://totp/Test?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ")]
        [DataRow("otpauth://hotp/Test?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&counter=0")]
        [DataRow("otpauth://totp/Test")]
        [DataRow("otpauth://totp/Test?secret=")]
        [DataRow("otpauth://totp/Test?secret=NOT-BASE32!")]
        [DataRow("otpauth://totp/Test?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&algorithm=MD5")]
        [DataRow("otpauth://totp/Test?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&digits=abc")]
        [DataRow("otpauth://totp/Test?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&period=0")]
        public void RejectsInvalidUris(string? uri)
        {
            Assert.IsFalse(Totp.TryParse(uri, out var totp));
            Assert.IsNull(totp);
        }
    }
}
