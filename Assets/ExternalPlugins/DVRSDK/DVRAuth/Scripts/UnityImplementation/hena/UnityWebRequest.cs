using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace DVRSDK.Auth
{
    public class UniWebRequest : IWebRequest
    {
        public async Task<WebRequestResponse> GetAsync(string uri, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            using (var request = UnityWebRequest.Get(uri))
            {
                if (headers != null)
                {
                    foreach (var pair in headers)
                    {
                        request.SetRequestHeader(pair.Key, pair.Value);
                    }
                }
                return await ProcessRequest(request);
            }
        }

        public async Task<WebRequestResponse> PostAsync(string uri, IEnumerable<KeyValuePair<string, string>> headers, byte[] postBody = null)
        {
            if (string.IsNullOrWhiteSpace(uri)) throw new ArgumentNullException(nameof(uri));
            if (headers == null) throw new ArgumentNullException(nameof(headers));

            if (postBody == null)
            {
                var form = new WWWForm();
                foreach (var pair in headers)
                {
                    form.AddField(pair.Key, pair.Value);
                }

                using (var request = UnityWebRequest.Post(uri, form))
                {
                    return await ProcessRequest(request);
                }
            }
            else
            {
                using (var request = UnityWebRequest.Put(uri, postBody))
                {
                    request.method = "POST"; // Trick for POST with body

                    request.SetRequestHeader("Content-Type", "application/json");
                    foreach (var pair in headers)
                    {
                        request.SetRequestHeader(pair.Key, pair.Value);
                    }

                    return await ProcessRequest(request);
                }
            }
        }

        public async Task<WebRequestResponse> PutAsync(string uri, IEnumerable<KeyValuePair<string, string>> headers = null, byte[] postBody = null)
        {
            if (string.IsNullOrWhiteSpace(uri)) throw new ArgumentNullException(nameof(uri));

            using (var request = UnityWebRequest.Put(uri, postBody))
            {
                request.SetRequestHeader("Content-Type", "application/octet-stream");
                request.SetRequestHeader("User-Agent", "UnityWebRequest");
                request.chunkedTransfer = false;
                if (headers != null)
                {
                    foreach (var pair in headers)
                    {
                        request.SetRequestHeader(pair.Key, pair.Value);
                    }
                }
                return await ProcessRequest(request);
            }
        }

        public async Task<WebRequestResponse> ProcessRequest(UnityWebRequest request)
        {
            await request.SendWebRequest();

            var ret = new WebRequestResponse();
            ret.IsSuccess = !request.isNetworkError && !request.isHttpError;
            ret.Reason = request.error;
            ret.StatusCode = (int)request.responseCode;
            ret.Content = request.downloadHandler.data;
            return ret;
        }
    }

    public static class UnityWebRequestAsyncOperationExtension
    {
        public static UnityWebRequestAsyncOperationAwaiter GetAwaiter(this UnityWebRequestAsyncOperation asyncOperation)
        {
            return new UnityWebRequestAsyncOperationAwaiter(asyncOperation);
        }
    }

    public class UnityWebRequestAsyncOperationAwaiter : INotifyCompletion
    {
        UnityWebRequestAsyncOperation _asyncOperation;

        public bool IsCompleted => _asyncOperation.isDone;

        public UnityWebRequestAsyncOperationAwaiter(UnityWebRequestAsyncOperation asyncOperation)
        {
            _asyncOperation = asyncOperation;
        }

        public void GetResult() { }

        public void OnCompleted(Action continuation)
        {
            _asyncOperation.completed += _ => { continuation(); };
        }
    }
}
