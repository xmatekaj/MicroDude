using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace MicroDude.Output
{
    public static class ClassificationTypes
    {
        public const string BuildText = "MicroDude.BuildText";
        public const string BuildError = "MicroDude.BuildError";
        public const string BuildWarning = "MicroDude.BuildWarning";
        public const string BuildProgress = "MicroDude.BuildProgress";
        public const string BuildSuccess = "MicroDude.BuildSuccess";
        public const string MicroDude = "MicroDude.MicroDude";
        public const string Normal = "MicroDude.Normal";

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(BuildText)]
        internal static ClassificationTypeDefinition BuildTextDefinition;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(BuildError)]
        internal static ClassificationTypeDefinition BuildErrorDefinition;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(BuildWarning)]
        internal static ClassificationTypeDefinition BuildWarningDefinition;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(BuildProgress)]
        internal static ClassificationTypeDefinition BuildProgressDefinition;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(BuildSuccess)]
        internal static ClassificationTypeDefinition BuildSuccessDefinition;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(MicroDude)]
        internal static ClassificationTypeDefinition MicroDudeCommandDefinition;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(Normal)]
        internal static ClassificationTypeDefinition NormalCommandDefinition;
    }
}