using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace lab4
{
    static class Program
    {
        // may be obsolete
        //static private LinkedList<BridgeConnection> connections = new LinkedList<BridgeConnection>();

        private static Socket listener;

        static void Main(string[] args)
        {
            listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            StartServer();
            Console.ReadKey();
        }

        static void StartServer()
        {
            string[] forbiddenHosts;
            try
            {
                forbiddenHosts = File.ReadAllLines("forbidden_hosts.cfg");
            }
            catch
            {
                Console.WriteLine("An error occured when tried to open a config file\n");
                return;
            }

            BridgeConnection.forbiddenHosts = forbiddenHosts;

            BridgeConnection.fobiddenPage = "<h1>Access to the requested domain\t{DOMAIN}" +
                "\thas been forbidden by the proxy server configuration</h1>";

            listener.Bind(new IPEndPoint(IPAddress.Parse("192.168.8.100"), 8000));
            listener.Listen(5);
            while (true)
            {
                // I actually can use async accept to make server-commands possible, but not now
                var newConnection = new BridgeConnection(listener.Accept());
                newConnection.StartServing();
            }
        }
    }
}
