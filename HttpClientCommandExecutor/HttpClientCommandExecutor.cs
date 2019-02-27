using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Internal;
using OpenQA.Selenium.Remote;

namespace HttpClientCommandExecutor
{
    public class HttpClientCommandExecutor : HttpCommandExecutor
    {
        private const string JsonMimeType = "application/json";
        private const string PngMimeType = "image/png";
        private const string Utf8CharsetType = "utf-8";
        private const string RequestAcceptHeader = JsonMimeType + ", " + PngMimeType;
        private const string UserAgentHeaderTemplate = "selenium/{0} (.net {1})";
        private Uri remoteServerUri;
        private TimeSpan serverResponseTimeout;
        private HttpClient client;

        public HttpClientCommandExecutor(Uri addressOfRemoteServer, TimeSpan timeout)
            : this(addressOfRemoteServer, timeout, true)
        {
        }

        public HttpClientCommandExecutor(Uri addressOfRemoteServer, TimeSpan timeout, bool enableKeepAlive)
            : base(addressOfRemoteServer, timeout, enableKeepAlive)
        {
            this.remoteServerUri = addressOfRemoteServer;
            this.serverResponseTimeout = timeout;
        }

        public override Response Execute(Command commandToExecute)
        {
            if (commandToExecute == null)
            {
                throw new ArgumentNullException("commandToExecute", "commandToExecute cannot be null");
            }

            if (this.client == null)
            {
                this.CreateHttpClient();
            }

            CommandInfo commandInfo = this.CommandInfoRepository.GetCommandInfo(commandToExecute.Name);

            HttpRequestInfo requestInfo = new HttpRequestInfo(this.remoteServerUri, commandToExecute, commandInfo);
            HttpResponseInfo responseInfo = this.MakeHttpRequest(requestInfo).GetAwaiter().GetResult();
            Response response = this.CreateResponse(responseInfo);
            if (commandToExecute.Name == DriverCommand.NewSession && response.IsSpecificationCompliant)
            {
                // Note: This requires a to-be-released version of Selenium. If you
                // need to use this with a prior version, you'll need to update the
                // commandInfoRepository field using reflection.
                //this.CommandInfoRepository = new W3CWireProtocolCommandInfoRepository();
                CommandInfoRepository commandInfoRepo = new W3CWireProtocolCommandInfoRepository();
                ReflectionHelper.SetFieldValue<CommandInfoRepository>(this, "commandInfoRepository", commandInfoRepo);
            }

            return response;
        }

        protected override void Dispose(bool disposing)
        {
            if (this.client != null)
            {
                this.client.Dispose();
            }

            base.Dispose(disposing);
        }

        private async Task<HttpResponseInfo> MakeHttpRequest(HttpRequestInfo requestInfo)
        {
            SendingRemoteHttpRequestEventArgs eventArgs = new SendingRemoteHttpRequestEventArgs(null, requestInfo.RequestBody);
            this.OnSendingRemoteHttpRequest(eventArgs);

            HttpMethod method = new HttpMethod(requestInfo.HttpMethod);
            HttpRequestMessage requestMessage = new HttpRequestMessage(method, requestInfo.FullUri);
            if (requestInfo.HttpMethod == CommandInfo.GetCommand)
            {
                CacheControlHeaderValue cacheControlHeader = new CacheControlHeaderValue();
                cacheControlHeader.NoCache = true;
                requestMessage.Headers.CacheControl = cacheControlHeader;
            }

            if (requestInfo.HttpMethod == CommandInfo.PostCommand)
            {
                MediaTypeWithQualityHeaderValue acceptHeader = new MediaTypeWithQualityHeaderValue(JsonMimeType);
                acceptHeader.CharSet = Utf8CharsetType;
                requestMessage.Headers.Accept.Add(acceptHeader);

                byte[] bytes = Encoding.UTF8.GetBytes(eventArgs.RequestBody);
                requestMessage.Content = new ByteArrayContent(bytes, 0, bytes.Length);
            }

            HttpResponseMessage responseMessage = await this.client.SendAsync(requestMessage);
            if (responseMessage.StatusCode == HttpStatusCode.RequestTimeout)
            {
                throw new WebDriverException(string.Format(CultureInfo.InvariantCulture, "The HTTP request to the remote WebDriver server for URL {0} timed out after {1} seconds.", requestInfo.FullUri, this.serverResponseTimeout.TotalSeconds));
            }

            if (responseMessage.Content == null)
            {
                throw new WebDriverException(string.Format(CultureInfo.InvariantCulture, "A exception with a null response was thrown sending an HTTP request to the remote WebDriver server for URL {0}. The status of the exception was {1}", requestInfo.FullUri, responseMessage.StatusCode));
            }

            HttpResponseInfo httpResponseInfo = new HttpResponseInfo();
            httpResponseInfo.Body = await responseMessage.Content.ReadAsStringAsync();
            httpResponseInfo.ContentType = responseMessage.Content.Headers.ContentType.ToString();
            httpResponseInfo.StatusCode = responseMessage.StatusCode;
            return httpResponseInfo;
        }

