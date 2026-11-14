namespace Steamless.Tests
{
    using Steamless.Unpacker.Common;

    /// <summary>
    /// The code-section bit means encrypted in a 2.0 header and the opposite in a 2.1 or 3.x
    /// header. Both readings are pinned, so a change to one cannot quietly spread to the other.
    /// </summary>
    internal static class StubDrmFlagsTests
    {
        public static void A20HeaderSetsTheBitWhenEncrypted()
        {
            Assert.True(StubDrmFlags.EncryptsCodeSectionV20(0x04), "bit set means encrypted");
            Assert.False(StubDrmFlags.EncryptsCodeSectionV20(0x00), "bit clear means plain");
            Assert.False(StubDrmFlags.EncryptsCodeSectionV20(0x10), "another bit does not count");
        }

        public static void A21HeaderClearsTheBitWhenEncrypted()
        {
            Assert.False(StubDrmFlags.EncryptsCodeSectionV21(0x04), "bit set means plain");
            Assert.True(StubDrmFlags.EncryptsCodeSectionV21(0x00), "bit clear means encrypted");
            Assert.True(StubDrmFlags.EncryptsCodeSectionV21(0x10), "another bit does not count");
        }
    }
}
