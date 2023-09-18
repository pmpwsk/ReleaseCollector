using EnvDTE;
using Microsoft.VisualStudio.RemoteSettings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Task = System.Threading.Tasks.Task;

namespace uwap.VSIX.ReleaseCollector
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class PublishAndCollect
    {
        #region Stuff
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("104e176a-7136-4ac2-9e9b-462901c70baf");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishAndCollect"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private PublishAndCollect(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static PublishAndCollect Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in PublishAndCollect's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new PublishAndCollect(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async delegate { await ExecuteAsync(); });
        }
        #endregion

        private async Task ExecuteAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                IVsStatusbar statusbar = (IVsStatusbar)await ServiceProvider.GetServiceAsync(typeof(SVsStatusbar));
                DTE dte = (DTE)await ServiceProvider.GetServiceAsync(typeof(DTE));

                Array projects = (Array)dte.ActiveSolutionProjects;
                if (projects == null || projects.Length == 0)
                {
                    MessageBox("Error!", "Select a project first!");
                    await StatusbarTextAsync("Select a project first!", statusbar);
                }
                else if (projects.Length > 1)
                {
                    MessageBox("Error!", "Please only select one project at a time!");
                    await StatusbarTextAsync("Please only select one project at a time!", statusbar);
                }
                else
                {
                    Project project = (Project)projects.GetValue(0);
                    try
                    {
                        Options options = ((ReleaseCollectorPackage)this.package).Options;

                        await StatusbarTextAsync($"Publishing {project.Name}...", statusbar);
                        string projPath = new FileInfo(project.FullName).DirectoryName;

                        if (!File.Exists(project.FullName))
                            throw new Exception($"No .csproj found for the project (looked for: {project.FullName})!");

                        string csproj = File.ReadAllText(project.FullName);
                        if (csproj.Contains("\"Microsoft.NET.Sdk.Web\""))
                            await PublishDefaultAsync(projPath, project.Name, options, statusbar);
                        else if (csproj.Contains("\"Microsoft.NET.Sdk\""))
                        {
                            if (csproj.Contains("<OutputType>Exe</OutputType>"))
                                await PublishDefaultAsync(projPath, project.Name, options, statusbar);
                            else throw new NotImplementedException("Libraries are not supported!");
                        }
                        else throw new Exception("Unsupported project type.");

                        await Task.Delay(500);
                        await StatusbarTextAsync($"Successfully published {project.Name}!", statusbar);
                        MessageBox("Done!", $"Successfully published {project.Name}!");
                    }
                    catch (Exception ex)
                    {
                        MessageBox("Error!", $"An error occurred while publishing {project.Name}:\n{ex.Message}");
                        await StatusbarTextAsync($"An error occurred while publishing {project.Name}!", statusbar);
                    }
                }

                await Task.Delay(5000);
                statusbar.FreezeOutput(0);
                statusbar.Clear();
            }
            catch (Exception ex)
            {
                MessageBox("Error!", $"An error stopped the operation:\n{ex.Message}");
            }
        }

        private string ReleaseFolder(string projPath)
        {
            string currentFolder = null;
            ushort[] currentVersion = null;
            foreach (DirectoryInfo info in new DirectoryInfo(projPath + "/bin").GetDirectories("v*", SearchOption.TopDirectoryOnly))
            {
                string[] versionStrings = info.Name.Remove(0, 1).Split('.');
                ushort[] version = new ushort[versionStrings.Length];
                for (int i = 0; i < versionStrings.Length; i++)
                    if (!ushort.TryParse(versionStrings[i], out version[i]))
                        goto Skip;
                if (currentVersion == null || LaterThan(version, currentVersion))
                {
                    currentFolder = info.Name;
                    currentVersion = version;
                }
                Skip:;
            }
            return currentFolder ?? "latest";
        }

        private bool LaterThan(ushort[] a, ushort[] b)
        {
            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            {
                if (a[i] < b[i])
                    return false;
                if (a[i] > b[i])
                    return true;
            }

            return a.Length > b.Length;
        }

        private async Task PublishDefaultAsync(string projPath, string projName, Options options, IVsStatusbar statusbar)
        {
            string releaseFolder = ReleaseFolder(projPath);
            string releasePath = projPath + "/bin/" + releaseFolder;
            if (releaseFolder != "latest" && Directory.GetFileSystemEntries(releasePath).Any())
                throw new Exception("The folder with the highest version number isn't empty! You probably forgot to create a folder for the next version.");
            if (Directory.Exists(releasePath))
                Directory.Delete(releasePath, true);
            Directory.CreateDirectory(releasePath);
            string versionSuffix = releaseFolder == "latest" ? "" : ("-" + releaseFolder);

            foreach (string system in options.SystemsClean)
            {
                await StatusbarTextAsync($"Publishing: {projName}{versionSuffix}_{system}-fd", statusbar);
                await PublishAsync(projPath, projName, $"{projName}{versionSuffix}_{system}-fd", $"-r {system} -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true --self-contained false", releasePath, options);
                switch (options.SelfContainedMode)
                {
                    case Options.SCMode.Yes:
                        await StatusbarTextAsync($"Publishing: {projName}{versionSuffix}_{system}-sc", statusbar);
                        await PublishAsync(projPath, projName, $"{projName}{versionSuffix}_{system}-sc", $"-r {system} -p:PublishSingleFile=true --self-contained true", releasePath, options);
                        break;
                    case Options.SCMode.Trimmed_DANGEROUS:
                        await StatusbarTextAsync($"Publishing: {projName}{versionSuffix}_{system}-sc", statusbar);
                        await PublishAsync(projPath, projName, $"{projName}{versionSuffix}_{system}-sc", $"-r {system} -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true", releasePath, options);
                        break;
                }
            }
        }

        private async Task PublishAsync(string projPath, string projName, string name, string arguments, string releasePath, Options options)
        {
            string buildDir = $"{releasePath}/{name}-bin";

            if (Directory.Exists(buildDir))
                Directory.Delete(buildDir, true);

            try
            {
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "dotnet";
                process.StartInfo.WorkingDirectory = projPath;
                process.StartInfo.Arguments = $"publish {projName}.csproj -o {buildDir} --configuration Release {arguments}";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await WaitForExitAsync(process);

                if (output.Contains("error"))
                    throw new Exception("Error in dotnet output.");

                foreach (string searchPattern in options.IgnoredExtensionsClean)
                    foreach (string f in Directory.GetFiles(buildDir, searchPattern))
                        File.Delete(f);

                var files = Directory.GetFiles(buildDir, "*", SearchOption.AllDirectories);
                if (files.Length == 0)
                    throw new Exception("No files found.");
                if (files.Length > 1)
                    ZipFile.CreateFromDirectory(buildDir, $"{releasePath}/{name}.zip", options.ZipCompression, options.ZipSubfolder);
                else
                {
                    FileInfo file = new FileInfo(files[0]);
                    File.Move(file.FullName, $"{releasePath}/{name}{file.Extension}");
                }
            }
            catch (Exception ex)
            {
                MessageBox("Error!", $"An error occurred while trying to publish {name}:\n{ex.Message}\n{ex.StackTrace}");
            }

            if (Directory.Exists(buildDir))
                Directory.Delete(buildDir, true);
        }

        private Task WaitForExitAsync(System.Diagnostics.Process process)
        {
            if (process.HasExited)
                return Task.CompletedTask;
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(null);
            return process.HasExited ? Task.CompletedTask : tcs.Task;
        }

        private async Task StatusbarTextAsync(string text, IVsStatusbar statusbar)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            statusbar.IsFrozen(out int frozen);
            if (frozen != 0)
                statusbar.FreezeOutput(0);
            statusbar.SetText(text);
            statusbar.FreezeOutput(1);
        }

        private void MessageBox(string title, string text)
            => VsShellUtilities.ShowMessageBox(
                this.package,
                text,
                title,
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }
}
