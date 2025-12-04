// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Text;
using UnityEngine;

namespace TiltBrush
{
    public class ViverseAuthManager : MonoBehaviour
    {
        private const int HTTP_PORT = 40074;

        public const string REDIRECT_PATH = "/viverse";
        public const string CALLBACK_PATH = "/api/viverse/auth/callback";

        [SerializeField] private string m_ClientId = "42ab6113-acc9-419e-93ca-e0734baf9d3d";

        public event Action<string, string, int, string, string, string, string> OnAuthComplete;
        public event Action<string> OnAuthError;

        private HttpServer m_HttpServer;
        private static readonly Queue<Action> s_MainThreadActions = new Queue<Action>();
        private Action<bool, string> m_OnLoginResult;

        [Serializable]
        private class AuthCallbackData
        {
            public AuthResult data;
        }

        [Serializable]
        private class AuthResult
        {
            public string account_id;
            public string access_token;
            public string refresh_token;
            public int expires_in;
            public string token_type;
            public string profile_name;
            public string avatar_url;
            public string avatar_id;
        }

        private static void EnqueueMainThread(Action action)
        {
            lock (s_MainThreadActions)
            {
                s_MainThreadActions.Enqueue(action);
            }
        }

        private void Update()
        {
            lock (s_MainThreadActions)
            {
                if (s_MainThreadActions.Count > 0)
                {
                    Debug.Log($"[ViverseAuth] UPDATE: Processing {s_MainThreadActions.Count} queued action(s)");
                }

                while (s_MainThreadActions.Count > 0)
                {
                    var action = s_MainThreadActions.Dequeue();
                    action?.Invoke();
                }
            }
        }

        void Start()
        {
            var allHttpServers = FindObjectsOfType<HttpServer>();
            m_HttpServer = allHttpServers.FirstOrDefault(s => s.GetType().Namespace == "TiltBrush");

            if (m_HttpServer == null && allHttpServers.Length > 0)
            {
                m_HttpServer = allHttpServers[0];
            }

            if (m_HttpServer != null)
            {
                Debug.Log($"ViverseAuthManager found HttpServer: {m_HttpServer.GetType().FullName}");

                Debug.Log($"[ViverseAuth] Checking existing handlers...");
                Debug.Log($"[ViverseAuth] Handler exists for '{REDIRECT_PATH}': {m_HttpServer.HttpHandlerExists(REDIRECT_PATH)}");
                Debug.Log($"[ViverseAuth] Handler exists for '{CALLBACK_PATH}': {m_HttpServer.HttpHandlerExists(CALLBACK_PATH)}");

                // Register CALLBACK first (more specific path) because HttpServer uses StartsWith
                if (!m_HttpServer.HttpHandlerExists(CALLBACK_PATH))
                {
                    m_HttpServer.AddRawHttpHandler(CALLBACK_PATH, HandleAuthCallback);
                }
                else
                {
                    m_HttpServer.RemoveHttpHandler(CALLBACK_PATH);
                    m_HttpServer.AddRawHttpHandler(CALLBACK_PATH, HandleAuthCallback);
                }

                if (!m_HttpServer.HttpHandlerExists(REDIRECT_PATH))
                {
                    m_HttpServer.AddRawHttpHandler(REDIRECT_PATH, HandleRedirectPage);
                }
                else
                {
                    m_HttpServer.RemoveHttpHandler(REDIRECT_PATH);
                    m_HttpServer.AddRawHttpHandler(REDIRECT_PATH, HandleRedirectPage);
                }

                Debug.Log($"VIVERSE Auth Handlers registered on port {HTTP_PORT}");
            }
            else
            {
                Debug.LogError("VIVERSEAuthManager could not find an instance of the HttpServer! Authentication flow cannot start.");
            }
        }

        /// <summary>
        /// Initiates VIVERSE authentication flow by opening browser
        /// </summary>
        /// <param name="onResult">Optional callback with (success, message)</param>
        public void StartAuthFlow(Action<bool, string> onResult = null)
        {
            m_OnLoginResult = onResult;
            string url = $"http://localhost:{HTTP_PORT}{REDIRECT_PATH}";
            App.OpenURL(url);
        }

        private HttpListenerContext HandleRedirectPage(HttpListenerContext context)
        {
            Debug.Log($"[ViverseAuth] HandleRedirectPage CALLED URL: {context.Request.Url}");

            string html = GenerateAuthHTML(m_ClientId, CALLBACK_PATH);

            byte[] buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush();

            Debug.Log("[ViverseAuth] HandleRedirectPage complete");
            return context;
        }

