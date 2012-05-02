using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HttpProxy
{
    public interface IReadOnlyUriHandler
    {
        void Init(Dictionary<string, string> parameters);
        Func<Uri, bool> Handels { get; }
        Action<ReadOnlyHttpListenerRequest> Handle { get; }
    }
}
