// Copyright 2024 Open Brush Authors
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
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// HTTP server for handling VIVERSE OAuth2 callbacks
    /// Integrates with Open Brush's OAuth2Identity system
    /// </summary>
    public class ViverseHttpServer : MonoBehaviour
    {
        public const int DEFAULT_PORT = 40078;

        [SerializeField] private string m_ClientId = "42ab6113-acc9-419e-93ca-e0734baf9d3d";
        [SerializeField] private int m_Port = DEFAULT_PORT;

        private HttpListener m_HttpListener;
        private bool m_IsRunning;
        private CancellationTokenSource m_ServerCts;

        // Security tokens
        private string m_StateToken;
        private string m_SecretKey;

        // Callback for when authentication completes
        public event Action<string, string, int> OnAuthComplete; // accessToken, refreshToken, expiresIn
        public event Action<string> OnAuthError;

        private static readonly Queue<Action> s_MainThreadActions = new Queue<Action>();

        /// <summary>
        /// Start the HTTP server for OAuth callback
        /// </summary>
        public bool StartServer()
        {
            if (m_IsRunning)
            {
                Debug.LogWarning("[ViverseHttpServer] Server already running");
                return true;
            }
            
            m_StateToken = GenerateSecureRandom(32);
            m_SecretKey = GenerateSecureRandom(32);

#if UNITY_ANDROID && !UNITY_EDITOR
    // --- Android Native Server ---
    try
    {
        AndroidHttpServerBridge.StartNativeHttpServer(m_Port);
        m_IsRunning = true;
        Debug.Log($"[ViverseHttpServer] Native Android server started on port {m_Port}");
        return true;
    }
    catch (Exception ex)
    {
        Debug.LogError($"[ViverseHttpServer] Failed to start native server: {ex.Message}");
        return false;
    }

#else
            // --- Editor/Desktop C# HttpListener ---
            try
            {
                m_ServerCts = new CancellationTokenSource();
                // C# version remains asynchronous, but relies on OAuth2Identity to open the URL
                Task.Run(() => InitHttpListener(), m_ServerCts.Token);
                m_IsRunning = true;
                Debug.Log($"[ViverseHttpServer] C# Server started on port {m_Port}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ViverseHttpServer] Failed to start: {ex.Message}");
                return false;
            }
#endif
        }

        /// <summary>
        /// Stop the HTTP server
        /// </summary>
        public void StopServer()
        {
            m_IsRunning = false;

            if (m_ServerCts != null)
            {
                m_ServerCts.Cancel();
                m_ServerCts.Dispose();
                m_ServerCts = null;
            }

            if (m_HttpListener != null)
            {
                try
                {
                    m_HttpListener.Stop();
                    m_HttpListener.Close();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ViverseHttpServer] Error stopping: {ex.Message}");
                }
                m_HttpListener = null;
            }

            Debug.Log("[ViverseHttpServer] Server stopped");
        }

        /// <summary>
        /// Get the authentication URL to open in browser
        /// </summary>
        public string GetAuthUrl()
        {
            return $"http://localhost:{m_Port}/";
        }

        private async void InitHttpListener()
        {
            try
            {
                m_HttpListener = new HttpListener();

                // Try multiple ports if primary fails
                int[] ports = { m_Port, m_Port + 1, m_Port + 2 };
                bool bound = false;

                foreach (int port in ports)
                {
                    try
                    {
                        m_HttpListener.Prefixes.Clear();
                        m_HttpListener.Prefixes.Add($"http://localhost:{port}/");
                        m_HttpListener.Start();
                        m_Port = port;
                        bound = true;
                        Debug.Log($"[ViverseHttpServer] Bound to port {port}");
                        break;
                    }
                    catch (HttpListenerException)
                    {
                        Debug.LogWarning($"[ViverseHttpServer] Port {port} in use");
                    }
                }

                if (!bound)
                {
                    throw new Exception("All ports in use");
                }

                while (m_IsRunning && !m_ServerCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var context = await m_HttpListener.GetContextAsync();
                        _ = Task.Run(() => HandleRequest(context));
                    }
                    catch (HttpListenerException)
                    {
                        break; // Server stopped
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ViverseHttpServer] Error in listener: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ViverseHttpServer] Failed to initialize: {ex.Message}");
                EnqueueMainThread(() => OnAuthError?.Invoke($"Server error: {ex.Message}"));
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            string path = context.Request.Url.AbsolutePath;
            string method = context.Request.HttpMethod;

            try
            {
                if (path == "/api/viverse/auth/callback" && method == "POST")
                {
                    HandleAuthCallback(context);
                }
                else if (method == "GET")
                {
                    ServeAuthPage(context);
                }
                else
                {
                    SendResponse(context, 404, "Not Found", "text/plain");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ViverseHttpServer] Error handling request: {ex.Message}");
                SendResponse(context, 500, "Internal Server Error", "text/plain");
            }
        }

        private void HandleAuthCallback(HttpListenerContext context)
        {
            // Read POST body
            string body;
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                body = reader.ReadToEnd();
            }

            Debug.Log($"[ViverseHttpServer] Auth callback received");

            // Validate secret key
            string receivedSecret = context.Request.Headers["X-Secret-Key"];

            Debug.Log($"[ViverseHttpServer] Expected Secret: {m_SecretKey}");
            Debug.Log($"[ViverseHttpServer] Received Secret: {receivedSecret}");

            if (string.IsNullOrEmpty(receivedSecret) || receivedSecret != m_SecretKey)
            {
                Debug.LogWarning("[ViverseHttpServer] Invalid secret - possible attack");
                SendJsonResponse(context, 403, "{\"error\":\"Forbidden\"}");
                return;
            }

            // Parse response
            try
            {
                var authData = JsonUtility.FromJson<AuthCallbackData>(body);

                if (authData?.data != null && !string.IsNullOrEmpty(authData.data.access_token))
                {
                    // Success - notify on main thread
                    string accessToken = authData.data.access_token;
                    string refreshToken = authData.data.refresh_token;
                    int expiresIn = authData.data.expires_in;

                    EnqueueMainThread(() =>
                    {
                        OnAuthComplete?.Invoke(accessToken, refreshToken, expiresIn);
                        StopServer();
                    });

                    SendJsonResponse(context, 200, "{\"status\":\"success\"}");
                }
                else
                {
                    SendJsonResponse(context, 400, "{\"error\":\"No access token\"}");
                    EnqueueMainThread(() => OnAuthError?.Invoke("No access token in response"));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ViverseHttpServer] Parse error: {ex.Message}");
                SendJsonResponse(context, 400, "{\"error\":\"Invalid JSON\"}");
                EnqueueMainThread(() => OnAuthError?.Invoke($"Parse error: {ex.Message}"));
            }
        }

        private void ServeAuthPage(HttpListenerContext context)
        {
            string secretKey = m_SecretKey;
            string stateToken = m_StateToken;
            string clientId = m_ClientId;
    
            // Generate the full VIVERSE authorization URL once.
            string viverseRedirectUrl = this.GetAuthUrl();
    
            // Check if this is the final redirect back from VIVERSE (contains the 'code')
            if (!string.IsNullOrEmpty(context.Request.QueryString["code"]))
            {
                // Validation Check
                string stateParam = context.Request.QueryString["state"];
                string secretParam = context.Request.QueryString["secret"];
        
                if (stateParam != stateToken || secretParam != secretKey) 
                {
                    SendResponse(context, 403, "Forbidden", "text/plain");
                    return;
                }

                // Serve the universal HTML page. The JavaScript will automatically detect the 
                // 'code' parameter and initiate the token exchange (POST to /auth/callback).
            }
    
            // Serve the HTML page (either the initial login prompt or the final code handler)
            string html = GenerateAuthHTML(secretKey, stateToken, clientId, viverseRedirectUrl); 
    
            SendResponse(context, 200, html, "text/html; charset=utf-8");
        }

        /// <summary>
        /// Generates the initial HTML page prompting the user to click a button to initiate the external VIVERSE login flow.
        /// </summary>
        /// <param name="authUrl">The complete URL (from GetAuthUrl()) pointing to the VIVERSE authorization server.</param>
        /// <returns>The complete HTML string.</returns>
        private string GenerateLoginPageHTML(string authUrl)
        {
            // The HTML contains a button linked directly to the VIVERSE authorization URL.
            string html = $@"
<!doctype html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"">
    <title>Open Brush VIVERSE Login</title>
    <style>
        body {{ font-family: sans-serif; text-align: center; padding-top: 50px; }}
        #login-button {{ 
            background-color: #0088cc; 
            color: white; 
            padding: 15px 30px; 
            font-size: 1.2em; 
            border: none; 
            cursor: pointer; 
            border-radius: 5px; 
            text-decoration: none;
            display: inline-block; /* Essential for the <a> tag to look like a button */
        }}
    </style>
