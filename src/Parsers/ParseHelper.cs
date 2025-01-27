using MicroDude.Models;
using MicroDude.Services;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace MicroDude.Parsers
{
    class ParseHelper
    {
        private const string DeviceSignaturePattern = @"Device signature\s*=\s*([0-9A-Fa-f]{2})\s*([0-9A-Fa-f]{2})\s*([0-9A-Fa-f]{2})";
        private const string SignaturePattern = @"signature\s*=\s*0x([0-9A-Fa-f]{2})\s*0x([0-9A-Fa-f]{2})\s*0x([0-9A-Fa-f]{2})";

        /// <summary>
        /// Extract signature from the AvrDude output
        /// </summary>
        /// <param name="output"></param>
        /// <returns>string in hexadecimal format</returns>
        public static string ExtractSignature(string output)
        {
            // First, try to match the "Device signature" format
            var deviceSignatureMatch = Regex.Match(output, DeviceSignaturePattern);
            if (deviceSignatureMatch.Success)
            {
                return deviceSignatureMatch.Groups[1].Value +
                       deviceSignatureMatch.Groups[2].Value +
                       deviceSignatureMatch.Groups[3].Value;
            }

            // If that doesn't match, try the original "signature" format
            var signatureMatch = Regex.Match(output, SignaturePattern);
            if (signatureMatch.Success)
            {
                return signatureMatch.Groups[1].Value +
                       signatureMatch.Groups[2].Value +
                       signatureMatch.Groups[3].Value;
            }

            // If neither format matches, return null
            return null;
        }

        /// <summary>
        /// Get a microcontroller with given signature
        /// </summary>
        /// <param name="signature"></param>
        /// <returns>Microcontroller object</returns>
        public static Microcontroller FindMicrocontrollerBySignature(string signature)
        {
            return AvrdudeConfigService.Instance.Microcontrollers.FirstOrDefault(m => m.Signature.Equals(signature, StringComparison.OrdinalIgnoreCase));
        }
    }
}
