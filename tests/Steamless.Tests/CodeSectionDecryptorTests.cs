namespace Steamless.Tests
{
    using System;
    using Steamless.API.PE;
    using Steamless.API.Services;
    using Steamless.Unpacker.Common;

    /// <summary>
    /// The size rule the two stub families share. A 3.x section holds every encrypted byte and
    /// wants the same count back; a 2.1 section holds one block fewer, because the stub kept that
    /// block in its payload, so the plaintext is one block longer than the file part. Reading the
    /// 2.1 case as the 3.x case silently loses the last code block.
    /// </summary>
    internal static class CodeSectionDecryptorTests
    {
        private const int SectionSize = 0x400;

        public static void DecryptsTheSectionForBothSizeRules()
        {
            var key = AesFixture.Key;
            var iv = AesFixture.Iv;

            var plain = new byte[SectionSize];
            for (var x = 0; x < plain.Length; x++)
                plain[x] = (byte)((x * 17) + 3);

            var cipher = AesFixture.Encrypt(plain, key, iv);
            var stolen = cipher[..AesFixture.BlockSize];

            // The section keeps the ciphertext from after the stolen block.
            var body = new byte[SectionSize];
            Array.Copy(cipher, AesFixture.BlockSize, body, 0, SectionSize - AesFixture.BlockSize);

            using var dir = new TestDirectory();
            var file = PeImage.Load(dir.Write("packed.exe", PeBuilder.Build(false, null, new[] { (".text", 0x1000u, body) }))).Image!;
            var section = file.FindSection(".text")!;
            var offset = file.FileOffsetFromRva(section.VirtualAddress);
            var log = new LoggingService();

            // A 3.x header counts the whole section and wants every byte back.
            var destination = file.ReadSection(section);
            Assert.True(
                CodeSectionDecryptor.TryDecrypt(file, stolen, offset, SectionSize, SectionSize, destination, key, iv, log),
                "the 3.x sizes failed");
            Assert.SameBytes(plain, destination, "3.x plaintext");

            // A 2.1 header counts the bytes after the stolen block and wants one block more back.
            Array.Clear(destination);
            Assert.True(
                CodeSectionDecryptor.TryDecrypt(file, stolen, offset, SectionSize - AesFixture.BlockSize, SectionSize, destination, key, iv, log),
                "the 2.1 sizes failed");
            Assert.SameBytes(plain, destination, "2.1 plaintext");
        }
    }
}
