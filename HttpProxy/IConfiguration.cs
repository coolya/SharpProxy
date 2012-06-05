using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HttpProxy
{
    public interface IConfiguration
    {
        string OwnUrl { get;  }
        string UpstreamUrl { get; }
    }
}
