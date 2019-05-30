using System;
using Microsoft.VisualStudio.Shell;

namespace ViewCodeInBrowser
{
    internal sealed class OpenInBrowser : BaseCommand
    {
        protected override int CommandId => 0x0100;
        protected override Guid CommandSet => new Guid("b22ddadb-6c6f-4428-9fc3-ccd57538c899");

        protected override int MaxSelectedItems => 10;
        
        private OpenInBrowser(AsyncPackage package) : base(package)
        {
        }
        
        #region Static

        public static async System.Threading.Tasks.Task CreateAndInitializeAsync(AsyncPackage package)
        {
            var command = new OpenInBrowser(package);
            await command.InitializeAsync();
        }

        #endregion

        protected override void CodeUrlAction(string codeUrl)
        {
            System.Threading.Tasks.Task.Run(() => System.Diagnostics.Process.Start(codeUrl));
        }
    }
}
