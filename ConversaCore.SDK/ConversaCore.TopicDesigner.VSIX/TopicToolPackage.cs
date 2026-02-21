using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace ConversaCore.TopicTool
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("ConversaCore Topic Designer", "Topic designer tooling for ConversaCore-based agents.", "0.1")]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(TopicToolWindow))]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class TopicToolPackage : AsyncPackage
    {
        public const string PackageGuidString = "b6b6b7e4-3c8e-4c2f-9f1a-4e6b2524f4c2";

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await TopicToolWindowCommand.InitializeAsync(this);
        }
    }
}
