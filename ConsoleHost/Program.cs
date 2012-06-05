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
            var server = new SimpleHttpServer.Server("*", 8080);

            var config = new Configuration() { OwnUrl=@"http://localhost:8080", UpstreamUrl=@"http://microdoof.net"};
            var Proxy = new HttpProxy.ProxyServer(config, new ConsoleLogger());

            Proxy.Attach(server);

            server.Start();

            Console.ReadLine();

            server.Stop();
        }
    }
}
