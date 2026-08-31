using System;
using System.Windows.Forms;
using ITHelpdeskToolkit.UI;

namespace ITHelpdeskToolkit
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
