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

        private static List<string> _excludedRequestHeaders = new List<string>() { "Connection", "Accept", "Host", "User-Agent", "Referer" };
        private static List<string> _excludedResponseHeaders = new List<string>() { "Content-Length", "Server" };

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
                TransformAndRun(req, res, (toTransform) => new Uri("http://192.168.2.100/" + toTransform.PathAndQuery),
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
                                              

                                              var copier = new AsyncStreamCopier(requestResponse.GetResponseStream(), serverResponse.OutputStream);

                                              copier.Completed += (sender, e) =>
                                                  {
                                                      serverResponse.Close();
                                                      requestResponse.Close();
                                                  };

                                              copier.Copy();
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

