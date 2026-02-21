using System;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace ConversaCore.TopicTool.Services
{
    /// <summary>
    /// Detects and validates ConversaCore projects in the current solution.
    /// </summary>
    public class ConversaCoreProjectDetector
    {
        private readonly DTE _dte;

        public ConversaCoreProjectDetector(DTE dte)
        {
            _dte = dte ?? throw new ArgumentNullException(nameof(dte));
        }

        public ConversaCoreProjectInfo? DetectProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_dte.Solution == null || string.IsNullOrEmpty(_dte.Solution.FullName))
            {
                return null;
            }

            // Look through all projects in the solution
            foreach (Project project in _dte.Solution.Projects)
            {
                var info = AnalyzeProject(project);
                if (info != null && info.IsConversaCoreProject)
                {
                    return info;
                }
            }

            return null;
        }

        private ConversaCoreProjectInfo? AnalyzeProject(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                if (string.IsNullOrEmpty(project.FullName))
                    return null;

                var projectDir = Path.GetDirectoryName(project.FullName);
                if (string.IsNullOrEmpty(projectDir))
                    return null;

                // Check for lib\ConversaCore*.dll
                var libFolder = Path.Combine(projectDir, "lib");
                if (!Directory.Exists(libFolder))
                    return null;

                var conversaCoreDlls = Directory.GetFiles(libFolder, "ConversaCore*.dll");
                if (conversaCoreDlls.Length == 0)
                    return null;

                // This looks like a ConversaCore project!
                var info = new ConversaCoreProjectInfo
                {
                    IsConversaCoreProject = true,
                    ProjectName = project.Name,
                    ProjectDirectory = projectDir,
                    LibFolder = libFolder,
                    ConversaCoreDlls = conversaCoreDlls
                };

                // Detect common folders
                var topicsFolder = Path.Combine(projectDir, "Topics");
                if (Directory.Exists(topicsFolder))
                {
                    info.TopicsFolder = topicsFolder;
                }

                var configFolder = Path.Combine(projectDir, "Configuration");
                if (Directory.Exists(configFolder))
                {
                    info.ConfigurationFolder = configFolder;

                    // Look for registration files
                    var registrationFiles = Directory.GetFiles(configFolder, "*TopicRegistration.cs");
                    if (registrationFiles.Length > 0)
                    {
                        info.RegistrationFile = registrationFiles[0];
                    }
                }

                // Try to find the build output assembly
                var binDebug = Path.Combine(projectDir, "bin", "Debug");
                if (Directory.Exists(binDebug))
                {
                    var targetFrameworks = Directory.GetDirectories(binDebug);
                    if (targetFrameworks.Length > 0)
                    {
                        var assemblyPath = Path.Combine(targetFrameworks[0], $"{project.Name}.dll");
                        if (File.Exists(assemblyPath))
                        {
                            info.AssemblyPath = assemblyPath;
                        }
                    }
                }

                // Check for document collections config
                var docConfigPath = Path.Combine(projectDir, ".conversacore.documents.json");
                if (File.Exists(docConfigPath))
                {
                    info.DocumentCollectionsConfigPath = docConfigPath;
                }

                return info;
            }
            catch
            {
                return null;
            }
        }

        public static ConversaCoreProjectDetector? GetInstance()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            return dte != null ? new ConversaCoreProjectDetector(dte) : null;
        }
    }

    public class ConversaCoreProjectInfo
    {
        public bool IsConversaCoreProject { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectDirectory { get; set; } = string.Empty;
        public string LibFolder { get; set; } = string.Empty;
        public string[] ConversaCoreDlls { get; set; } = Array.Empty<string>();
        public string? TopicsFolder { get; set; }
        public string? ConfigurationFolder { get; set; }
        public string? RegistrationFile { get; set; }
        public string? AssemblyPath { get; set; }
        public string? DocumentCollectionsConfigPath { get; set; }
    }
}
