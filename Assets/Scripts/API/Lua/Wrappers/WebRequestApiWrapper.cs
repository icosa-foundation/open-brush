using System.Collections.Generic;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.Networking;
namespace TiltBrush
{
    [LuaDocsDescription("Functions to call remote websites or APIs")]
    [MoonSharpUserData]
    public static class WebRequestApiWrapper
    {

        private static void _setHeaders(ref UnityWebRequest request, Table headers)
        {
            request.SetRequestHeader("UserAgent", ApiManager.WEBREQUEST_USER_AGENT);
            if (headers != null)
            {
                foreach (var header in headers.Keys)
                {
                    var key = header.String;
                    var value = headers.Get(key).String;
                    request.SetRequestHeader(key, value);
                }
            }
        }

        public static void Get(string url, Closure onSuccess, Closure onError, Table headers, DynValue context)
        {
            var request = UnityWebRequest.Get(url);
            _setHeaders(ref request, headers);
            request.SendWebRequest();
            LuaManager.Instance.QueueWebRequest(request, onSuccess, onError, context);
        }

        public static void Post(string url, Table postData, Closure onSuccess, Closure onError, Table headers, DynValue context)
        {
            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            foreach (var pair in postData.Pairs)
            {
                formData.Add(new MultipartFormDataSection($"{pair.Key}={pair.Value}"));
            }
            var request = UnityWebRequest.Post(url, formData);
            _setHeaders(ref request, headers);
            request.SendWebRequest();
            LuaManager.Instance.QueueWebRequest(request, onSuccess, onError, context);
        }
    }
}
