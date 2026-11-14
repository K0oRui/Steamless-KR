namespace Steamless.Tests
{
    using Steamless.API.PE;
    /// <summary>
    /// The pattern scan every variant rule depends on, plus the range check the stub readers use
    /// before they trust a size or an offset from the file.
    /// </summary>
    internal static class PeHelpersTests
    {
        public static void FindPatternHandlesWildcards()
        {
            var data = new byte[] { 0x90, 0x48, 0x8B, 0x05, 0x10, 0x20, 0x30, 0x40, 0xCC };

            Assert.Equal(1L, PeHelpers.FindPattern(data, "48 8B 05"), "plain pattern");
            Assert.Equal(1L, PeHelpers.FindPattern(data, "48 8B 05 ?? ?? ?? ??"), "trailing wildcards");
            Assert.Equal(1L, PeHelpers.FindPattern(data, "?? 8B 05 ?? ?? ?? ??"), "leading wildcard");
            Assert.Equal(0L, PeHelpers.FindPattern(data, "90 48 8b 05"), "lower case hex");
            Assert.Equal(-1L, PeHelpers.FindPattern(data, "48 8B 06"), "absent pattern");
            Assert.Equal(-1L, PeHelpers.FindPattern(data, "4G 8B"), "malformed pattern");
            Assert.Equal(-1L, PeHelpers.FindPattern(data, "48 8B 05 10 20 30 40 41"), "pattern longer than data");
            Assert.Equal(0L, PeHelpers.FindPattern(data, "?? ?? ??"), "an all-wildcard pattern matches at zero");
        }

        public static void IsValidRangeRejectsNegativeAndOverflowingRanges()
        {
            var data = new byte[16];

            Assert.True(PeHelpers.IsValidRange(data, 0, 16), "the whole buffer is a valid range");
            Assert.True(PeHelpers.IsValidRange(data, 16, 0), "an empty range at the end is valid");
            Assert.False(PeHelpers.IsValidRange(data, -1, 1), "a negative offset was accepted");
            Assert.False(PeHelpers.IsValidRange(data, 0, -1), "a negative size was accepted");
            Assert.False(PeHelpers.IsValidRange(data, 15, 2), "a range past the end was accepted");
            Assert.False(PeHelpers.IsValidRange(null!, 0, 0), "a null buffer was accepted");
            Assert.False(PeHelpers.IsValidRange(data, long.MaxValue, 1), "an offset at long.MaxValue was accepted");
        }
    }
}
