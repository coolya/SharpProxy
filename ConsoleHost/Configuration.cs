using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleHost
{
    class Configuration : HttpProxy.IConfiguration
    {
        public string ListeningRoot
        {
            get;
            set;
        }

        public int ListeningPort
        {
            get;
            set;
        }




        public IList<HttpProxy.IUriHandlerConfiguration> UriHandlers
        {
            get { throw new NotImplementedException(); }
        }


        public string ListeningProtocol
        {
            get { return "http"; }
        }
    }
}
