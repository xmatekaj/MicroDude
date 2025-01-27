using System;

namespace MicroDude
{
    /// <summary>
    /// Helper class that exposes all GUIDs used across VS Package.
    /// </summary>
    internal static class GuidsList
    {
        // Now define the list of guids as public static members.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        public static readonly Guid guidMicroDudePkg = new Guid("{3C7C5ABE-82AC-4A37-B077-0FF60E8B1FD3}");
        public const string guidMicroDudePkg_string = "3C7C5ABE-82AC-4A37-B077-0FF60E8B1FD3";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        public static readonly Guid guidGenericCmdBmp = new Guid("{0A4C51BD-3239-4370-8869-16E0AE8C0A46}");

        public const string MicroDudeString = "e2534787-33fb-458a-838a-f22015cfac7d";
        public static Guid MicroDude = new Guid(MicroDudeString);

        public const string guidMicroDudeCmdSetString = "c0246e5e-2d78-442d-8ded-229e9ca7c0c6";
        public static Guid guidMicroDudeCmdSet = new Guid(guidMicroDudeCmdSetString);

        public const string guidIconDetectString = "fcb34ff2-7b5b-4c64-a9ad-2342ad4cba51";
        public static Guid guidIconDetect = new Guid(guidIconDetectString);

        public const string guidIconFlashString = "c03d9bed-ea37-4992-b154-7434133b9856";
        public static Guid guidIconFlash = new Guid(guidIconFlashString);

        public const string guidIconFlashAutoString = "cf79f8cd-487c-4d26-8b63-8b45e20923c7";
        public static Guid guidIconFlashAuto = new Guid(guidIconFlashAutoString);

        public const string guidIconFlashAutoDesiabledString = "007801bf-1ef4-4848-8168-6872d3175a6a";
        public static Guid guidIconFlashAutoDisabled = new Guid(guidIconFlashAutoString);

        public const string guidIconVerifyString = "b2c74325-17a2-4b1e-8d21-9e7c88a6e438";
        public static Guid guidIconVerify = new Guid(guidIconVerifyString);

        public const string guidIconFuseString = "ebbeafdf-76bc-4e32-b34d-8d663c710117";
        public static Guid guidIconFuse = new Guid(guidIconFuseString);

        public const string guidIconOscillatorString = "764314b2-ba6d-4c6d-9d55-b0d44e566006";
        public static Guid guidIconOscillator = new Guid(guidIconOscillatorString);

        public const string guidIconLockBitsString = "a00e4112-e05e-4c1c-8f72-206a66c62bed";
        public static Guid guidIconLockBits = new Guid(guidIconLockBitsString);
        

        public const string guidIconMicroDudeSettingsString = "005090f0-7f61-469e-a19c-a511a91b7502";
        public static Guid guidIconMicroDudeSettings = new Guid(guidIconMicroDudeSettingsString);

        public const string guidIconSettingsString = "af5452c5-22b2-42e3-b2c0-c68a97ec9b14";
        public static Guid guidIconSettings = new Guid(guidIconSettingsString);

    }
}
