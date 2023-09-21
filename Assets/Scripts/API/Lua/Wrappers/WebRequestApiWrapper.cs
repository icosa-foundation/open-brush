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

        [LuaDocsDescription("Sends a GET request to the given URL")]
        [LuaDocsExample(@"WebRequest:Get(""https://www.example.com/"", onSuccess, onError, {[""Accept""] = ""application/json""}, context)")]
        [LuaDocsParameter("url", "The URL to send the request to")]
        [LuaDocsParameter("onSuccess", "A function to call when the request succeeds")]
        [LuaDocsParameter("onError", "A function to call when the request fails")]
        [LuaDocsParameter("headers", "A table of key-value pairs to send as headers")]
        [LuaDocsParameter("context", "A value to pass to the onSuccess and onError functions")]
        public static void Get(string url, Closure onSuccess, Closure onError = null, Table headers = null, DynValue context = null)
        {
            var request = UnityWebRequest.Get(url);
            _setHeaders(ref request, headers);
            request.SendWebRequest();
            LuaManager.Instance.QueueWebRequest(request, onSuccess, onError, context);
        }

        [LuaDocsDescription("Sends a POST request to the given URL with the given data")]
        [LuaDocsExample(@"WebRequest:Post(""https://www.example.com/"", {[""foo""] = ""bar""}, onSuccess, onError, {[""Accept""] = ""application/json""}, context)")]
        [LuaDocsParameter("url", "The URL to send the request to")]
        [LuaDocsParameter("postData", "A table of key-value pairs to send as POST data")]
        [LuaDocsParameter("onSuccess", "A function to call when the request succeeds")]
        [LuaDocsParameter("onError", "A function to call when the request fails")]
        [LuaDocsParameter("headers", "A table of key-value pairs to send as headers")]
        [LuaDocsParameter("context", "A value to pass to the onSuccess and onError functions")]
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