        private Response CreateResponse(HttpResponseInfo responseInfo)
        {
            Response response = new Response();
            string body = responseInfo.Body;
            if (responseInfo.ContentType != null && responseInfo.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                response = Response.FromJson(body);
            }
            else
            {
                response.Value = body;
            }

            if (this.CommandInfoRepository.SpecificationLevel < 1 && (responseInfo.StatusCode < HttpStatusCode.OK || responseInfo.StatusCode >= HttpStatusCode.BadRequest))
            {
                if (responseInfo.StatusCode >= HttpStatusCode.BadRequest && responseInfo.StatusCode < HttpStatusCode.InternalServerError)
                {
                    response.Status = WebDriverResult.UnhandledError;
                }
                else if (responseInfo.StatusCode >= HttpStatusCode.InternalServerError)
                {
                    if (responseInfo.StatusCode == HttpStatusCode.NotImplemented)
                    {
                        response.Status = WebDriverResult.UnknownCommand;
                    }
                    else if (response.Status == WebDriverResult.Success)
                    {
                        response.Status = WebDriverResult.UnhandledError;
                    }
                }
                else
                    response.Status = WebDriverResult.UnhandledError;
            }

            if (response.Value is string)
            {
                response.Value = ((string)response.Value).Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            }

            return response;
        }

        private void CreateHttpClient()
        {
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            string userInfo = this.remoteServerUri.UserInfo;
            if (!string.IsNullOrEmpty(userInfo) && userInfo.Contains(":"))
            {
                string[] userInfoComponents = this.remoteServerUri.UserInfo.Split(new char[] { ':' }, 2);
                httpClientHandler.Credentials = new NetworkCredential(userInfoComponents[0], userInfoComponents[1]);
                httpClientHandler.PreAuthenticate = true;
            }

            httpClientHandler.Proxy = this.Proxy;
            httpClientHandler.MaxConnectionsPerServer = 2000;

            this.client = new HttpClient(httpClientHandler);
            string userAgentString = string.Format(CultureInfo.InvariantCulture, UserAgentHeaderTemplate, ResourceUtilities.AssemblyVersion, ResourceUtilities.PlatformFamily);
            this.client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgentString);

            this.client.DefaultRequestHeaders.Accept.ParseAdd(RequestAcceptHeader);
            if (!this.IsKeepAliveEnabled)
            {
                this.client.DefaultRequestHeaders.Connection.ParseAdd("close");
            }

            this.client.Timeout = this.serverResponseTimeout;
        }

        private class HttpRequestInfo
        {
            public HttpRequestInfo(Uri serverUri, Command commandToExecute, CommandInfo commandInfo)
            {
                this.FullUri = commandInfo.CreateCommandUri(serverUri, commandToExecute);
                this.HttpMethod = commandInfo.Method;
                this.RequestBody = commandToExecute.ParametersAsJsonString;
            }

            public Uri FullUri { get; set; }
            public string HttpMethod { get; set; }
            public string RequestBody { get; set; }
        }

        private class HttpResponseInfo
        {
            public HttpStatusCode StatusCode { get; set; }
            public string Body { get; set; }
            public string ContentType { get; set; }
        }
    }
}
