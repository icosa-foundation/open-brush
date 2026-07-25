namespace TiltBrush
{
    public static class ViverseEndpoints
    {
        // --- Identity & Auth ---
        // Updated to fetch dynamically from SecretsConfig via the Config singleton
        public static string CLIENT_ID => Config.m_SingletonState?.ViveSecrets?.ClientId;
        public const string AUTH_BASE_URL = "https://account.htcvive.com";
        public const string AUTH_LOGOUT_URL = AUTH_BASE_URL + "/logout";
        public const string REDIRECT_URL = "https://www.viverse.com/";

        // --- SDK & Profile ---
        public const string SDK_API_BASE = "https://sdk-api.viverse.com";
        // Note: SDK usually expects base without trailing slash, but UnityWebRequest needs full paths
        public const string PROFILE_INFO = SDK_API_BASE + "/api/meetingareaselector/v2/newgenavatar/sdk/me";
        public const string AVATAR_LIST = SDK_API_BASE + "/api/meetingareaselector/v1/newgenavatar/getavatarlist";

        // --- World Publishing (CMS) ---
        public const string WORLD_API_BASE = "https://world-api.viverse.com/api/hubs-cms/v1/standalone";
        public const string WORLD_CREATE = WORLD_API_BASE + "/contents";
        public const string WORLD_UPLOAD_FORMAT = WORLD_API_BASE + "/contents/{0}/upload";

        // --- Studio (User Facing) ---
        public const string STUDIO_UPLOAD_REDIRECT = "https://studio.viverse.com/upload";

        // NEW: URL format to open the published world directly. 
        // Usage: string.Format(ViverseEndpoints.WORLD_VIEW_FORMAT, hubSid);
        public const string WORLD_VIEW_FORMAT = "https://worlds.viverse.com/{0}";
    }
}
