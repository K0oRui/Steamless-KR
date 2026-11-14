namespace Steamless.Tests
{
    using System;
    using System.Collections.Generic;
    using Steamless.API.Model;
    using Steamless.API.PE;

    /// <summary>
    /// A plugin whose claim, result and failure the check decides. It stands in for the plugins the
    /// loader would read from a folder, so the session's dispatch can run without a compiled plugin
    /// DLL or a real SteamStub stub.
    /// </summary>
    internal sealed class FakePlugin : SteamlessPlugin
    {
        private readonly string m_Name;

        public FakePlugin(string name)
        {
            this.m_Name = name;
        }

        public override string Name => this.m_Name;

        public override string Description => "A test plugin.";

        public bool Claims { get; set; } = true;

        public bool Unpacks { get; set; } = true;

        public bool ThrowsOnClaim { get; set; }

        public bool ThrowsOnProcess { get; set; }

        /// <summary>The label detection reports, or null to keep the plugin name.</summary>
        public string? DetectedVariant { get; set; }

        /// <summary>The layouts the picker lists, or null to keep the plugin name.</summary>
        public string[]? DeclaredVariants { get; set; }

        public override IReadOnlyList<string> Variants => this.DeclaredVariants ?? base.Variants;

        /// <summary>Runs while the plugin holds a file, so a check can cancel the run mid-flight.</summary>
        public Action? OnProcess { get; set; }

        /// <summary>Runs with the image before the plugin reports its result, so a check can change it.</summary>
        public Action<PeImage>? OnProcessFile { get; set; }

        /// <summary>Overrides the claim rule with a check of the file the plugin is given.</summary>
        public Func<PeImage, bool>? ClaimWhen { get; set; }

        public int ClaimCalls { get; private set; }

        public int ProcessCalls { get; private set; }

        public int DisposeCalls { get; private set; }

        public override bool CanProcessFile(PeImage file)
        {
            this.ClaimCalls++;

            if (this.ThrowsOnClaim)
                throw new InvalidOperationException($"{this.m_Name} cannot read this file.");

            return this.ClaimWhen?.Invoke(file) ?? this.Claims;
        }

        public override string DetectVariant(PeImage file)
        {
            return this.DetectedVariant ?? base.DetectVariant(file);
        }

        public override bool ProcessFile(PeImage file, SteamlessOptions options)
        {
            this.ProcessCalls++;

            this.OnProcess?.Invoke();
            this.OnProcessFile?.Invoke(file);

            if (this.ThrowsOnProcess)
                throw new InvalidOperationException($"{this.m_Name} failed on this file.");

            return this.Unpacks;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                this.DisposeCalls++;
        }
    }
}
