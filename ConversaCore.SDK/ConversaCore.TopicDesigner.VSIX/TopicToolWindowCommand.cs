using System;
using System.ComponentModel.Design;
using System.IO;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace ConversaCore.TopicTool
{
    internal sealed class TopicToolWindowCommand
    {
        public const int MainCommandId = 0x0100;
        public const int ContextCommandId = 0x0101;
        public static readonly Guid CommandSet = new Guid("a0d5a85f-3f3a-4f62-9a3f-5a8e4e9b6b1f");

        private readonly AsyncPackage _package;

        private TopicToolWindowCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));

            var mainMenuId = new CommandID(CommandSet, MainCommandId);
            var mainMenuItem = new MenuCommand(this.Execute, mainMenuId);
            commandService.AddCommand(mainMenuItem);

            var contextMenuId = new CommandID(CommandSet, ContextCommandId);
            var contextMenuItem = new OleMenuCommand(this.Execute, contextMenuId);
            contextMenuItem.BeforeQueryStatus += OnBeforeQueryStatus;
            commandService.AddCommand(contextMenuItem);
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            _ = new TopicToolWindowCommand(package, commandService!);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var selectedTopicsFolder = TryGetSelectedTopicsFolder();

            var window = (TopicToolWindow)_package.FindToolWindow(typeof(TopicToolWindow), 0, true);
            if (window?.Frame is IVsWindowFrame windowFrame)
            {
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());

                if (!string.IsNullOrEmpty(selectedTopicsFolder) && window.Content is TopicManagementControl control)
                {
                    control.SetTopicsRoot(selectedTopicsFolder);
                }
            }
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (sender is OleMenuCommand command)
            {
                var selectedFolder = TryGetSelectedTopicsFolder();
                var isTopicsFolder = !string.IsNullOrEmpty(selectedFolder);

                command.Visible = isTopicsFolder;
                command.Enabled = isTopicsFolder;
            }
        }

        private string? TryGetSelectedTopicsFolder()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            if (dte?.SelectedItems == null || dte.SelectedItems.Count == 0)
            {
                return null;
            }

            try
            {
                var selectedItem = dte.SelectedItems.Item(1);
                var projectItem = selectedItem.ProjectItem;
                if (projectItem == null)
                {
                    return null;
                }

                var fullPathProperty = projectItem.Properties?.Item("FullPath");
                var fullPath = fullPathProperty?.Value as string;
                if (string.IsNullOrEmpty(fullPath))
                {
                    return null;
                }

                var path = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!Directory.Exists(path))
                {
                    return null;
                }

                var folderName = Path.GetFileName(path);
                if (!string.Equals(folderName, "Topics", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return path;
            }
            catch
            {
                return null;
            }
        }
    }
}
