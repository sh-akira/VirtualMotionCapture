using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace DVRSDK.Auth
{
    /// <summary>
    /// THIS CLASS CANNOT USE IN UNITY BECAUSE HTTPS(SSL) CONNECTION CANNOT USE IN MONO.
    /// </summary>
    public class DotNetWebRequest : IWebRequest
    {
        private static HttpClientHandler httpClientHandler;
        private static HttpClient httpClient;

        public async Task<WebRequestResponse> GetAsync(string uri, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            CheckHttpClient();
            ClearDefaultHeaders();

            if (headers != null)
            {
                foreach (var pair in headers)
                {
                    httpClient.DefaultRequestHeaders.Add(pair.Key, pair.Value);
                }
            }

            using (var response = await httpClient.GetAsync(uri))
            {
                return await ProcessRequest(response);
            }
        }

        public async Task<WebRequestResponse> PostAsync(string uri, IEnumerable<KeyValuePair<string, string>> headers, byte[] postBody = null)
        {
            if (string.IsNullOrWhiteSpace(uri)) throw new ArgumentNullException(nameof(uri));
            if (headers == null) throw new ArgumentNullException(nameof(headers));

            using (var content = new MultipartFormDataContent())
            {
                foreach (var pair in headers)
                {
                    content.Add(new StringContent(pair.Value), pair.Key);
                }

                CheckHttpClient();
                ClearDefaultHeaders();

                using (var response = await httpClient.PostAsync(uri, content))
                {
                    return await ProcessRequest(response);
                }
            }
        }

        private async Task<WebRequestResponse> ProcessRequest(HttpResponseMessage response)
        {
            var ret = new WebRequestResponse();
            ret.IsSuccess = response.IsSuccessStatusCode;
            ret.Reason = response.ReasonPhrase;
            ret.StatusCode = (int)response.StatusCode;
            ret.Content = await response.Content.ReadAsByteArrayAsync();
            return ret;
        }

        private void CheckHttpClient()
        {
            if (httpClient == null)
            {
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls | System.Net.SecurityProtocolType.Ssl3;
                httpClientHandler = new HttpClientHandler()
                {
                    ClientCertificateOptions = ClientCertificateOption.Automatic,
                    UseDefaultCredentials = true,
                    //SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls11 | System.Security.Authentication.SslProtocols.Tls,
                };
                httpClient = new HttpClient(httpClientHandler);

                // タイムアウトをセット(オプション)
                httpClient.Timeout = TimeSpan.FromSeconds(10.0);
            }
        }

        private void ClearDefaultHeaders()
        {
            // ユーザーエージェント文字列をセット(オプション)
            httpClient.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 6.3; Trident/7.0; rv:11.0) like Gecko");

            // 受け入れ言語をセット(オプション)
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "ja-JP");
        }

        public Task<WebRequestResponse> PutAsync(string uri, IEnumerable<KeyValuePair<string, string>> headers = null, byte[] postBody = null) => throw new NotImplementedException();
    }
}
