using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

using Microsoft.VisualStudio.TeamFoundation.VersionControl;

namespace ViewCodeInBrowser
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class OpenInBrowser
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("b22ddadb-6c6f-4428-9fc3-ccd57538c899");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package m_Package;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenInBrowser"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        private OpenInBrowser(Package package)
        {
            m_Package = package ?? throw new ArgumentNullException(nameof(package));

            if (!(ServiceProvider.GetService(typeof(IMenuCommandService)) is OleMenuCommandService commandService))
                return;

            var menuCommandId = new CommandID(CommandSet, CommandId);
            //var menuItem = new MenuCommand(MenuItemCallback, menuCommandId);
            var menuItem = new OleMenuCommand(MenuItemCallback, menuCommandId)
            {
                Supported = true
            };
            menuItem.BeforeQueryStatus += MenuItemOnBeforeQueryStatus;
            commandService.AddCommand(menuItem);
        }
        
        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static OpenInBrowser Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider => m_Package;

        private EnvDTE80.DTE2 m_Dte;

        private EnvDTE80.DTE2 Dte => m_Dte ?? (m_Dte = (EnvDTE80.DTE2) ServiceProvider.GetService(typeof(DTE)));

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static void Initialize(Package package)
        {
            Instance = new OpenInBrowser(package);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            
            // D:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation


            Dte.ExecuteCommand("View.TfsSourceControlExplorer");
            var vce = Dte.GetObject("Microsoft.VisualStudio.TeamFoundation.VersionControl.VersionControlExt") as VersionControlExt;
            var vcee = vce.Explorer;



            var tfsServerName = vcee.Workspace.VersionControlServer.TeamProjectCollection.Uri;
            var localPath = vcee.Workspace.Folders[0].LocalItem;
            var serverPath = vcee.Workspace.Folders[0].ServerItem;
            var basePath = $"{tfsServerName}{serverPath.Substring(1)}";



            //https://voy-devtfs2.visualstudio.com/DefaultCollection/CommonLicencing/_versionControl?path=%24%2FCommonLicencing%2FVoyager.Shared.CommonLicencing%2FInvalidCustomerDatabaseCodeException.cs

            var selectedFileNames = GetSelectedFileNames();

            foreach (var fileName in selectedFileNames)
            {
                var relativePath = fileName.Replace(localPath, "");

                var pathParameter = Uri.EscapeDataString($"{serverPath}{relativePath.Replace("\\", "/")}");

                var codeUrl = $"{basePath}/_versionControl?path={pathParameter}";

                var message = $"fileName: {fileName}\r\n" +
                          $"relativePath: {relativePath}\r\n" +
                          $"basePath: {basePath}\r\n" +
                          $"localPath: {localPath}\r\n" +
                          $"serverPath: {serverPath}\r\n" +
                          $"pathParameter: {pathParameter}\r\n" +
                          $"codeUrl: {codeUrl}";

                // Show a message box to prove we were here

                //string message = string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.GetType().FullName);
                //string title = "OpenInBrowser";

                //VsShellUtilities.ShowMessageBox(
                //    this.ServiceProvider,
                //    message,
                //    title,
                //    OLEMSGICON.OLEMSGICON_INFO,
                //    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                //    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

                System.Diagnostics.Process.Start(codeUrl);
            }
        }

        private void WriteToOutput(string output)
        {
            IVsOutputWindow outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;

            var generalPaneGuid = VSConstants.GUID_OutWindowGeneralPane;
            IVsOutputWindowPane generalPane;
            outWindow.GetPane(ref generalPaneGuid, out generalPane);

            generalPane.OutputString(output);
            //generalPane.Activate(); // Brings this pane into view
        }

        private List<string> GetSelectedFileNames()
        {
            var selectedItems = Dte.SelectedItems;

            if (selectedItems == null)
                return new List<string>(0);

            return selectedItems.OfType<SelectedItem>()
                                .Select(selectedItem =>
                                        {
                                            // File Node in Solution Explorer or Code Editor Context
                                            if (selectedItem.ProjectItem != null)
                                                return selectedItem.ProjectItem.FileNames[1];

                                            // Project Node in Solution Explorer
                                            if (selectedItem.Project != null)
                                                return selectedItem.Project.FileName;

                                            // Solution Node in Solution Explorer
                                            return Dte.Solution.FileName;
                                        })
                                .ToList();
        }


        private void MenuItemOnBeforeQueryStatus(object sender, EventArgs e)
        {
            if (!(sender is OleMenuCommand menuCommand))
                return;

            menuCommand.Visible = false;
            menuCommand.Enabled = false;

            var selectedItemFileNames = GetSelectedFileNames();

            // If too many items selected, don't show the menu
            if (selectedItemFileNames.Count > 10)
                return;

            // If any of the items selected are not under SC then don't open
            if (selectedItemFileNames.Any(f => !Dte.SourceControl.IsItemUnderSCC(f)))
                return;
            
            menuCommand.Visible = true;
            menuCommand.Enabled = true;
        }
    }
}
