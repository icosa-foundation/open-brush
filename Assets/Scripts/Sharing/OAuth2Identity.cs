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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.PeopleService.v1;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TiltBrush
{

    /// Handle accessing OAuth2 based web services.
    public class OAuth2Identity : MonoBehaviour
    {

        public class UserInfo
        {
            public string id;
            public string name;
            public string email;
            public Texture2D icon;
            public bool isGoogle;
        }

        public class UserInfoError : Exception
        {
            public UserInfoError(string message) : base(message) { }
        }

        // static API

        /// Sent when _any_ profile is updated.
        public static event Action<OAuth2Identity> ProfileUpdated;

        public event Action OnSuccessfulAuthorization;
        public event Action OnLogout;

        // See FIFE's image_url_util.SetImageUrlOptions
        public static string SetImageUrlOptions(string originalUrl)
        {
            // format is https://HOST/[PREFIX/]/ENCRYPTEDPROTO[=OPTIONS]
            string withoutOptions = Regex.Replace(originalUrl, @"=[^/=]+$", "");

            //     s128   size=128
            //     -c     crop
            //     -k     kill_animation
            //     -no    no_overlay
            //     -rj    request jpeg
            return withoutOptions + "=s128-c-k-no-rj";
        }

#if UNITY_EDITOR
        [MenuItem("Open Brush/Cloud/Log in (Google)")]
        private static void LogInGoogle() => App.GoogleIdentity.LoginAsync();
        [MenuItem("Open Brush/Cloud/Log in (Sketchfab)")]
        private static void LogInSketchfab() => App.SketchfabIdentity.LoginAsync();
        [MenuItem("Open Brush/Cloud/Log in (Viverse)")]
        private static void LogInViverse() => App.ViveIdentity.LoginAsync();
        [MenuItem("Open Brush/Cloud/Log out (All)")]
        private static void LogOutAll()
        {
            App.GoogleIdentity.Logout();
            App.SketchfabIdentity.Logout();
            App.ViveIdentity.Logout();
        }
        [MenuItem("Open Brush/Cloud/Log in (Sketchfab)", true)]
        [MenuItem("Open Brush/Cloud/Log in (Google)", true)]
        [MenuItem("Open Brush/Cloud/Log out (All)", true)]
        private static bool EnablePlayModeOnly() => Application.isPlaying;
#endif

        // instance API
        [SerializeField] private SecretsConfig.Service m_Service;
        [SerializeField] private string[] m_OAuthScopes;
        [SerializeField] private string[] m_AdditionalDesktopOAuthScopes;
        [SerializeField] private string m_CallbackPath;
        [SerializeField] private Texture2D m_LoggedInTexture;
        [SerializeField] private string m_TokenStorePrefix;

        [Header("Server URLs should be blank if using Google authorization")]
        [SerializeField] private string m_AuthorizationServerUrl;
        [SerializeField] private string m_TokenServerUrl;

        private UserInfo m_User = null;
        private PlayerPrefsDataStore m_TokenDataStore;
        private OAuth2CredentialRequest m_CredentialRequest;

        public bool IsGoogle => m_Service == SecretsConfig.Service.Google;
        public bool IsIcosa => m_Service == SecretsConfig.Service.Icosa;
        public UserCredential UserCredential => m_CredentialRequest?.UserCredential;
        private SecretsConfig.ServiceAuthData ServiceAuthData => App.Config.Secrets?[m_Service];
        public string ClientId => ServiceAuthData?.ClientId;
        private string ClientSecret => ServiceAuthData?.ClientSecret;
        private ViverseAuthManager m_ViverseAuthManager;
        private ViverseTokenData m_ViverseToken;

        public UserInfo Profile
        {
            get { return m_User; }
            set
            {
                m_User = value;
                OAuth2Identity.ProfileUpdated?.Invoke(this);
            }
        }

        public bool LoggedIn
        {
            // We don't consider us logged in until we have the UserInfo
            get
            {
                if (m_Service == SecretsConfig.Service.Vive)
                {
                    return m_ViverseToken != null && m_ViverseToken.IsValid && Profile != null;
                }

                return UserCredential?.Token?.AccessToken != null && Profile != null;
            }
        }

        public bool HasAccessToken
        {
            get
            {
                if (m_Service == SecretsConfig.Service.Vive)
                {
                    return m_ViverseToken != null && m_ViverseToken.IsValid;
                }

                return UserCredential?.Token?.AccessToken != null;
            }
        }

        public async Task<string> GetAccessToken()
        {
            if (m_Service == SecretsConfig.Service.Vive)
            {
                if (m_ViverseToken != null && m_ViverseToken.IsValid)
                {
                    return m_ViverseToken.AccessToken;
                }

                Debug.LogWarning("VIVERSE: Token is expired or missing, need to re-authenticate");
                return null;
            }

            if (UserCredential == null) { return null; }
            return await UserCredential.GetAccessTokenForRequestAsync();
        }

        private void Awake()
        {
            InitializeAsync().WrapErrors();
        }

        /// The returned Task signals that OAuth2Identity is fully initialized and operational.
        /// This is only public so it can be used in unit tests.
        public async Task InitializeAsync()
        {

            if (m_Service == SecretsConfig.Service.Vive)
            {
                m_ViverseAuthManager = FindObjectOfType<ViverseAuthManager>();

                if (m_ViverseAuthManager == null)
                {
                    GameObject go = new GameObject("ViverseAuthManager");
                    go.transform.SetParent(App.Instance.transform);
                    m_ViverseAuthManager = go.AddComponent<ViverseAuthManager>();
                }

                m_ViverseAuthManager.OnAuthComplete += OnViverseAuthComplete;
                m_ViverseAuthManager.OnAuthError += OnViverseAuthError;

                string tokenJson = PlayerPrefs.GetString("viverse_token", "");
                if (!string.IsNullOrEmpty(tokenJson))
                {
                    try
                    {
                        m_ViverseToken = JsonUtility.FromJson<ViverseTokenData>(tokenJson);

                        if (m_ViverseToken != null && m_ViverseToken.IsValid)
                        {
                            await GetUserInfoAsync(onStartup: true, forTesting: !Application.isPlaying);

                            if (LoggedIn)
                            {
                                OnSuccessfulAuthorization?.Invoke();
                            }
                        }
                        else
                        {
                            m_ViverseToken = null;
                        }
                    }
                    catch (Exception)
                    {
                        m_ViverseToken = null;
                    }
                }

                return;
            }

            if (string.IsNullOrEmpty(ClientId) || string.IsNullOrEmpty(ClientSecret))
            {
                Debug.LogWarning(
                    $"Attempted to initialize to {m_Service} with missing Client Id or Client Secret.");
                return;
            }

            m_TokenDataStore = new PlayerPrefsDataStore(m_TokenStorePrefix);
            var scopes = App.Config.IsMobileHardware
                ? m_OAuthScopes
                : m_OAuthScopes.Concat(m_AdditionalDesktopOAuthScopes).ToArray();
            if (scopes != null && scopes.Length == 0)
            {
                scopes = null;
            }

            if (string.IsNullOrWhiteSpace(m_AuthorizationServerUrl) ||
                string.IsNullOrWhiteSpace(m_TokenServerUrl))
            {
                Debug.Assert(
                    string.IsNullOrWhiteSpace(m_AuthorizationServerUrl) &&
                    string.IsNullOrWhiteSpace(m_TokenServerUrl),
                    $"Both or neither of the server URLs must be set for {name}");
                // Use Google authorization code flow.
                m_CredentialRequest = new OAuth2CredentialRequest(
                    new ClientSecrets
                    {
                        ClientId = ClientId,
                        ClientSecret = ClientSecret,
                    },
                    scopes,
                    m_CallbackPath,
                    m_TokenDataStore);
            }
            else
            {
                // Use the generic authorization code.
                m_CredentialRequest = new OAuth2CredentialRequest(
                    new ClientSecrets
                    {
                        ClientId = ClientId,
                        ClientSecret = ClientSecret,
                    },
                    scopes,
                    m_CallbackPath,
                    m_TokenDataStore,
                    m_AuthorizationServerUrl,
                    m_TokenServerUrl);
            }

            // If we have a stored user token, see if we can refresh it and log in automatically.
            if (m_TokenDataStore.UserTokens() != null)
            {
                await ReauthorizeAsync();
                await GetUserInfoAsync(onStartup: true, forTesting: !Application.isPlaying);
                if (LoggedIn)
                {
                    OnSuccessfulAuthorization?.Invoke();
                }
            }
        }

        private void OnViverseAuthComplete(string accessToken, string refreshToken, int expiresIn, string accountId, string profileName, string avatarUrl, string avatarId)
        {
            try
            {
                // Create VIVERSE token
                m_ViverseToken = ViverseTokenData.FromAuthResponse(accessToken, refreshToken, expiresIn);

                // Save to PlayerPrefs
                string tokenJson = JsonUtility.ToJson(m_ViverseToken);
                PlayerPrefs.SetString("viverse_token", tokenJson);
                PlayerPrefs.Save();

                if (VrAssetService.m_Instance != null)
                {
                    VrAssetService.m_Instance.ConsumeUploadResults();
                }

                // Set Profile immediately with data from JavaScript
                Profile = new UserInfo
                {
                    id = accountId ?? "viverse_user",
                    name = profileName ?? "VIVERSE User",
                    email = "",
                    icon = m_LoggedInTexture, // Default icon first
                    isGoogle = false
                };

                // Download avatar async if URL exists
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    DownloadViverseAvatarAsync(avatarUrl).WrapErrors();
                }

                // Notify success
                OnSuccessfulAuthorization?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"VIVERSE: Error saving token: {ex.Message}");
                OnViverseAuthError($"Failed to save token: {ex.Message}");
            }
        }

        private async Task DownloadViverseAvatarAsync(string avatarUrl)
        {
            try
            {
                Texture2D avatar = await ImageUtils.DownloadTextureAsync(avatarUrl);
                if (avatar != null && !(avatar.width == 8 && avatar.height == 8))
                {
                    Profile.icon = avatar;
                    OAuth2Identity.ProfileUpdated?.Invoke(this);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"VIVERSE: Avatar download failed - {ex.Message}");
            }
        }

        private async Task LoginViverseAsync()
        {
            if (m_ViverseAuthManager == null)
            {
                ControllerConsoleScript.m_Instance?.AddNewLine("VIVERSE: Auth Manager not initialized");
                return;
            }

            m_ViverseAuthManager.StartAuthFlow();

            ControllerConsoleScript.m_Instance?.AddNewLine("Opening VIVERSE login in browser...");
        }

        private void OnViverseAuthError(string error)
        {
            Debug.LogError($"VIVERSE auth failed: {error}");
            if (ControllerConsoleScript.m_Instance != null)
            {
                ControllerConsoleScript.m_Instance.AddNewLine($"VIVERSE login failed: {error}");
            }
        }

        private async Task<UserInfo> GetUserInfoViverseAsync(bool forTesting)
        {
            string accessToken = m_ViverseToken?.AccessToken;
            if (string.IsNullOrEmpty(accessToken))
            {
                Debug.LogError("VIVERSE: No access token");
                return null;
            }

            using (UnityWebRequest request = UnityWebRequest.Get("https://sdk-api.viverse.com/avatar/profile"))
            {
                request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                await request.SendWebRequest();

                Debug.Log($"VIVERSE Profile API: {request.responseCode} - {request.downloadHandler.text}");

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"VIVERSE: Profile API failed - {request.error}");
                    return null;
                }

                var profile = JsonUtility.FromJson<ViverseProfileResponse>(request.downloadHandler.text);

                Texture2D profileIcon = m_LoggedInTexture;
                if (!string.IsNullOrEmpty(profile?.activeAvatar?.headIconUrl) && !forTesting)
                {
                    try
                    {
                        profileIcon = await ImageUtils.DownloadTextureAsync(profile.activeAvatar.headIconUrl);
                        if (profileIcon == null || (profileIcon.width == 8 && profileIcon.height == 8))
                        {
                            profileIcon = m_LoggedInTexture;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"VIVERSE: Avatar download failed - {ex.Message}");
                        profileIcon = m_LoggedInTexture;
                    }
                }

                return new UserInfo
                {
                    id = profile?.activeAvatar?.id ?? "viverse_user",
                    name = profile?.name ?? "VIVERSE User",
                    email = "",
                    icon = profileIcon,
                    isGoogle = false
                };
            }
        }

        /// Force-performs authentication - will cancel any current authentication in progress.
        public async Task AuthorizeAsync()
        {
            if (m_CredentialRequest.IsAuthorizing)
            {
                await m_CredentialRequest.Cancel();
            }
            await m_CredentialRequest.AuthorizeAsync();
        }

        // Will attempt to reauthorize if we have a refresh token and we're not currently
        // authorizing.
        public async Task ReauthorizeAsync()
        {
            if (m_TokenDataStore.UserTokens() == null || m_CredentialRequest.IsAuthorizing)
            {
                return;
            }
            await m_CredentialRequest.AuthorizeAsync();
        }

        public async void LoginAsync()
        {
            if (m_Service == SecretsConfig.Service.Vive)
            {
                await LoginViverseAsync();
                return;
            }

            if (!IsIcosa && (string.IsNullOrEmpty(ClientId) || string.IsNullOrEmpty(ClientSecret)))
            {
                Debug.LogWarning(
                    $"Attempted to log in to {m_Service} with missing Client Id or Client Secret.");
                return;
            }
            await AuthorizeAsync();
            await GetUserInfoAsync();
            if (LoggedIn)
            {
                OnSuccessfulAuthorization?.Invoke();
            }
        }

        public void Logout()
        {
            if (m_Service == SecretsConfig.Service.Vive)
            {
                if (m_ViverseAuthManager != null)
                {
                    m_ViverseAuthManager.OnAuthComplete -= OnViverseAuthComplete;
                    m_ViverseAuthManager.OnAuthError -= OnViverseAuthError;
                }

                if (Profile != null && ControllerConsoleScript.m_Instance != null)
                {
                    ControllerConsoleScript.m_Instance.AddNewLine(Profile.name + " logged out.");
                }

                m_ViverseToken = null;
                PlayerPrefs.DeleteKey("viverse_token");
                PlayerPrefs.Save();

                Profile = null;
                OnLogout?.Invoke();
                return;
            }

            if (UserCredential?.Token?.RefreshToken != null)
            {
                // Not sure if it's possible for m_User to be null here.
                if (Profile != null)
                {
                    ControllerConsoleScript.m_Instance.AddNewLine(Profile.name + " logged out.");
                }
                else
                {
                    ControllerConsoleScript.m_Instance.AddNewLine("Logged out.");
                }
                m_CredentialRequest.RevokeCredential();
                m_TokenDataStore.ClearAsync();
                Profile = null;
            }
            OnLogout?.Invoke();
        }

        /// Sign an outgoing request.
        public async Task Authenticate(UnityWebRequest www)
        {

            if (m_Service == SecretsConfig.Service.Vive)
            {
                string accessToken = await GetAccessToken();
                if (!string.IsNullOrEmpty(accessToken))
                {
                    www.SetRequestHeader("AccessToken", accessToken);
                }
                return;
            }

            if (UserCredential?.Token != null)
            {
                string accessToken = await GetAccessToken();
                www.SetRequestHeader("Authorization", String.Format("Bearer {0}", accessToken));
            }
        }

        // Attempts to fill in this.Profile
        private async Task GetUserInfoAsync(bool onStartup = false, bool forTesting = false)
        {
            if (m_Service == SecretsConfig.Service.Vive)
            {
                if (m_ViverseToken != null && m_ViverseToken.IsValid)
                {
                    Profile = await GetUserInfoViverseAsync(forTesting);

                    if (!forTesting && ControllerConsoleScript.m_Instance != null)
                    {
                        ControllerConsoleScript.m_Instance.AddNewLine(Profile.name + " logged in.");
                    }
                    return;
                }

                if (forTesting)
                {
                    throw new InvalidOperationException("You must have a valid token to test VIVERSE");
                }
                return;
            }

            if (String.IsNullOrEmpty(UserCredential?.Token?.RefreshToken))
            {
                if (forTesting)
                {
                    throw new InvalidOperationException("You must have a refresh token to unit test this");
                }
                return;
            }

            Task<UserInfo> infoTask = IsGoogle
                ? GetUserInfoGoogleAsync(forTesting)
                : m_Service == SecretsConfig.Service.Vive
                    ? GetUserInfoViverseAsync(forTesting)
                    : GetUserInfoSketchfabAsync(forTesting);

            Profile = await infoTask; // Triggers OnProfileUpdated event

            if (!forTesting)
            {
                ControllerConsoleScript.m_Instance.AddNewLine(Profile.name + " logged in.");
            }
        }

        private async Task<UserInfo> GetUserInfoSketchfabAsync(bool forTesting)
        {
            var sketchfabService = new SketchfabService(this);
            var ret = await sketchfabService.GetUserInfo();
            var profileUrl = ret.avatar.images
                .OrderByDescending(tup => tup.width)
                .First().url;
            return new UserInfo
            {
                email = ret.email,
                icon = await ImageUtils.DownloadTextureAsync(profileUrl),
                name = ret.displayName,
                id = ret.uid,
                isGoogle = false
            };
        }

        private async Task<UserInfo> GetUserInfoGoogleAsync(bool forTesting)
        {
            var peopleService = new PeopleServiceService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = UserCredential,
                ApplicationName = App.kGoogleServicesAppName,
            });
            var meRequest = peopleService.People.Get("people/me");
            meRequest.PersonFields = "names,emailAddresses,photos";
            var me = await meRequest.ExecuteAsync();

            UserInfo user = new UserInfo();
            user.id = me.ResourceName;
            user.name = me.Names.FirstOrDefault()?.DisplayName ?? "Unknown Name";
            user.email = me.EmailAddresses.FirstOrDefault(x => x.Metadata.Primary ?? false)?.Value ?? "";
            user.isGoogle = true;
            string iconUri = me.Photos.FirstOrDefault()?.Url;
            if (string.IsNullOrEmpty(iconUri))
            {
                Debug.LogException(new UserInfoError("Returned UserInfo contained no icon URI."));
            }
            else if (!forTesting)
            { // Not necessary yet when unit-testing
                user.icon = await ImageUtils.DownloadTextureAsync(SetImageUrlOptions(iconUri));
            }
            // Assume if the texture is 8x8 that the texture couldn't be decoded, and put our own in.
            // See b/62269743. Also use this texture if there is no user icon.
            if (user.icon == null || user.icon.width == 8 && user.icon.height == 8)
            {
                user.icon = m_LoggedInTexture;
            }
            return user;
        }

        private void OnDestroy()
        {
            if (m_ViverseAuthManager != null)
            {
                m_ViverseAuthManager.OnAuthComplete -= OnViverseAuthComplete;
                m_ViverseAuthManager.OnAuthError -= OnViverseAuthError;
            }
        }

    }

} // namespace TiltBrush
