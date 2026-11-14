namespace Steamless.Tests
{
    using System;
    using Steamless.API.Model;
    using Steamless.API.Services;

    /// <summary>
    /// One run of a session: it hands back the report, it stops when cancelled, and it cannot be
    /// started twice. The plugins are in memory, so a check owns the timing.
    /// </summary>
    internal static class SessionRunTests
    {
        public static void ARunReturnsItsReport()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("Claimer");

            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, new LoggingService());
            var run = new SessionRun(session, session.Selections[0], new[] { file }, new SteamlessOptions());

            var report = run.StartAsync().GetAwaiter().GetResult();

            Assert.Equal(1, report.Outcomes.Count, "outcomes");
            Assert.Equal(UnpackStatus.Unpacked, report.Outcomes[0].Status, "status");
            Assert.False(run.IsRunning, "the run has finished");
        }

        public static void CancellingARunStopsIt()
        {
            using var dir = new TestDirectory();
            var first = dir.Write("first.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var second = dir.Write("second.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("Claimer");

            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, new LoggingService());
            var run = new SessionRun(session, session.Selections[0], new[] { first, second }, new SteamlessOptions());

            // Cancelling while the plugin holds the first file leaves the run to stop before the
            // second one, the same boundary a cancel button owns.
            plugin.OnProcess = run.Cancel;

            Assert.Throws<OperationCanceledException>(
                () => run.StartAsync().GetAwaiter().GetResult(),
                "a cancelled run");

            Assert.False(run.IsRunning, "the run has stopped");
        }

        public static void ARunStartsOnce()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("Claimer");

            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, new LoggingService());
            var run = new SessionRun(session, session.Selections[0], new[] { file }, new SteamlessOptions());

            run.StartAsync().GetAwaiter().GetResult();

            Assert.Throws<InvalidOperationException>(
                () => run.StartAsync().GetAwaiter().GetResult(),
                "a second start");

            Assert.Equal(1, plugin.ProcessCalls, "process calls after a second start");
        }
    }
}
