using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Blockchain.Utils
{
    public static class Utils
    {
        public static string GetLocalIpAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork &&
                        !ip.ToString().StartsWith("169.254.") && // Ignore APIPA
                        !ip.ToString().Equals("127.0.0.1"))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch
            {
            }
            return "127.0.0.1";
        }
        public static void AddLog(string msg)
        {
            Console.WriteLine($"[{DateTime.Now}] " + msg);
        }
    }
}
