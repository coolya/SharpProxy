using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleHost
{
    class Program
    {
        static void Main(string[] args)
        {
            var Proxy = new HttpProxy.ProxyServer(new ConsoleLogger());

            var config = new Configuration() { ListeningPort = 80, ListeningRoot = "localhost" };

            Proxy.Start(config);

            Console.ReadLine();
        }
    }
}