</head>
<body>
    <h1>VIVERSE Authentication Required</h1>
    <p>Please log in to your VIVERSE account to continue using Open Brush.</p>
    
    <a id=""login-button"" href=""{authUrl}"">
        Login with VIVERSE
    </a>

    <p style=""margin-top: 20px; color: gray;"">
        Click the button above to be redirected to the VIVERSE login page.
    </p>
</body>
</html>";

            return html;
        }

        
private string GenerateAuthHTML(string secretKey, string stateToken, string clientId, string viverseRedirectUrl)
{
    
    return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <script defer src=""https://www.viverse.com/static-assets/viverse-sdk/1.3.0/index.umd.cjs""></script>
</head>
<body>
    <h1>🎨 Open Brush - VIVERSE Login</h1>
    <p>Click the button below to authenticate with your VIVERSE account.</p>
    <button id=""login-button"" onclick=""login()"">Login with VIVERSE</button>
    <div id=""status""></div>
  
    <script>
        // *** CRITICAL FIXES: Use the C# parameter names ***
        const SECRET_KEY = '{secretKey}'; 
        const STATE_TOKEN = '{stateToken}'; 
        const VIVERSE_AUTH_URL = '{viverseRedirectUrl}'; // New: Used for direct redirect
        
        function showStatus(message, type) {{
            // ... (Implementation remains the same) ...
        }}
        
        function initializeViverseClient() {{
            return new Promise((resolve) => {{
                if (window.viverse && window.viverse.Client) {{
                    resolve(new window.viverse.Client({{
                        // *** CRITICAL FIX: Use the C# parameter name ***
                        clientId: '{clientId}',
                        domain: 'account.htcvive.com',
                    }}));
                }} else {{
                    setTimeout(() => resolve(initializeViverseClient()), 300);
                }}
            }});
        }}
        
        async function login() {{
            showStatus('Initializing...', 'loading');
            try {{
                // Use the pre-generated VIVERSE URL directly for simplicity and robustness
                showStatus('Redirecting to VIVERSE...', 'loading');
                window.location.href = VIVERSE_AUTH_URL; //remove this
                
                const viverseClient = await initializeViverseClient();
                await viverseClient.loginWithRedirect({{ state: STATE_TOKEN }});
                
                
            }} catch (error) {{
                console.error('Login failed:', error);
                showStatus('Login failed: ' + error.message, 'error');
            }}
        }}
        
        // ... (window.addEventListener('load') logic remains the same for handling the callback) ...
    </script>
</body>
</html>";
}

        private void SendResponse(HttpListenerContext context, int statusCode, string body, string contentType)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        private void SendJsonResponse(HttpListenerContext context, int statusCode, string json)
        {
            SendResponse(context, statusCode, json, "application/json");
        }

        private string GenerateSecureRandom(int length)
        {
            byte[] bytes = new byte[length];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes)
                .Replace("+", "").Replace("/", "").Replace("=", "")
                .Substring(0, length);
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
                while (s_MainThreadActions.Count > 0)
                {
                    s_MainThreadActions.Dequeue()?.Invoke();
                }
            }
        }

        private void OnDestroy()
        {
            StopServer();
        }

        [Serializable]
        private class AuthCallbackData
        {
            public AuthResult data;
        }

        [Serializable]
        private class AuthResult
        {
            public string access_token;
            public string refresh_token;
            public int expires_in;
            public string token_type;
        }
    }
} // namespace TiltBrush
