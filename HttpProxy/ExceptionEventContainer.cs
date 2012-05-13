using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HttpProxy
{
    class ExceptionEventContainer
    {
        public Exception Exception { get; private set; }

        public bool Cancel { get; set; }

        public ExceptionEventContainer(Exception ex)
        {
            Exception = ex;
        }
    }
}
