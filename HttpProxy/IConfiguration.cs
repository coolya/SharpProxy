using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HttpProxy
{
    public interface IConfiguration
    {
        string ListeningRoot { get; }
        string ListeningProtocol { get; }
        int ListeningPort { get; }
        IList<IUriHandlerConfiguration> UriHandlers { get; }
    }
}
