using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HttpProxy
{
    public interface IUriHandlerConfiguration
    {
        string Type { get; }
        string Assembly { get; }
    }
}
