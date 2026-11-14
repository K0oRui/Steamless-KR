namespace Steamless.Tests
{
    using System;
    using System.IO;
    using Steamless.API.PE;
    using Steamless.API.Services;

    /// <summary>
    /// Checks the one place the tool reads a file and the one place it writes bytes. The fixtures are
    /// built by <see cref="PeBuilder"/>, so every case is a real PE image of a known shape.
    /// </summary>
    internal static class PeImageTests
    {
        private const int FileHeader = PeBuilder.NtOffset + 4;

        private const int Optional = PeBuilder.NtOffset + 24;

        private const int SectionTable = Optional + 224;

        public static void LoadsBothFlavours()
        {
            foreach (var is64Bit in new[] { false, true })
            {
                using var dir = new TestDirectory();
                var path = dir.Write("sample.exe", PeBuilder.Build(is64Bit, null, PeBuilder.Sections));

                var result = PeImage.Load(path);
                Assert.True(result.Success, $"{(is64Bit ? "PE32+" : "PE32")} was rejected: {result.Message}");

                var image = result.Image!;
                Assert.Equal(is64Bit, image.Is64Bit, "bitness");
                Assert.Equal((ushort)2, image.NumberOfSections, "section count");
                Assert.Equal(0x1010u, image.AddressOfEntryPoint, "entry point");
                Assert.Equal(is64Bit ? 0x0000000140000000UL : 0x00400000UL, image.ImageBase, "image base");
                Assert.Equal(0x3000u, image.SizeOfImage, "image size");
                Assert.Equal(PeLoadFailure.None, result.Failure, "failure code");
                Assert.Equal("the file is a PE image", result.Message, "load message");

                Assert.True(image.HasSection(".text"), ".text is missing");
                Assert.Equal(0x600UL, image.FileOffsetFromRva(0x2000), ".data file offset");
                Assert.Equal(".data", image.FindSectionAtRva(0x2000)?.Name, ".data lookup by RVA");
                Assert.True(image.FindSectionAtRva(0x9000) == null, "an RVA outside every section was claimed");
            }
        }

        public static void RejectsBrokenFiles()
        {
            var good = PeBuilder.Build(false, null, PeBuilder.Sections);

            using var dir = new TestDirectory();
            var missing = PeImage.Load(dir.PathFor("nope.exe"));
            Assert.False(missing.Success, "a missing file was accepted");
            Assert.Equal(PeLoadFailure.FileMissing, missing.Failure, "missing file reason");

            ExpectFailure("too small", good[..40], PeLoadFailure.TooSmall);
            ExpectFailure("no MZ", Mutate(good, b => b[0] = 0x00), PeLoadFailure.NotDosExecutable);
            ExpectFailure("NT offset inside the DOS header", Mutate(good, b => PeBuilder.WriteUInt32(b, 0x3C, 2)), PeLoadFailure.BadNtOffset);
            ExpectFailure("no PE signature", Mutate(good, b => PeBuilder.WriteUInt32(b, 0x3C, 0x40)), PeLoadFailure.NotPortableExecutable);
            ExpectFailure("no sections", Mutate(good, b => PeBuilder.WriteUInt16(b, FileHeader + 2, 0)), PeLoadFailure.NoSections);
            ExpectFailure("optional header past EOF", Mutate(good, b => PeBuilder.WriteUInt16(b, FileHeader + 16, 0xFFFF)), PeLoadFailure.OptionalHeaderOutsideFile);
            ExpectFailure("optional header too small", Mutate(good, b => PeBuilder.WriteUInt16(b, FileHeader + 16, 200)), PeLoadFailure.OptionalHeaderTruncated);

            // A zero-size optional header at the end of the file must return a rejection instead
            // of reaching the magic-word read and throwing.
            var emptyOptional = good[..Optional];
            PeBuilder.WriteUInt16(emptyOptional, FileHeader + 16, 0);
            ExpectFailure("optional header at EOF", emptyOptional, PeLoadFailure.OptionalHeaderOutsideFile);
            ExpectFailure("bad optional magic", Mutate(good, b => PeBuilder.WriteUInt16(b, Optional, 0x1FF)), PeLoadFailure.UnsupportedOptionalHeader);
            ExpectFailure("file alignment of three", Mutate(good, b => PeBuilder.WriteUInt32(b, Optional + 36, 3)), PeLoadFailure.BadAlignment);
            ExpectFailure("section table past EOF", Mutate(good, b => PeBuilder.WriteUInt16(b, FileHeader + 2, 0x400)), PeLoadFailure.SectionTableOutsideFile);
            ExpectFailure("section body past EOF", Mutate(good, b => PeBuilder.WriteUInt32(b, SectionTable + 16, 0x7FFFFF)), PeLoadFailure.SectionOutsideFile);
            ExpectFailure("section body at offset zero", Mutate(good, b => PeBuilder.WriteUInt32(b, SectionTable + 20, 0)), PeLoadFailure.SectionOverlapsHeaders);
            ExpectFailure("section body inside the headers", Mutate(good, b => PeBuilder.WriteUInt32(b, SectionTable + 20, 0x40)), PeLoadFailure.SectionOverlapsHeaders);
        }

        public static void AcceptsBodylessSectionAtOffsetZero()
        {
            // A section with no body may point anywhere, including zero; it writes nothing back.
            var bytes = Mutate(
                PeBuilder.Build(false, null, PeBuilder.Sections),
                b =>
                {
                    PeBuilder.WriteUInt32(b, SectionTable + 16, 0);
                    PeBuilder.WriteUInt32(b, SectionTable + 20, 0);
                });

            using var dir = new TestDirectory();
            var result = PeImage.Load(dir.Write("bodyless.exe", bytes));
            Assert.True(result.Success, $"a bodyless section at zero was rejected: {result.Message}");
        }

        public static void SaveTreatsABodylessLastSectionAsTheEndOfTheSections()
        {
            // The last section has no body and points at zero, which the reader allows. The save
            // must not take that pointer as the overlay start, or it appends the whole input to
            // itself and the unpacked file comes out twice the size.
            var bytes = PeBuilder.Build(false, null, new[]
            {
                (".text", 0x1000u, PeBuilder.Pattern(0x11, 0x200)),
                (".bss", 0x2000u, Array.Empty<byte>()),
            });

            const int BssPointerToRawData = SectionTable + 40 + 20;
            PeBuilder.WriteUInt32(bytes, BssPointerToRawData, 0);

            using var dir = new TestDirectory();
            var result = PeImage.Load(dir.Write("input.exe", bytes));
            Assert.True(result.Success, $"a bodyless last section was rejected: {result.Message}");

            var output = dir.PathFor("output.exe");
            Assert.True(result.Image!.Save(output, false), "save failed");
            Assert.SameBytes(bytes, File.ReadAllBytes(output), "saved bodyless file");
        }

        public static void RepairsHeaderPointersAfterSectionRemoval()
        {
            // One import descriptor in .rdata that the repair can find, plus a .bind section to remove.
            var rdata = PeBuilder.Pattern(0x33, 0x200);
            PeBuilder.WriteUInt32(rdata, 0, 0x2080); // OriginalFirstThunk
            PeBuilder.WriteUInt32(rdata, 12, 0x2100); // Name
            PeBuilder.WriteUInt32(rdata, 16, 0x2080); // FirstThunk
            Array.Copy(System.Text.Encoding.ASCII.GetBytes("kernel32.dll\0"), 0, rdata, 0x100, 13);

            var original = PeBuilder.Build(false, PeBuilder.Pattern(0x77, 0x40), new[]
            {
                (".text", 0x1000u, PeBuilder.Pattern(0x11, 0x200)),
                (".rdata", 0x2000u, rdata),
                (".bind", 0x3000u, PeBuilder.Pattern(0x44, 0x200)),
            });

            using var dir = new TestDirectory();
            var image = PeImage.Load(dir.Write("packed.exe", original)).Image!;

            var bind = image.FindSection(".bind")!;
            image.ImportTable = new PeDataDirectory(bind.VirtualAddress, 0x28);
            image.CertificateTable = new PeDataDirectory(0x1234, 0x20);

            Assert.True(image.RemoveSection(bind), "the section was not removed");
            image.RebuildSections();
            image.RepairHeaderPointers(bind.VirtualAddress, bind.VirtualSize, new LoggingService());

            var rdataSection = image.FindSection(".rdata")!;
            Assert.Equal(rdataSection.VirtualAddress, image.ImportTable.VirtualAddress, "repaired import table");

            var output = dir.PathFor("unpacked.exe");
            Assert.True(image.Save(output, false), "save failed");

            var reloaded = PeImage.Load(output).Image!;
            var last = reloaded.FindSection(".rdata")!;
            var overlayStart = last.PointerToRawData + last.SizeOfRawData;

            Assert.Equal(rdataSection.VirtualAddress, reloaded.ImportTable.VirtualAddress, "saved import table");
            Assert.Equal(overlayStart, reloaded.CertificateTable.VirtualAddress, "saved certificate table");

            // The overlay moved up over the removed section and is still the overlay's bytes.
            Assert.BytesInRange(0x77, File.ReadAllBytes(output), (int)overlayStart, 0x40, "overlay after the removal");
        }

        public static void SaveReproducesTheFile()
        {
            var original = PeBuilder.Build(false, new byte[] { 0xA1, 0xA2, 0xA3, 0xA4 }, PeBuilder.Sections);

            using var dir = new TestDirectory();
            var input = dir.Write("input.exe", original);
            var output = dir.PathFor("output.exe");

            var image = PeImage.Load(input).Image!;
            Assert.True(image.Save(output, false), "save failed");
            Assert.SameBytes(original, File.ReadAllBytes(output), "saved file");
            Assert.False(File.Exists(output + ".tmp"), "the temporary file was left behind");
        }

        public static void SaveReplacesOneSection()
        {
            var original = PeBuilder.Build(false, null, PeBuilder.Sections);
            var replacement = PeBuilder.Pattern(0x99, 0x200);

            using var dir = new TestDirectory();
            var input = dir.Write("input.exe", original);
            var output = dir.PathFor("output.exe");

            var image = PeImage.Load(input).Image!;
            var data = image.FindSection(".data");
            Assert.True(data != null, ".data is missing");
            image.ReplaceSection(data, replacement);
            Assert.True(image.Save(output, false), "save failed");

            var saved = File.ReadAllBytes(output);

            // Headers must not move: the replacement only changes the section body.
            Assert.SameBytes(original[..PeBuilder.HeadersSize], saved[..PeBuilder.HeadersSize], "headers");
            Assert.BytesInRange(0x99, saved, 0x600, 0x200, "replaced .data body");
            Assert.BytesInRange(0x11, saved, 0x400, 0x200, "untouched .text body");
        }

        public static void SaveComputesAndClearsTheChecksum()
        {
            // One extra overlay byte makes the file length odd, which tests the checksum's final-byte
            // padding.
            var original = PeBuilder.Build(false, new byte[] { 0xAA }, PeBuilder.Sections);
            PeBuilder.WriteUInt32(original, PeBuilder.CheckSumOffset, 0xDEADBEEF);

            using var dir = new TestDirectory();
            var input = dir.Write("input.exe", original);

            var image = PeImage.Load(input).Image!;

            var computedPath = dir.PathFor("computed.exe");
            Assert.True(image.Save(computedPath, true), "save with checksum failed");

            var computed = File.ReadAllBytes(computedPath);
            var stored = BitConverter.ToUInt32(computed, PeBuilder.CheckSumOffset);
            Assert.Equal(ComputeChecksum(computed), stored, "written checksum");
            Assert.True(stored != 0, "the checksum was left at zero");

            var clearedPath = dir.PathFor("cleared.exe");
            Assert.True(image.Save(clearedPath, false), "save without checksum failed");
            Assert.Equal(0u, BitConverter.ToUInt32(File.ReadAllBytes(clearedPath), PeBuilder.CheckSumOffset), "checksum field when not recalculated");
        }

        public static void AChangedBodyReachesTheFileOnlyAfterReplace()
        {
            var original = PeBuilder.Build(false, null, PeBuilder.Sections);

            using var dir = new TestDirectory();
            var input = dir.Write("input.exe", original);
            var before = dir.PathFor("before.exe");
            var after = dir.PathFor("after.exe");

            var image = PeImage.Load(input).Image!;
            var data = image.FindSection(".data");
            Assert.True(data != null, ".data is missing");

            var body = image.ReadSection(data);
            body[0] = 0x99;

            Assert.True(image.Save(before, false), "save without a replacement failed");
            Assert.Equal((byte)0x22, File.ReadAllBytes(before)[0x600], "the edited copy reached the file without a replace");

            image.ReplaceSection(data, body);
            Assert.True(image.Save(after, false), "save with a replacement failed");
            Assert.Equal((byte)0x99, File.ReadAllBytes(after)[0x600], "the replacement did not reach the file");
        }

        public static void ReplaceSectionRejectsAForeignOrOversizedBody()
        {
            var original = PeBuilder.Build(false, null, PeBuilder.Sections);

            using var dir = new TestDirectory();
            var image = PeImage.Load(dir.Write("input.exe", original)).Image!;
            var data = image.FindSection(".data")!;

            Assert.Throws<ArgumentException>(
                () => image.ReplaceSection(data, new byte[image.ReadSection(data).Length + 1]),
                "an oversized body");

            var other = PeImage.Load(dir.Write("other.exe", original)).Image!;
            Assert.Throws<ArgumentException>(
                () => image.ReplaceSection(other.FindSection(".data")!, new byte[0x200]),
                "a section from another image");
        }

        public static void SaveDropsARemovedSection()
        {
            var original = PeBuilder.Build(false, null, PeBuilder.Sections);

            using var dir = new TestDirectory();
            var input = dir.Write("input.exe", original);
            var output = dir.PathFor("output.exe");

            var image = PeImage.Load(input).Image!;
            Assert.True(image.RemoveSection(image.FindSection(".data")), "the section was not removed");
            image.RebuildSections();

            Assert.True(image.Save(output, false), "save failed");

            var reloaded = PeImage.Load(output);
            Assert.True(reloaded.Success, $"the output does not parse: {reloaded.Message}");
            Assert.Equal((ushort)1, reloaded.Image!.NumberOfSections, "section count");
            Assert.False(reloaded.Image!.HasSection(".data"), ".data survived the removal");
            Assert.Equal(0x2000u, reloaded.Image!.SizeOfImage, "image size after the removal");

            var saved = File.ReadAllBytes(output);
            Assert.BytesInRange(0x11, saved, 0x400, 0x200, "remaining .text body");
        }

        public static void SaveHandlesARealignedSectionAtTheEndOfTheFile()
        {
            // A section whose declared size is not aligned and that ends the file: realigning
            // rounds the size up past the bytes the file holds, and the save must pad, not read
            // past the buffer.
            var bytes = PeBuilder.Build(false, null, new[] { (".text", 0x1000u, PeBuilder.Pattern(0x11, 0x1F0)) });
            Array.Resize(ref bytes, PeBuilder.HeadersSize + 0x1F0);
            PeBuilder.WriteUInt32(bytes, SectionTable + 16, 0x1F0);

            using var dir = new TestDirectory();
            var image = PeImage.Load(dir.Write("input.exe", bytes)).Image!;
            image.RebuildSections();

            // Reading the section after the realign must not run past the buffer either.
            var body = image.ReadSection(image.FindSection(".text")!);
            Assert.Equal(0x200, body.Length, "section buffer size");
            Assert.BytesInRange(0x11, body, 0, 0x1F0, "section body read");
            Assert.BytesInRange(0x00, body, 0x1F0, 0x10, "alignment padding read");

            var output = dir.PathFor("output.exe");
            Assert.True(image.Save(output, false), "save failed");

            var saved = File.ReadAllBytes(output);
            Assert.Equal(PeBuilder.HeadersSize + 0x200, saved.Length, "saved length");
            Assert.BytesInRange(0x11, saved, PeBuilder.HeadersSize, 0x1F0, "section body");
            Assert.BytesInRange(0x00, saved, PeBuilder.HeadersSize + 0x1F0, 0x10, "alignment padding");
        }

        public static void SaveChecksumsAFileLargerThanOneRead()
        {
            // The checksum helper reads a megabyte at a time; a file that spans several reads has
            // to fold the same value as one read would.
            var original = PeBuilder.Build(false, PeBuilder.Pattern(0xA7, (0x100000 * 2) + 0x201), PeBuilder.Sections);

            using var dir = new TestDirectory();
            var image = PeImage.Load(dir.Write("input.exe", original)).Image!;
            var output = dir.PathFor("output.exe");
            Assert.True(image.Save(output, true), "save failed");

            var computed = File.ReadAllBytes(output);
            Assert.Equal(ComputeChecksum(computed), BitConverter.ToUInt32(computed, PeBuilder.CheckSumOffset), "written checksum");
        }

        private static void ExpectFailure(string name, byte[] bytes, PeLoadFailure expected)
        {
            using var dir = new TestDirectory();
            var result = PeImage.Load(dir.Write("bad.exe", bytes));
            Assert.False(result.Success, $"{name}: the file was accepted");
            Assert.Equal(expected, result.Failure, $"{name}: wrong reason");
        }

        private static byte[] Mutate(byte[] source, Action<byte[]> change)
        {
            var copy = (byte[])source.Clone();
            change(copy);
            return copy;
        }

        /// <summary>
        /// The PE checksum written a second time, straight from the spec, so a change in the
        /// production fold shows up here instead of in a file the loader never validates.
        /// </summary>
        private static uint ComputeChecksum(byte[] file)
        {
            ulong sum = 0;
            var pairs = file.Length & ~1;

            for (var x = 0; x < pairs; x += 2)
            {
                // The field is four bytes, so both words that cover it are read as zero.
                var inField = x >= PeBuilder.CheckSumOffset && x < PeBuilder.CheckSumOffset + 4;
                var word = inField ? 0u : (uint)(file[x] | (file[x + 1] << 8));
                sum += word;
                sum = (sum & 0xFFFF) + (sum >> 16);
            }

            if ((file.Length & 1) != 0)
            {
                sum += file[file.Length - 1];
                sum = (sum & 0xFFFF) + (sum >> 16);
            }

            sum = (sum & 0xFFFF) + (sum >> 16);
            sum = (sum & 0xFFFF) + (sum >> 16);

            return (uint)(sum + (ulong)file.Length);
        }
    }
}
