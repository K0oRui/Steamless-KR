namespace Steamless.Tests
{
    using System;
    using System.IO;

    /// <summary>
    /// A throwaway folder for one test. Deleting it on dispose keeps the machine clean even when a
    /// check fails halfway through.
    /// </summary>
    internal sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            this.Root = Path.Combine(Path.GetTempPath(), "steamless-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.Root);
        }

        public string Root { get; }

        public string Write(string name, byte[] data)
        {
            var path = Path.Combine(this.Root, name);
            File.WriteAllBytes(path, data);
            return path;
        }

        public string PathFor(string name)
        {
            return Path.Combine(this.Root, name);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(this.Root, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
