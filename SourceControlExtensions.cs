using System;
using System.Diagnostics;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace ViewCodeInBrowser
{
    public static class SourceControlExtensions
    {
        public static bool IsSourceControlled(this SourceControl sourceControl, string itemName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                return sourceControl.IsItemUnderSCC(itemName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Exception checking if Source Controlled: {ex}");
                return false;
            }
        }
    }
}