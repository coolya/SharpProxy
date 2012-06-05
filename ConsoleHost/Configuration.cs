using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleHost
{
    class Configuration : HttpProxy.IConfiguration
    {
        public string OwnUrl
        {
            get;
            set;
        }

        public string UpstreamUrl
        {
            get;
            set;
        }
    }
}
