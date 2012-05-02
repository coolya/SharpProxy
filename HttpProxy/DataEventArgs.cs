using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HttpProxy
{
    public class DataEventArgs<T> : EventArgs
    {
        T data;
        public DataEventArgs(T data)
        {
            this.data = data;
        }

        public T Data { get { return data; } }   
    }
}
