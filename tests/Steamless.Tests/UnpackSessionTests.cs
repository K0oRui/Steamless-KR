namespace Steamless.Tests
{
    using System.Threading;
    using Steamless.API.Model;
    using Steamless.API.Services;

    /// <summary>
    /// Folder input and the run report. A session with no plugin folder installed still expands the
    /// input, which is all these checks need: they are about which files reach the plugins and how a
    /// failure is counted, not about unpacking.
    /// </summary>
    internal static class UnpackSessionTests
    {
        public static void FolderInputFindsUpperCaseExecutables()
        {
            using var dir = new TestDirectory();

            var sample = PeBuilder.Build(false, null, PeBuilder.Sections);
            dir.Write("GAME.EXE", sample);
            dir.Write("other.exe", sample);
            dir.Write("other.exe.unpacked.exe", sample);
            dir.Write("notes.txt", new byte[] { 1, 2, 3 });

            using var session = UnpackSession.Load(dir.PathFor("plugins"), new LoggingService());
            var report = session.Run(new[] { dir.Root }, session.Selections[0], new SteamlessOptions(), CancellationToken.None);

            Assert.Equal(2, report.Outcomes.Count, "files offered to the plugins");

            foreach (var outcome in report.Outcomes)
            {
                Assert.False(outcome.Path.EndsWith(".unpacked.exe", System.StringComparison.OrdinalIgnoreCase), "a previous output was offered as input");
                Assert.Equal(UnpackStatus.Skipped, outcome.Status, "status without a plugin");
            }
        }

        public static void ReportCountsEveryOutcome()
        {
            var report = new UnpackReport(new[]
            {
                new UnpackOutcome("a.exe", UnpackStatus.Unpacked),
                new UnpackOutcome("b.exe", UnpackStatus.Failed),
                new UnpackOutcome("c.exe", UnpackStatus.Skipped),
                new UnpackOutcome("d.exe", UnpackStatus.Failed),
            });

            Assert.Equal(1, report.Unpacked, "unpacked count");
            Assert.Equal(2, report.Failed, "failed count");
            Assert.Equal(1, report.Skipped, "skipped count");
        }
    }
}
