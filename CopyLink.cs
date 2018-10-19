using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TeamFoundation.VersionControl;

namespace ViewCodeInBrowser
{
    internal sealed class CopyLink
    {
        public const int COMMAND_ID = 0x0101;
        public static readonly Guid CommandSet = new Guid("b22ddadb-6c6f-4428-9fc3-ccd57538c899");

        private const int _MAX_SELECTED_ITEMS = 1;

        private readonly IAsyncServiceProvider m_ServiceProvider;
        private EnvDTE80.DTE2 m_Dte;

        private CopyLink(AsyncPackage package)
        {
            m_ServiceProvider = package ?? throw new ArgumentNullException(nameof(package));
        }
        
        private async System.Threading.Tasks.Task InitializeAsync()
        {
            if (!(await m_ServiceProvider.GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commandService))
                return;

            // Would be better to lazy initialise this but need to fully understand the VS threading stuff first
            // https://docs.microsoft.com/en-us/visualstudio/extensibility/how-to-use-asyncpackage-to-load-vspackages-in-the-background?view=vs-2017#querying-services-from-asyncpackage
            m_Dte = (EnvDTE80.DTE2) await m_ServiceProvider.GetServiceAsync(typeof(DTE));

            var menuCommandId = new CommandID(CommandSet, COMMAND_ID);
            var menuItem = new OleMenuCommand(MenuItemCallback, menuCommandId);
            menuItem.BeforeQueryStatus += MenuItemOnBeforeQueryStatus;

            // AddCommand calls GetService (not GetServiceAsync) so switch to UI thread
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            commandService.AddCommand(menuItem);
        }

        #region Static

        public static async System.Threading.Tasks.Task CreateAndInitializeAsync(AsyncPackage package)
        {
            var command = new CopyLink(package);
            await command.InitializeAsync();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void MenuItemCallback(object sender, EventArgs e)
        {
            m_Dte.ExecuteCommand("View.TfsSourceControlExplorer");
            var vce = (VersionControlExt)m_Dte.GetObject("Microsoft.VisualStudio.TeamFoundation.VersionControl.VersionControlExt");
            var vceExp = vce.Explorer;
            
            var tfsServerName = vceExp.Workspace.VersionControlServer.TeamProjectCollection.Uri;
            var localPath = vceExp.Workspace.Folders[0].LocalItem;
            var serverPath = vceExp.Workspace.Folders[0].ServerItem;
            var serverProjectName = serverPath.Substring(1).TrimStart('/').Split('/').First(); // Path starts with $ and first folder is the VSTS "Project"
            var basePath = $"{tfsServerName}/{serverProjectName}";
            
            var selectedFileNames = GetSelectedFileNames();

            foreach (var fileName in selectedFileNames)
            {
                var relativePath = fileName.Replace(localPath, "").Replace("\\", "/");

                var pathParameter = Uri.EscapeDataString($"{serverPath}{relativePath}");

                var codeUrl = $"{basePath}/_versionControl?path={pathParameter}";
                
                Clipboard.SetText(codeUrl);
            }
        }

        private void MenuItemOnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread(nameof(GetSelectedFileNames));

            if (!(sender is OleMenuCommand menuCommand))
                return;

            menuCommand.Visible = false;
            menuCommand.Enabled = false;

            var selectedItemFileNames = GetSelectedFileNames();

            // If too many items selected, don't show the menu item
            if (selectedItemFileNames.Count > _MAX_SELECTED_ITEMS)
                return;

#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread - checked above and Any executes immediately on same thread

            // If any of the items selected are not under SC then don't show the menu item
            if (selectedItemFileNames.Any(f => !m_Dte.SourceControl.IsItemUnderSCC(f)))
                return;

#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

            menuCommand.Visible = true;
            menuCommand.Enabled = true;
        }

        #endregion

        #region Private Methods

        private List<string> GetSelectedFileNames()
        {
            ThreadHelper.ThrowIfNotOnUIThread(nameof(GetSelectedFileNames));

            var selectedItems = m_Dte.SelectedItems;

            if (selectedItems == null)
                return new List<string>(0);

            return selectedItems.OfType<SelectedItem>()
                .Select(selectedItem =>
                {
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread - checked above and ToList below executes immediately on same thread

                    // File Node in Solution Explorer or Code Editor Context
                    if (selectedItem.ProjectItem != null)
                        return selectedItem.ProjectItem.FileNames[1];

                    // Project Node in Solution Explorer
                    if (selectedItem.Project != null)
                        return selectedItem.Project.FileName;

                    // Solution Node in Solution Explorer
                    return m_Dte.Solution.FileName;

#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
                })
                .ToList();
        }

        #endregion
    }
}
