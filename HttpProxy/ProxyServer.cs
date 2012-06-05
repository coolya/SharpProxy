using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleHttpServer;
using System.Net;
using System.Reflection;
using System.IO;

namespace HttpProxy
{
    public class ProxyServer
    {
        ILogger _log;
        Server _server;
        IConfiguration _config;

        static List<string> _excludedRequestHeaders = new List<string>() { "Connection", "Accept", "Host", "User-Agent", "Referer", "Accept-Encoding" };
        static List<string> _excludedResponseHeaders = new List<string>() { "Content-Length", "Server", "Host", "Date" };

        public ProxyServer(IConfiguration config, ILogger log)
        {
            _log = log;
            _config = config;
        }

        public void Attach( Server server)
        {            
            Action<HttpListenerRequest, ServerResponse> act = HandleRequest;

            _server = server;
            _server.Get(_config.OwnUrl, act);
            _server.Delete(_config.OwnUrl, act);
            _server.Post(_config.OwnUrl, act);
            _server.Put(_config.OwnUrl, act);
            _log.Info("Server configured on {0} for upstream {1}", _config.OwnUrl, _config.UpstreamUrl);
        }

        public void HandleRequest(System.Net.HttpListenerRequest req, ServerResponse res)
        {
            try
            {
                _log.Verbose("Handling request for {0} from {1}", req.Url, req.RemoteEndPoint.Address);
                TransformAndRun(req, res, (toTransform) => new Uri(_config.UpstreamUrl + toTransform.PathAndQuery),
                                      (requestResponse, serverResponse) =>
                                      {
                                          try
                                          {
                                              if (requestResponse.ContentLength > -1)
                                                  serverResponse.ContentLength64 = requestResponse.ContentLength;

                                              foreach (string item in requestResponse.Headers)
                                              {
                                                  try
                                                  {
                                                      if (!_excludedResponseHeaders.Contains(item))
                                                          serverResponse.Headers.Add(item, requestResponse.Headers[item]);
                                                  }
                                                  catch (Exception ex)
                                                  {
                                                      _log.Warning("Failed to copy Header {0} with Value {1} \r\n {2}", item, requestResponse.Headers[item], ex.Message);
                                                  }
                                              }

                                              if(requestResponse.ContentType.StartsWith("text/html", StringComparison.InvariantCultureIgnoreCase)
                                                  || requestResponse.ContentType.StartsWith("text/javascript", StringComparison.InvariantCultureIgnoreCase)
                                                  || requestResponse.ContentType.StartsWith("text/css", StringComparison.InvariantCultureIgnoreCase))
                                              {
                                                  var buffer = new MemoryStream();
                                                  var rewriter = new AsyncUrlRewriter(requestResponse.GetResponseStream(), buffer,
                                                        Encoding.UTF8, _config.UpstreamUrl, _config.OwnUrl);

                                                   rewriter.OnException += (sender, e) =>
                                                      {
                                                          e.Data.Cancel = true;
                                                          _log.Error(e.Data.Exception);

                                                          try
                                                          {
                                                              serverResponse.Close();
                                                              requestResponse.Close();
                                                          }
                                                          catch (Exception ex)
                                                          {
                                                              _log.Error(ex);
                                                          }
                                                      };                                               

                                                  rewriter.Completed += (sender, e) =>
                                                      {
                                                          serverResponse.ContentLength64 = buffer.Length;
                                                          buffer.Position = 0;

                                                          var copier = new AsyncStreamCopier(buffer, serverResponse.OutputStream);
                                                          copier.Completed += (s, a) =>
                                                              {
                                                                  buffer.Close();
                                                                  serverResponse.Close();
                                                                  requestResponse.Close();
                                                              };
                                                          copier.Copy();
                                                      };
                                                  rewriter.Copy();
                                              }
                                              else
                                              {
                                                  var copier = new AsyncStreamCopier(requestResponse.GetResponseStream(), serverResponse.OutputStream);

                                                  copier.Completed += (sender, e) =>
                                                      {
                                                          serverResponse.Close();
                                                          requestResponse.Close();
                                                      };

                                                  copier.Copy();
                                              }
                                          }
                                          catch (Exception ex)
                                          {
                                              _log.Error(ex);
                                              serverResponse.Do503(ex).Close();
                                              requestResponse.Close();
                                          }
                                      });
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                res.Do503(ex).Close(); ;
            }
        }

