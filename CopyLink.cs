using System;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;

namespace ViewCodeInBrowser
{
    internal sealed class CopyLink : BaseCommand
    {
        protected override int CommandId => 0x0101;
        protected override Guid CommandSet => new Guid("b22ddadb-6c6f-4428-9fc3-ccd57538c899");

        protected override int MaxSelectedItems => 1;

        private CopyLink(AsyncPackage package) : base(package)
        {
        }

        #region Static

        public static async System.Threading.Tasks.Task CreateAndInitializeAsync(AsyncPackage package)
        {
            var command = new CopyLink(package);
            await command.InitializeAsync();
        }

        #endregion

        protected override void CodeUrlAction(string codeUrl)
        {
            Clipboard.SetText(codeUrl);
        }
    }
}
