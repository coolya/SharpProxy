using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleHttpServer;

namespace HttpProxy
{
    public interface IUriHandler
    {
        void Init(Dictionary<string, string> parameters);
        Func<Uri, bool> Handles { get; }
        Action<System.Net.HttpListenerRequest, ServerResponse> Handle { get; }
    }
}