        private void TransformAndRun(System.Net.HttpListenerRequest clientRequest, ServerResponse serverResponse, Func<Uri, Uri> transformUri, Action<WebResponse, ServerResponse> OnComplete)
        {
            var proxyRequest = (HttpWebRequest)HttpWebRequest.Create(transformUri(clientRequest.Url));

            proxyRequest.SendChunked = false;
            proxyRequest.Accept = clientRequest.Headers["Accept"];
            proxyRequest.UserAgent = clientRequest.Headers["User-Agent"];
            proxyRequest.Referer = clientRequest.Headers["Referer"];

            proxyRequest.CookieContainer = new CookieContainer();

            foreach (Cookie cookie in clientRequest.Cookies)
            {
                try
                {
                    cookie.Domain = proxyRequest.RequestUri.Host;
                    proxyRequest.CookieContainer.Add(cookie);
                }
                catch (Exception ex)
                {
                    _log.Warning("Falied to proxy cookie {0} \r\n {1}", cookie, ex.Message);
                }
            }

            foreach (string item in clientRequest.Headers)
            {
                try
                {
                    if (!_excludedRequestHeaders.Contains(item))
                        proxyRequest.Headers.Add(item, clientRequest.Headers[item]);
                }
                catch (Exception)
                {
                    _log.Warning("Failed to copy Header {0} with Value {1}", item, clientRequest.Headers[item]);
                }
            }

            proxyRequest.Method = clientRequest.HttpMethod;
            proxyRequest.ContentLength = clientRequest.ContentLength64;
            proxyRequest.ContentType = clientRequest.ContentType;

            proxyRequest.Headers.Add("X-Forwarded-For", clientRequest.UserHostAddress);

            AsyncCallback callback = (requestResult) => 
                    {
                        var asyncState = requestResult.AsyncState as Tuple<ServerResponse, Action<WebResponse, ServerResponse>>;

                        var response = asyncState.Item1;
                        var OnCompleteCallback = asyncState.Item2;

                        try
                        {
                            var requestResponse = proxyRequest.EndGetResponse(requestResult);
                            OnCompleteCallback(requestResponse, response);
                        }
                        catch (WebException ex)
                        {                          

                            var httpresponse = ex.Response as HttpWebResponse;
                            if (httpresponse != null)
                            {
                                _log.Info("Server returned Status {0} for URL {1}", httpresponse.StatusCode, httpresponse.ResponseUri);

                                response.StatusCode = (int)httpresponse.StatusCode;
                                response.StatusDescription = httpresponse.StatusDescription;

                                var copier = new AsyncStreamCopier(httpresponse.GetResponseStream(), response.OutputStream);

                                copier.Completed += (sender, e) =>
                                {
                                    response.Close();
                                    httpresponse.Close();
                                };

                                copier.Copy();
                            }
                            else
                            {
                                _log.Error(ex);
                                ex.Response.Close();
                                response.Do503(ex).Close();
                            }

                        }
                        catch (Exception ex)
                        {
                            _log.Error(ex);
                            response.Do503(ex).Close();
                        }

                    };

            var state = Tuple.Create(serverResponse, OnComplete);

            if (proxyRequest.Method != "GET")
            {
                var copier = new AsyncStreamCopier(clientRequest.InputStream, proxyRequest.GetRequestStream());

                copier.Completed += (sender, a) => proxyRequest.BeginGetResponse(callback, state);

                copier.Copy();
            }
            else
            {
                proxyRequest.BeginGetResponse(callback, state);                    
            }
        }
    }
}

