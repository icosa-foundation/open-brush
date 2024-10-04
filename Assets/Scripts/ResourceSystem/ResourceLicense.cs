using System;
namespace TiltBrush
{
    public class ResourceLicense
    {
        public enum DerivativePermission
        {
            NoDerivatives,
            SameLicense,
            NoAdditionalRestrictions,
            NoRestrictions,
        }

        public string ShortName { get; private set; }
        public string LongName { get; private set; }
        public string Url { get; private set; }
        public DerivativePermission Derivatives { get; private set; }
        public bool Shareable { get; private set; }
        public bool Attribution { get; private set; }
        public bool Commercial { get; private set; }
        public bool AllRightsReserved { get; private set; }

        public ResourceLicense CC_BY = new ResourceLicense
        {
            ShortName = "CC BY 4.0",
            LongName = "Creative Commons Attribution 4.0 International",
            Url = "https://creativecommons.org/licenses/by/4.0/",
            Shareable = true,
            Derivatives = DerivativePermission.NoAdditionalRestrictions,
            Attribution = true,
            Commercial = true,
            AllRightsReserved = false,
        };

        public ResourceLicense CC_BY_SA = new ResourceLicense
        {
            ShortName = "CC BY-SA 4.0",
            LongName = "Creative Commons Attribution-ShareAlike 4.0 International",
            Url = "https://creativecommons.org/licenses/by-sa/4.0/",
            Shareable = true,
            Derivatives = DerivativePermission.SameLicense,
            Attribution = true,
            Commercial = true,
            AllRightsReserved = false,
        };

        public ResourceLicense CC_BY_ND = new ResourceLicense
        {
            ShortName = "CC BY-ND 4.0",
            LongName = "Creative Commons Attribution-NoDerivatives 4.0 International",
            Url = "https://creativecommons.org/licenses/by-nd/4.0/",
            Shareable = true,
            Derivatives = DerivativePermission.NoDerivatives,
            Attribution = true,
            Commercial = true,
            AllRightsReserved = false,
        };

        public ResourceLicense CC_BY_NC = new ResourceLicense
        {
            ShortName = "CC BY-NC 4.0",
            LongName = "Creative Commons Attribution-NonCommercial 4.0 International",
            Url = "https://creativecommons.org/licenses/by-nc/4.0/",
            Shareable = true,
            Derivatives = DerivativePermission.NoAdditionalRestrictions,
            Attribution = true,
            Commercial = false,
            AllRightsReserved = false,
        };

        public ResourceLicense CC_BY_NC_SA = new ResourceLicense
        {
            ShortName = "CC BY-NC-SA 4.0",
            LongName = "Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International",
            Url = "https://creativecommons.org/licenses/by-nc-sa/4.0/",
            Shareable = true,
            Derivatives = DerivativePermission.SameLicense,
            Attribution = true,
            Commercial = false,
            AllRightsReserved = false,
        };

        public ResourceLicense CC_BY_NC_ND = new ResourceLicense
        {
            ShortName = "CC BY-NC-ND 4.0",
            LongName = "Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International",
            Url = "https://creativecommons.org/licenses/by-nc-nd/4.0/",
            Shareable = true,
            Derivatives = DerivativePermission.NoDerivatives,
            Attribution = true,
            Commercial = false,
            AllRightsReserved = false,
        };

        public ResourceLicense ALL_RIGHTS_RESERVED = new ResourceLicense
        {
            ShortName = "ALL RIGHTS RESERVED",
            LongName = "All Rights Reserved",
            Url = "",
            Shareable = false,
            Derivatives = DerivativePermission.NoDerivatives,
            Attribution = true,
            Commercial = false,
            AllRightsReserved = true,
        };
    }
}
