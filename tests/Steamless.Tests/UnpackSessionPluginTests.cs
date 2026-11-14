namespace Steamless.Tests
{
    using System;
    using System.Linq;
    using System.Threading;
    using Steamless.API.Model;
    using Steamless.API.Services;

    /// <summary>
    /// The session's dispatch: which plugin claims a file, what a claim reports, how a plugin that
    /// throws is contained, when a run stops, and who owns the plugins. The plugins are in memory,
    /// so these checks need no folder and no compiled plugin DLL.
    /// </summary>
    internal static class UnpackSessionPluginTests
    {
        public static void AutomaticModeRunsThePluginThatClaims()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("Claimer");

            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, new LoggingService());
            var report = session.Run(new[] { file }, session.Selections[0], new SteamlessOptions(), CancellationToken.None);

            Assert.Equal(1, report.Outcomes.Count, "outcomes");
            Assert.Equal(UnpackStatus.Unpacked, report.Outcomes[0].Status, "status");
            Assert.Equal(1, plugin.ProcessCalls, "process calls");
            Assert.Equal(1, report.Unpacked, "unpacked count");
        }

        public static void AutomaticModeSkipsAPluginThatDoesNotClaim()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var first = new FakePlugin("First") { Claims = false };
            var second = new FakePlugin("Second");

            using var session = new UnpackSession(new SteamlessPlugin[] { first, second }, new LoggingService());
            var report = session.Run(new[] { file }, session.Selections[0], new SteamlessOptions(), CancellationToken.None);

            Assert.Equal(0, first.ProcessCalls, "process calls on the non-claiming plugin");
            Assert.Equal(1, second.ProcessCalls, "process calls on the claiming plugin");
            Assert.Equal(UnpackStatus.Unpacked, report.Outcomes[0].Status, "status");
        }

        public static void AutomaticModeReportsAClaimThatFails()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("Broken") { Unpacks = false };

            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, new LoggingService());
            var report = session.Run(new[] { file }, session.Selections[0], new SteamlessOptions(), CancellationToken.None);

            Assert.Equal(UnpackStatus.Failed, report.Outcomes[0].Status, "status");
            Assert.Equal(1, report.Failed, "failed count");
        }

        public static void AutomaticModeContainsAPluginThatThrowsOnClaim()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("Thrower") { ThrowsOnClaim = true };

            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, new LoggingService());
            var report = session.Run(new[] { file }, session.Selections[0], new SteamlessOptions(), CancellationToken.None);

            Assert.Equal(UnpackStatus.Failed, report.Outcomes[0].Status, "a throw counts as a claim that failed");
            Assert.Equal(0, plugin.ProcessCalls, "process calls after a failed claim");
        }

        public static void AnErroredClaimLetsTheNextPluginTry()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var thrower = new FakePlugin("Thrower") { ThrowsOnClaim = true };
            var claimer = new FakePlugin("Claimer");
            var service = new LoggingService();

            using var feed = new LogFeed(service, () => true);
            using var session = new UnpackSession(new SteamlessPlugin[] { thrower, claimer }, service);

            var report = session.Run(new[] { file }, session.Selections[0], new SteamlessOptions(), CancellationToken.None);

            Assert.Equal(UnpackStatus.Unpacked, report.Outcomes[0].Status, "status");
            Assert.Equal(0, thrower.ProcessCalls, "process calls on the thrower");
            Assert.Equal(1, claimer.ProcessCalls, "process calls on the next plugin");
            Assert.True(
                feed.Entries.Any(entry => entry.Message.EndsWith("Thrower cannot read this file.")),
                "the throw is logged");
        }

        public static void AutomaticModeContainsAPluginThatThrowsOnProcess()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("Thrower") { ThrowsOnProcess = true };

            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, new LoggingService());
            var report = session.Run(new[] { file }, session.Selections[0], new SteamlessOptions(), CancellationToken.None);

            Assert.Equal(UnpackStatus.Failed, report.Outcomes[0].Status, "status");
        }

        public static void AFailedClaimLeavesTheNextPluginACleanFile()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));

            // The first plugin claims the file, removes a section and then gives up, which is what
            // the 2.1 and 3.x unpackers do before their last steps. The second plugin can only
            // claim the file as it was on disk.
            var first = new FakePlugin("First")
            {
                Unpacks = false,
                OnProcessFile = image => image.RemoveSection(image.FindSection(".data")),
            };

            var second = new FakePlugin("Second") { ClaimWhen = image => image.HasSection(".data") };

            using var session = new UnpackSession(new SteamlessPlugin[] { first, second }, new LoggingService());
            var report = session.Run(new[] { file }, session.Selections[0], new SteamlessOptions(), CancellationToken.None);

            Assert.Equal(1, second.ClaimCalls, "claims on the second plugin");
            Assert.Equal(1, second.ProcessCalls, "process calls on the second plugin");
            Assert.Equal(UnpackStatus.Unpacked, report.Outcomes[0].Status, "status");
        }

        public static void ExplicitSelectionRunsOnlyTheChosenPlugin()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var first = new FakePlugin("First");
            var second = new FakePlugin("Second");

            using var session = new UnpackSession(new SteamlessPlugin[] { first, second }, new LoggingService());
            var selected = session.Selections[2];

            Assert.Equal("Second", selected.Name, "third picker entry is the second plugin");

            var report = session.Run(new[] { file }, selected, new SteamlessOptions(), CancellationToken.None);

            Assert.Equal(0, first.ClaimCalls, "claims on the unselected plugin");
            Assert.Equal(1, second.ProcessCalls, "process calls on the selected plugin");
            Assert.Equal(UnpackStatus.Unpacked, report.Outcomes[0].Status, "status");
        }

        public static void PickerListsEveryVariantOfAPlugin()
        {
            var plugin = new FakePlugin("SteamStub 3.x")
            {
                DeclaredVariants = new[] { "SteamStub 3.0", "SteamStub 3.1" },
            };

            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, new LoggingService());

            Assert.Equal(3, session.Selections.Count, "picker entries");
            Assert.Equal("Automatic", session.Selections[0].Name, "first entry");
            Assert.Equal("SteamStub 3.0", session.Selections[1].Name, "first variant");
            Assert.Equal("SteamStub 3.1", session.Selections[2].Name, "second variant");
            Assert.Equal(plugin, session.Selections[1].Plugin, "variant maps to its plugin");
        }

        public static void ExplicitVariantRejectsAnotherVariant()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("SteamStub 3.x")
            {
                DeclaredVariants = new[] { "SteamStub 3.0", "SteamStub 3.1" },
                DetectedVariant = "SteamStub 3.1",
            };

            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, new LoggingService());
            var report = session.Run(new[] { file }, session.Selections[1], new SteamlessOptions(), CancellationToken.None);

            Assert.Equal(UnpackStatus.Skipped, report.Outcomes[0].Status, "status");
            Assert.Equal(0, plugin.ProcessCalls, "process calls when the file is another variant");
        }

        public static void ExplicitVariantRunsTheMatchingFile()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("SteamStub 3.x")
            {
                DeclaredVariants = new[] { "SteamStub 3.0", "SteamStub 3.1" },
                DetectedVariant = "SteamStub 3.1",
            };

            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, new LoggingService());
            var report = session.Run(new[] { file }, session.Selections[2], new SteamlessOptions(), CancellationToken.None);

            Assert.Equal(1, plugin.ProcessCalls, "process calls when the file matches");
            Assert.Equal(UnpackStatus.Unpacked, report.Outcomes[0].Status, "status");
        }

        public static void CancellationStopsTheRun()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("Claimer");

            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, new LoggingService());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(
                () => session.Run(new[] { file }, session.Selections[0], new SteamlessOptions(), cts.Token),
                "a cancelled run");

            Assert.Equal(0, plugin.ClaimCalls, "claims after cancellation");
        }

        public static void DisposeDisposesEveryPlugin()
        {
            var first = new FakePlugin("First");
            var second = new FakePlugin("Second");

            var session = new UnpackSession(new SteamlessPlugin[] { first, second }, new LoggingService());
            session.Dispose();

            Assert.Equal(1, first.DisposeCalls, "dispose calls on the first plugin");
            Assert.Equal(1, second.DisposeCalls, "dispose calls on the second plugin");
        }

        public static void RunAfterDisposeThrows()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));

            var session = new UnpackSession(new SteamlessPlugin[] { new FakePlugin("Claimer") }, new LoggingService());
            session.Dispose();

            Assert.Throws<ObjectDisposedException>(
                () => session.Run(new[] { file }, session.Selections[0], new SteamlessOptions(), CancellationToken.None),
                "a run after dispose");
        }

        public static void DetectListsTheClaimingPlugins()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var claimer = new FakePlugin("Claimer");
            var quiet = new FakePlugin("Quiet") { Claims = false };
            var thrower = new FakePlugin("Thrower") { ThrowsOnClaim = true };

            using var session = new UnpackSession(new SteamlessPlugin[] { claimer, quiet, thrower }, new LoggingService());
            var detection = session.Detect(file);

            Assert.True(detection.HasImage, "the file parses");
            Assert.Equal(1, detection.ClaimingPlugins.Count, "claimant count");
            Assert.Equal("Claimer", detection.ClaimingPlugins[0], "claimant name");
        }

        public static void DetectionUsesThePluginsVariantName()
        {
            using var dir = new TestDirectory();
            var file = dir.Write("game.exe", PeBuilder.Build(false, null, PeBuilder.Sections));
            var plugin = new FakePlugin("Claimer") { DetectedVariant = "SteamStub Variant 3.0 Unpacker" };

            using var session = new UnpackSession(new SteamlessPlugin[] { plugin }, new LoggingService());
            var detection = session.Detect(file);

            Assert.Equal(1, detection.ClaimingPlugins.Count, "claimant count");
            Assert.Equal("SteamStub Variant 3.0 Unpacker", detection.ClaimingPlugins[0], "claimant label");
        }
    }
}
