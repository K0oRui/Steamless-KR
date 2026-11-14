namespace Steamless.Tests
{
    using Steamless.API.Model;
    using Steamless.API.PE;
    using Steamless.Unpacker.Common;

    /// <summary>
    /// The rules the plugin base owns for every variant: the prologue claim, the 32-bit default,
    /// the magic a stub writes before its entry point, and the bounds-checked header read. A
    /// stand-in plugin supplies only what a real one supplies, so no variant DLL is involved.
    /// </summary>
    internal static class StubUnpackerTests
    {
        private const string Prologue = "DE AD BE EF";

        private sealed class TestUnpacker : StubUnpacker<UnpackRun>
        {
            protected override string VariantName => "Test Variant";

            protected override string BindPrologue => Prologue;

            protected override bool ReadHeader(UnpackRun run) => false;

            protected override bool Unpack(UnpackRun run, SteamlessOptions options) => false;

            protected override uint GetEntryPoint(UnpackRun run) => 0;

            public bool HasMagic(PeImage file, uint magic) => HasHeaderMagic(file, magic);

            public bool ReadWindow(PeImage file, ulong rva, uint size, out byte[] data) => TryReadHeaderWindow(file, rva, size, out data);
        }

        public static void TheDefaultClaimLooksForThePrologue()
        {
            using var dir = new TestDirectory();
            var plugin = new TestUnpacker();

            var packed = Load(dir, "packed.exe", false, Packed(PeBuilder.Pattern(0x11, 0x200)));
            Assert.True(plugin.CanProcessFile(packed), "a .bind section with the prologue is claimed");

            var plain = Load(dir, "plain.exe", false, new[]
            {
                (".text", 0x1000u, PeBuilder.Pattern(0x11, 0x200)),
                (".bind", 0x2000u, PeBuilder.Pattern(0x22, 8)),
            });

            Assert.False(plugin.CanProcessFile(plain), "a .bind section without the prologue is not claimed");
        }

        public static void TheDefaultClaimRejects64BitFiles()
        {
            using var dir = new TestDirectory();
            var plugin = new TestUnpacker();
            var wide = Load(dir, "packed64.exe", true, Packed(PeBuilder.Pattern(0x11, 0x200)));

            Assert.False(plugin.CanProcessFile(wide), "the default claim is 32-bit only");
        }

        public static void AHeaderWindowOutsideTheFileIsRejected()
        {
            using var dir = new TestDirectory();
            var plugin = new TestUnpacker();
            var image = Load(dir, "window.exe", false, Packed(PeBuilder.Pattern(0x11, 0x200)));

            Assert.False(plugin.ReadWindow(image, 0x1000, 0x4000, out _), "a size past the end of the file");
            Assert.False(plugin.ReadWindow(image, 0x1000, 0, out _), "a zero size");

            Assert.True(plugin.ReadWindow(image, 0x1000, 0x200, out var data), "a header inside the file");
            Assert.Equal(0x11, data[0], "the window starts at the section body");
        }

        public static void TheHeaderMagicSitsBeforeTheEntryPoint()
        {
            using var dir = new TestDirectory();
            var plugin = new TestUnpacker();

            // The builder puts the entry point at .text + 0x10, so the magic lands at + 0x0C.
            var text = PeBuilder.Pattern(0x11, 0x200);
            PeBuilder.WriteUInt32(text, 0x0C, 0xC0DEC0DE);

            var image = Load(dir, "magic.exe", false, Packed(text));

            Assert.True(plugin.HasMagic(image, 0xC0DEC0DE), "the magic at entry - 4");
            Assert.False(plugin.HasMagic(image, 0xC0DEC0DF), "a different magic");
        }

        private static (string Name, uint VirtualAddress, byte[] Body)[] Packed(byte[] text)
        {
            return new[]
            {
                (".text", 0x1000u, text),
                (".bind", 0x2000u, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x90, 0x90 }),
            };
        }

        private static PeImage Load(TestDirectory dir, string name, bool is64Bit, (string Name, uint VirtualAddress, byte[] Body)[] sections)
        {
            var path = dir.Write(name, PeBuilder.Build(is64Bit, null, sections));
            var loaded = PeImage.Load(path);

            Assert.True(loaded.Success, $"{name} is a PE image");
            return loaded.Image!;
        }
    }
}
