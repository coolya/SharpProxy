using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace HttpProxy
{
    class AsyncStreamCopier
    {
        private readonly Stream _input;
        private readonly Stream _output;

        private byte[] buffer = new byte[4096];

        public event EventHandler Completed;
        public event EventHandler<DataEventArgs<byte[]>> Copied;

        public AsyncStreamCopier(Stream input, Stream output)
        {
            _input = input;
            _output = output;
        }

        public void Copy()
        {
            GetData();
        }

        private void GetData()
        {
            _input.BeginRead(buffer, 0, buffer.Length, ReadComplete, null);
        }

        private void ReadComplete(IAsyncResult result)
        {

            int bytes = _input.EndRead(result);

            if (bytes == 0)
            {
                RaiseComplete();
                return;
            }

            _output.Write(buffer, 0, bytes);

            RaiseCopied(buffer);

            GetData();
        }

        private void RaiseCopied(byte[] buffer)
        {
            var handler = Copied;

            if (handler != null)
                handler(this,new DataEventArgs<byte[]>(buffer));
        }

        private void RaiseComplete()
        {
            var handler = Completed;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
