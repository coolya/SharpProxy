using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HttpProxy
{
    class AsyncUrlRewriter 
    {
        private byte[] _lastBuffer;
        private Encoding _encoding;
        private string orgUrl;
        private string newUrl;

        private readonly Stream _input;
        private readonly Stream _output;

        private byte[] buffer = new byte[4096];

        public event EventHandler Completed;
        public event EventHandler<DataEventArgs<int>> BytesWriting;
        public event EventHandler<DataEventArgs<ExceptionEventContainer>> WriteError;
        public event EventHandler Canceled;

        private static object _lock = new object();

        public AsyncUrlRewriter(Stream input, Stream output, Encoding enc, string url, string newUrl)
        {
            _input = input;
            _output = output;

            this._encoding = enc;
            this.orgUrl = url;
            this.newUrl = newUrl;
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


            int index = 0;
            int lastindex = 0;
            string str = _encoding.GetString(buffer, 0, bytes);

            StringBuilder rewritten = new StringBuilder();

            while ((index = str.IndexOf("href", index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                int firstQuote = str.IndexOf('"', index);
                int secondQuote = str.IndexOf('"', firstQuote + 1);

                rewritten.Append(str.Substring(lastindex, firstQuote + 1 - lastindex));

                string target = str.Substring(firstQuote + 1, secondQuote - firstQuote - 1);

                rewritten.Append(target.Replace(orgUrl, newUrl));

                lastindex = secondQuote;

                index = secondQuote;
            }

            rewritten.Append(str.Substring(lastindex));

            str = rewritten.ToString();

            rewritten = new StringBuilder();

            index = 0;
            lastindex = 0;

            while ((index = str.IndexOf("src", index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                int firstQuote = str.IndexOf('"', index);
                int secondQuote = str.IndexOf('"', firstQuote + 1);

                rewritten.Append(str.Substring(lastindex, firstQuote + 1 - lastindex));

                string target = str.Substring(firstQuote + 1, secondQuote - firstQuote - 1);

                rewritten.Append(target.Replace(orgUrl, newUrl));

                lastindex = secondQuote;

                index = secondQuote;
            }

            rewritten.Append(str.Substring(lastindex));

            var byteData = _encoding.GetBytes(rewritten.ToString());

            RaiseBytesWriting(byteData.Length);

            bool cancel = false;

            try
            {
                _output.Write(byteData, 0, byteData.Length);
            }
            catch (Exception ex)
            {
                //let someone else deside if its clever to continue
                cancel = RaiseWriteError(ex);
            }

            if (!cancel)
                GetData();
            else
                RaiseCanceled();
        }

        private void RaiseComplete()
        {
            var handler = Completed;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void RaiseBytesWriting(int bytes)
        {
            var handler = BytesWriting;

            if (handler != null)
                handler(this, new DataEventArgs<int>(bytes));
        }

        private bool RaiseWriteError(Exception ex)
        {
            var handler = WriteError;

            if (handler != null)
            {
                var e = new ExceptionEventContainer(ex);
                handler(this, new DataEventArgs<ExceptionEventContainer>(e));
                return e.Cancel;
            }

            return true;
        }

        private void RaiseCanceled()
        {
            var handler = Canceled;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }


    }
}
