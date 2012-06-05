using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HttpProxy
{
    class AsyncUrlRewriter 
    {
        string _lastBuffer;
        Encoding _encoding;
        string orgUrl;
        string newUrl;

        readonly Stream _input;
        readonly Stream _output;

        byte[] buffer = new byte[4096];

        public event EventHandler Completed;
        public event EventHandler<DataEventArgs<ExceptionEventContainer>> OnException;

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

        void GetData()
        {
            _input.BeginRead(buffer, 0, buffer.Length, ReadComplete, null);
        }

        void ReadComplete(IAsyncResult result)
        {
            int bytes = _input.EndRead(result);

            if (bytes == 0)
            {
                RaiseComplete();
                return;
            }

            string str = _encoding.GetString(buffer, 0, bytes);

            if (!string.IsNullOrEmpty(_lastBuffer))
            {
                str = _lastBuffer + str;
                _lastBuffer = null;
            }

            str = RewriteUrl("href", str);
            str = RewriteUrl("src", str);

            var byteData = _encoding.GetBytes(str);

            bool cancel = false;

            try
            {
                _output.Write(byteData, 0, byteData.Length);
            }
            catch (Exception ex)
            {
                //let someone else deside if its clever to continue
                cancel = RaiseException(ex);
            }

            if (!cancel)
                GetData();
        }

        string RewriteUrl(string toRewrite, string str)
        {
            int index = 0;
            int lastindex = 0;

            StringBuilder rewritten = new StringBuilder();

            index = str.IndexOf(toRewrite, index, StringComparison.OrdinalIgnoreCase);

            while (index != -1)
            {
                int firstQuote = index + toRewrite.Length + 1;
                char quote = '0';

                if (firstQuote < str.Length)
                    quote = str[firstQuote];
                else
                    firstQuote = -1;

                if (firstQuote == -1)
                {// we have found something to replace but it is longer than the buffer
                    _lastBuffer = str.Substring(index - toRewrite.Length);
                    index = -1;
                }
                else
                {
                    int secondQuote = str.IndexOf(quote, firstQuote + 1);

                    rewritten.Append(str.Substring(lastindex, firstQuote + 1 - lastindex));

                    string target;

                    if (secondQuote != -1)
                    {
                        target = str.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                        rewritten.Append(target.Replace(orgUrl, newUrl));
                        lastindex = secondQuote;
                        index = str.IndexOf(toRewrite, secondQuote, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {//we have found something but it is longer than the buffer
                        _lastBuffer = str.Substring(index);
                        index = -1;
                    }
                }
            }
            rewritten.Append(str.Substring(lastindex));
            return rewritten.ToString();
        }

        void RaiseComplete()
        {
            var handler = Completed;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }        

        bool RaiseException(Exception ex)
        {
            var handler = OnException;

            if (handler != null)
            {
                var e = new ExceptionEventContainer(ex);
                handler(this, new DataEventArgs<ExceptionEventContainer>(e));
                return e.Cancel;
            }

            return true;
        }
    }
}