        private HttpListenerContext HandleAuthCallback(HttpListenerContext context)
        {
            Debug.Log("[ViverseAuth] HandleAuthCallback CALLED");

            try
            {
                string body;
                using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    body = reader.ReadToEnd();
                }

                var authResultWrapper = JsonUtility.FromJson<AuthCallbackData>(body);

                if (authResultWrapper?.data != null && !string.IsNullOrEmpty(authResultWrapper.data.access_token))
                {
                    var data = authResultWrapper.data;
                    EnqueueMainThread(() =>
                    {
                        OnAuthComplete?.Invoke(data.access_token, data.refresh_token, data.expires_in, data.account_id, data.profile_name, data.avatar_url, data.avatar_id);
                        m_OnLoginResult?.Invoke(true, "Login successful");
                        Debug.Log("[ViverseAuth] Callbacks invoked!");
                    });

                    byte[] successResponse = Encoding.UTF8.GetBytes("{\"status\":\"success\"}");
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.ContentLength64 = successResponse.Length;
                    context.Response.OutputStream.Write(successResponse, 0, successResponse.Length);

                    Debug.Log("[ViverseAuth] Response sent: 200 OK");
                }
                else
                {
                    Debug.LogWarning("[ViverseAuth] No access token in response!");

                    byte[] errorResponse = Encoding.UTF8.GetBytes("{\"status\":\"error\",\"message\":\"No access token\"}");
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.ContentLength64 = errorResponse.Length;
                    context.Response.OutputStream.Write(errorResponse, 0, errorResponse.Length);

                    EnqueueMainThread(() =>
                    {
                        OnAuthError?.Invoke("No access token in response from VIVERSE SDK callback.");
                        m_OnLoginResult?.Invoke(false, "No access token received");
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ViverseAuth] Exception in HandleAuthCallback: {ex.Message}\n{ex.StackTrace}");

                byte[] errorResponse = Encoding.UTF8.GetBytes("{\"status\":\"error\",\"message\":\"Server error\"}");
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = errorResponse.Length;
                context.Response.OutputStream.Write(errorResponse, 0, errorResponse.Length);

                EnqueueMainThread(() =>
                {
                    OnAuthError?.Invoke($"Server error during callback processing: {ex.Message}");
                    m_OnLoginResult?.Invoke(false, $"Server error: {ex.Message}");
                });
            }

            context.Response.OutputStream.Flush();
            Debug.Log("[ViverseAuth] HandleAuthCallback COMPLETE");
            return context;
        }

        private string GenerateAuthHTML(string clientId, string callbackPath)
        {
            string htmlTemplate = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>VIVERSE Login</title>
    <script src=""https://www.viverse.com/static-assets/viverse-sdk/1.3.0/index.umd.cjs""></script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            color: #fff;
        }}
        .container {{
            text-align: center;
            padding: 40px;
            background: rgba(255, 255, 255, 0.1);
            backdrop-filter: blur(10px);
            border-radius: 20px;
            box-shadow: 0 8px 32px 0 rgba(31, 38, 135, 0.37);
            border: 1px solid rgba(255, 255, 255, 0.18);
            max-width: 500px;
            width: 90%;
        }}
        .logo {{
            font-size: 48px;
            font-weight: 700;
            margin-bottom: 20px;
            background: linear-gradient(45deg, #fff, #f0f0f0);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
        }}
        .spinner {{
            width: 50px;
            height: 50px;
            margin: 30px auto;
            border: 4px solid rgba(255, 255, 255, 0.3);
            border-top: 4px solid #fff;
            border-radius: 50%;
            animation: spin 1s linear infinite;
        }}
        @keyframes spin {{ 0% {{ transform: rotate(0deg); }} 100% {{ transform: rotate(360deg); }} }}
        .status {{ font-size: 18px; margin: 20px 0; opacity: 0.9; }}
        .details {{
            margin-top: 30px;
            padding: 20px;
            background: rgba(0, 0, 0, 0.2);
            border-radius: 10px;
            font-size: 12px;
            font-family: 'Courier New', monospace;
            text-align: left;
            max-height: 200px;
            overflow-y: auto;
            word-break: break-all;
            display: none;
        }}
        .details:empty {{ display: none; }}
        .success {{ color: #4ade80; }}
        .error {{ color: #f87171; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""logo"">VIVERSE</div>
        <div id=""status"" class=""status"">Initializing login...</div>
        <div class=""spinner"" id=""spinner""></div>
        <div id=""details"" class=""details""></div>
    </div>
    <script>
        const CLIENT_ID = '{0}';
        const CALLBACK_ENDPOINT = '{1}';
        const statusEl = document.getElementById('status');
        const spinnerEl = document.getElementById('spinner');
        const detailsEl = document.getElementById('details');
        
        function updateStatus(message, type = 'info') {{
            statusEl.textContent = message;
            statusEl.className = 'status ' + (type === 'success' ? 'success' : type === 'error' ? 'error' : '');
        }}
        function showDetails(data) {{ detailsEl.textContent = JSON.stringify(data, null, 2); }}
        function hideSpinner() {{ spinnerEl.style.display = 'none'; }}

        function initializeViverseClient() {{
            return new Promise((resolve) => {{
                if (window.viverse && window.viverse.Client) {{
                    resolve(new window.viverse.Client({{ clientId: CLIENT_ID, domain: 'account.htcvive.com' }}));
                }} else {{
                    setTimeout(() => resolve(initializeViverseClient()), 300);
                }}
            }});
        }}

        async function startLogin() {{
            try {{
                updateStatus('Connecting to VIVERSE...');
                const viverseClient = await initializeViverseClient();
                updateStatus('Redirecting to login...');
                await viverseClient.loginWithRedirect({{ state: 'test' }});
            }} catch (error) {{
                console.error('Login failed:', error);
                updateStatus('Login failed: ' + error.message, 'error');
                showDetails(error);
                hideSpinner();
            }}
        }}

window.addEventListener('load', async () => {{
    if (window.location.search.includes('code=') && window.location.search.includes('state=')) {{
        updateStatus('Processing login callback...');
        try {{
            const viverseClient = await initializeViverseClient();
            const result = await viverseClient.handleRedirectCallback();
            showDetails(result);
            
            if (result && result.access_token) {{
                // User is NOW logged in with valid access_token
                try {{
                    updateStatus('Fetching profile...');
                    
                    const avatarClient = new window.viverse.avatar({{
                        baseURL: 'https://sdk-api.viverse.com/',
                        token: result.access_token
                    }});
                    
                    const profile = await avatarClient.getProfile();
                    
                    result.profile_name = profile.name || '';
                    result.avatar_url = profile.activeAvatar?.headIconUrl || '';
                    result.avatar_id = profile.activeAvatar?.id || '';
                    
                    console.log('Profile fetched:', profile);
                }} catch (profileError) {{
                    console.error('Profile fetch failed:', profileError);
                    // Continue anyway - send auth data even if profile fails
                }}
                
                updateStatus('Sending credentials to application...');
                const response = await fetch(CALLBACK_ENDPOINT, {{
                    method: 'POST',
                    headers: {{ 'Content-Type': 'application/json' }},
                    body: JSON.stringify({{ data: result }})
                }});
                
                if (response.ok) {{
                    hideSpinner();
                    updateStatus('✓ Login successful! You can close this window.', 'success');
                }} else {{
                    hideSpinner();
                    updateStatus('Backend error occurred', 'error');
                }}
            }} else {{
                throw new Error('No access token received from VIVERSE');
            }}
        }} catch (error) {{
            hideSpinner();
            updateStatus('Authentication failed: ' + error.message, 'error');
            showDetails(error);
            console.error('Error during redirect callback:', error);
        }}
    }} else {{
        startLogin();
    }}
}});
    </script>
</body>
</html>";

            return string.Format(htmlTemplate, clientId, callbackPath);
        }

        private void OnDestroy()
        {
            if (m_HttpServer != null)
            {
                m_HttpServer.RemoveHttpHandler(REDIRECT_PATH);
                m_HttpServer.RemoveHttpHandler(CALLBACK_PATH);
            }
        }
    }

    [Serializable]
    public class ViverseProfileResponse
    {
        public string name; // User's display name
        public ActiveAvatarData activeAvatar;
    }

    [Serializable]
    public class ActiveAvatarData
    {
        public string id;
        public string headIconUrl; // Profile picture
        public string vrmUrl;
        public string snapshot;
    }
} // namespace TiltBrush
