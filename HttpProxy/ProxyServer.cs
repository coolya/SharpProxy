using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleHttpServer;
using System.Net;
using System.Reflection;

namespace HttpProxy
{
    public class ProxyServer
    {
        private ILogger _log;

        private Server _server;

        private IConfiguration _config;

        private static List<string> _excludedRequestHeaders = new List<string>() { "Connection", "Accept", "Host", "User-Agent", "Referer", "Accept-Encoding" };
        private static List<string> _excludedResponseHeaders = new List<string>() { "Content-Length", "Server", "Host", "Date" };

        public ProxyServer(ILogger log)
        {
            _log = log;
            AppDomain.CurrentDomain.TypeResolve += (sender, e) =>
            {
                var config = _config.UriHandlers.FirstOrDefault((item) => item.Type == e.Name);

                if (config != null)
                    return Assembly.Load(config.Assembly);

                return null;
            };
        }


        public void Start(IConfiguration config)
        {
            _config = config;
            Action<HttpListenerRequest, ServerResponse> act = HandleRequest;

            _server = new Server(config.ListeningRoot, config.ListeningPort);
            _server.Get("", act);
            _server.Delete("", act);
            _server.Post("", act);
            _server.Put("", act);
            _server.Start();
            _log.Info("Server up and running on {0} port {1}", config.ListeningRoot, config.ListeningPort);
        }

        public void HandleRequest(System.Net.HttpListenerRequest req, ServerResponse res)
        {
            try
            {
                _log.Verbose("Handling request for {0} from {1}", req.Url, req.RemoteEndPoint.Address);
                TransformAndRun(req, res, (toTransform) => new Uri("http://localhost:4964/" + toTransform.PathAndQuery),
                                      (requestResponse, serverResponse) =>
                                      {
                                          try
                                          {
                                              serverResponse.ContentLength64 = requestResponse.ContentLength;                                              

                                              foreach (string item in requestResponse.Headers)
                                              {
                                                  try
                                                  {
                                                      if (!_excludedResponseHeaders.Contains(item))
                                                          serverResponse.Headers.Add(item, requestResponse.Headers[item]);
                                                  }
                                                  catch (Exception)
                                                  {
                                                      _log.Warning("Failed to copy Header {0} with Value {1}", item, requestResponse.Headers[item]);
                                                  }

                                              }

                                              serverResponse.RedirectLocation = "http://localhost:80";

                                              if(requestResponse.ContentType.StartsWith("text/html", StringComparison.InvariantCultureIgnoreCase))
                                              {
                                                  var rewriter = new AsyncUrlRewriter(requestResponse.GetResponseStream(), serverResponse.OutputStream, 
                                                        Encoding.UTF8, "localhost:12214", "localhost");

                                                  int bytesToWrite = 0;
                                                  rewriter.BytesWriting += (sender, e) =>
                                                  {
                                                      bytesToWrite += e.Data;                                                     
                                                  };

                                                  rewriter.WriteError += (sender, e) =>
                                                      {
                                                          e.Data.Cancel = true;
                                                          _log.Error(e.Data.Exception);
                                                      };

                                                  rewriter.Canceled += (sender, e) =>
                                                      {
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
                                                          if (bytesToWrite < serverResponse.ContentLength64)
                                                          {
                                                              int bytesToFill = (int)(serverResponse.ContentLength64 - bytesToWrite);
                                                              var bytez = new byte[bytesToFill];

                                                              serverResponse.OutputStream.Write(bytez, 0, bytesToFill);
                                                          }

                                                          serverResponse.Close();
                                                          requestResponse.Close();
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

            proxyRequest.Accept = clientRequest.Headers["Accept"];
            proxyRequest.UserAgent = clientRequest.Headers["User-Agent"];
            proxyRequest.Referer = clientRequest.Headers["Referer"];

            proxyRequest.CookieContainer = new CookieContainer();

            foreach (Cookie cookie in clientRequest.Cookies)
            {
                try
                {
                    proxyRequest.CookieContainer.Add(cookie);
                }
                catch (Exception)
                {
                    _log.Warning("Falied to proxy cookie {0}", cookie);
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

            proxyRequest.Host = "localhost";

            // http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html#sec14.45
            string currentServerName = _config.ListeningRoot;
            string currentServerPort = _config.ListeningPort.ToString();
            string currentServerProtocol = _config.ListeningProtocol;

            if (currentServerProtocol.IndexOf("/") >= 0)
                currentServerProtocol = currentServerProtocol.Substring(currentServerProtocol.IndexOf("/") + 1);

            string currentVia = String.Format("{0} {1}:{2} ({3})", "1.1", currentServerName, currentServerPort, "SharpProxy");

            proxyRequest.Headers.Add("Via", currentVia);


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

