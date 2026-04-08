using System;
using System.Windows.Forms;

namespace SystemMonitorApp
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool isAdmin;
            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                isAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }

            if (!isAdmin)
            {
                MessageBox.Show("ACE tool hub deeply integrates with WMI and the Recycle Bin and requires Administrative privileges.\n\nPlease right-click the executable and select 'Run as administrator'.", "Elevation Required - ACE tool hub", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.Run(new MainForm());
        }
    }
}
