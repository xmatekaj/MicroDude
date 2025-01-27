using MicroDude.Core;
using MicroDude.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroDude.Parsers
{
    class AvrDudeOutputParser
    {
        internal static void ParseSignatureOutput(AvrDudeResult result)
        {
            string signature = result.Success ? ParseHelper.ExtractSignature(result.Output) : ParseHelper.ExtractSignature(result.Error);
            if (!String.IsNullOrEmpty(signature))
            {
                Microcontroller detectedMcu = ParseHelper.FindMicrocontrollerBySignature(signature);

                if (detectedMcu != null)
                {
                    OutputPaneHandler.PrintTextToOutputPane($"Detected microcontroller: {detectedMcu.Description} ({detectedMcu.Id}) Signature: {signature}");
                }
                else
                {
                    OutputPaneHandler.PrintTextToOutputPane($"Unknown microcontroller detected.\n\nSignature: {signature}");
                }
            }
            else
            {
                OutputPaneHandler.PrintTextToOutputPane($"Cannot extract the signature from the output\n\nFull command:\n{result.Command}\n\nFull output:\n{result.Output}\n\nFull error:\n{result.Error}");
            }
        }
    }
}
