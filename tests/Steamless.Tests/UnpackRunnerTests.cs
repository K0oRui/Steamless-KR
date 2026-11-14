namespace Steamless.Tests
{
    using System;
    using System.Linq;
    using Steamless.API.Events;
    using Steamless.API.Model;
    using Steamless.API.Services;

    /// <summary>
    /// The app's run: it hands back the report, ends a multi-file run with one total, logs a
    /// cancellation, contains a throw and refuses a second start. The plugins are in memory, so a
    /// check owns the timing and no host is involved.
    /// </summary>
    internal static class UnpackRunnerTests
    {
        public static void ARunReturnsItsReport()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("Claimer");
            var service = new LoggingService();

            using var feed = new LogFeed(service, () => true);
            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, service);
            var runner = new UnpackRunner(session, feed);

            var runningInside = false;
            plugin.OnProcess = () => runningInside = runner.IsRunning;

            var report = runner.RunAsync(new[] { file }, session.Selections[0], new SteamlessOptions()).GetAwaiter().GetResult();

            Assert.True(report != null, "a report");
            Assert.Equal(1, report!.Outcomes.Count, "outcomes");
            Assert.Equal(UnpackStatus.Unpacked, report.Outcomes[0].Status, "status");
            Assert.True(runningInside, "running while the plugin holds a file");
            Assert.False(runner.IsRunning, "the run has finished");
            Assert.False(feed.Entries.Any(entry => entry.Message.StartsWith("Finished:")), "a single file needs no total");
        }

        public static void AMultiFileRunEndsWithOneTotal()
        {
            using var dir = new TestDirectory();
            var first = dir.Write("first.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var second = dir.Write("second.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("Claimer");
            var service = new LoggingService();

            using var feed = new LogFeed(service, () => true);
            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, service);
            var runner = new UnpackRunner(session, feed);

            var report = runner.RunAsync(new[] { first, second }, session.Selections[0], new SteamlessOptions()).GetAwaiter().GetResult();

            Assert.Equal(2, report!.Outcomes.Count, "outcomes");

            var total = feed.Entries[feed.Entries.Count - 1];
            Assert.Equal("Finished: 2 unpacked, 0 failed, 0 skipped.", total.Message, "the total");
            Assert.Equal(LogMessageType.Information, total.MessageType, "the total's severity");
        }

        public static void ACancelledRunIsLogged()
        {
            using var dir = new TestDirectory();
            var first = dir.Write("first.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var second = dir.Write("second.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("Claimer");
            var service = new LoggingService();

            using var feed = new LogFeed(service, () => true);
            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, service);
            var runner = new UnpackRunner(session, feed);

            // Cancelling while the plugin holds the first file leaves the run to stop before the
            // second one, the same boundary a cancel button owns.
            plugin.OnProcess = runner.Cancel;

            var report = runner.RunAsync(new[] { first, second }, session.Selections[0], new SteamlessOptions()).GetAwaiter().GetResult();

            Assert.True(report == null, "no report");
            Assert.False(runner.IsRunning, "the run has stopped");
            Assert.True(feed.Entries.Any(entry => entry.Message == "Unpack cancelled."), "the cancel line");
        }

        public static void ARunThatThrowsIsContained()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("Claimer");
            var service = new LoggingService();

            using var feed = new LogFeed(service, () => true);
            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, service);
            var runner = new UnpackRunner(session, feed);

            // A disposed session fails before a plugin is reached, which is the case the session's
            // own per-plugin guard cannot cover.
            session.Dispose();

            var report = runner.RunAsync(new[] { file }, session.Selections[0], new SteamlessOptions()).GetAwaiter().GetResult();

            Assert.True(report == null, "no report");
            Assert.False(runner.IsRunning, "the run has stopped");
            Assert.True(feed.Entries.Any(entry => entry.Message.StartsWith("The unpack stopped:")), "the failure line");
        }

        public static void ASecondRunIsRefusedWhileOneIsInFlight()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("Claimer");
            var service = new LoggingService();

            using var feed = new LogFeed(service, () => true);
            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, service);
            var runner = new UnpackRunner(session, feed);

            UnpackReport? second = null;
            plugin.OnProcess = () => second = runner.RunAsync(new[] { file }, session.Selections[0], new SteamlessOptions()).GetAwaiter().GetResult();

            var first = runner.RunAsync(new[] { file }, session.Selections[0], new SteamlessOptions()).GetAwaiter().GetResult();

            Assert.True(first != null, "the first run reports");
            Assert.True(second == null, "the second run is refused");
            Assert.Equal(1, plugin.ProcessCalls, "unpack calls");
            Assert.False(runner.IsRunning, "the run has finished");
        }
    }
}
