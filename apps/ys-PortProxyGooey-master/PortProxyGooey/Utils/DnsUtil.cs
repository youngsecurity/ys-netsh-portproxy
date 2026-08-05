using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PortProxyGooey.Utils
{
    internal class DnsUtil
    {
        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        static extern uint DnsFlushResolverCache();

        public static void FlushCache()
        {
            uint status = DnsFlushResolverCache();
            if (status == 0)
            {
                throw new InvalidOperationException("Flush DNS Cache failed.");
            } else
            {
                MessageBox.Show("DNS Flushed!", "Whoosh", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
