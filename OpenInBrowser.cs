using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TeamFoundation.VersionControl;

namespace ViewCodeInBrowser
{
    internal sealed class OpenInBrowser
    {
        public const int COMMAND_ID = 0x0100;
        public static readonly Guid CommandSet = new Guid("b22ddadb-6c6f-4428-9fc3-ccd57538c899");

        private const int _MAX_SELECTED_ITEMS = 10;

        private readonly Package m_Package;
        
        private OpenInBrowser(Package package)
        {
            m_Package = package ?? throw new ArgumentNullException(nameof(package));

            if (!(ServiceProvider.GetService(typeof(IMenuCommandService)) is OleMenuCommandService commandService))
                return;

            var menuCommandId = new CommandID(CommandSet, COMMAND_ID);
            var menuItem = new OleMenuCommand(MenuItemCallback, menuCommandId)
            {
                Supported = true
            };
            menuItem.BeforeQueryStatus += MenuItemOnBeforeQueryStatus;
            commandService.AddCommand(menuItem);
        }
        
        public static OpenInBrowser Instance
        {
            get;
            private set;
        }
        
        private IServiceProvider ServiceProvider => m_Package;

        private EnvDTE80.DTE2 m_Dte;

        private EnvDTE80.DTE2 Dte => m_Dte ?? (m_Dte = (EnvDTE80.DTE2) ServiceProvider.GetService(typeof(DTE)));
        
        public static void Initialize(Package package)
        {
            Instance = new OpenInBrowser(package);
        }

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
            Dte.ExecuteCommand("View.TfsSourceControlExplorer");
            var vce = (VersionControlExt)Dte.GetObject("Microsoft.VisualStudio.TeamFoundation.VersionControl.VersionControlExt");
            var vceExp = vce.Explorer;
            
            var tfsServerName = vceExp.Workspace.VersionControlServer.TeamProjectCollection.Uri;
            var localPath = vceExp.Workspace.Folders[0].LocalItem;
            var serverPath = vceExp.Workspace.Folders[0].ServerItem;
            var basePath = $"{tfsServerName}{serverPath.Substring(1)}";
            
            var selectedFileNames = GetSelectedFileNames();

            foreach (var fileName in selectedFileNames)
            {
                var relativePath = fileName.Replace(localPath, "").Replace("\\", "/");

                var pathParameter = Uri.EscapeDataString($"{serverPath}{relativePath}");

                var codeUrl = $"{basePath}/_versionControl?path={pathParameter}";
                
                System.Diagnostics.Process.Start(codeUrl);
            }
        }

        private void MenuItemOnBeforeQueryStatus(object sender, EventArgs e)
        {
            if (!(sender is OleMenuCommand menuCommand))
                return;

            menuCommand.Visible = false;
            menuCommand.Enabled = false;

            var selectedItemFileNames = GetSelectedFileNames();

            // If too many items selected, don't show the menu item
            if (selectedItemFileNames.Count > _MAX_SELECTED_ITEMS)
                return;

            // If any of the items selected are not under SC then don't show the menu item
            if (selectedItemFileNames.Any(f => !Dte.SourceControl.IsItemUnderSCC(f)))
                return;
            
            menuCommand.Visible = true;
            menuCommand.Enabled = true;
        }

        #endregion

        #region Private Methods

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

        #endregion
    }
}
