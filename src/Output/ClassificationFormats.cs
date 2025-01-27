using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.Windows.Media;
using MicroDude.Properties;

namespace MicroDude.Output
{
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.BuildError)]
    [Name(ClassificationTypes.BuildError)]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    internal sealed class BuildErrorFormat : ClassificationFormatDefinition
    {
        public BuildErrorFormat()
        {
            ForegroundColor = (Color)ColorConverter.ConvertFromString(MicroDudeSettings.Default.ErrorColor);
            BackgroundOpacity = 0;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.BuildWarning)]
    [Name(ClassificationTypes.BuildWarning)]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    internal sealed class BuildWarningFormat : ClassificationFormatDefinition
    {
        public BuildWarningFormat()
        {
            ForegroundColor = (Color)ColorConverter.ConvertFromString(MicroDudeSettings.Default.WarningColor);
            BackgroundOpacity = 0;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.BuildProgress)]
    [Name(ClassificationTypes.BuildProgress)]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    internal sealed class BuildProgressFormat : ClassificationFormatDefinition
    {
        public BuildProgressFormat()
        {
            ForegroundColor = (Color)ColorConverter.ConvertFromString(MicroDudeSettings.Default.InfoColor);
            BackgroundOpacity = 0;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.BuildSuccess)]
    [Name(ClassificationTypes.BuildSuccess)]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    internal sealed class BuildSuccessFormat : ClassificationFormatDefinition
    {
        public BuildSuccessFormat()
        {
            ForegroundColor = (Color)ColorConverter.ConvertFromString(MicroDudeSettings.Default.SuccessColor);
            BackgroundOpacity = 0;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.MicroDude)]
    [Name(ClassificationTypes.MicroDude)]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    internal sealed class AvrCommandFormat : ClassificationFormatDefinition
    {
        public AvrCommandFormat()
        {
            ForegroundColor = (Color)ColorConverter.ConvertFromString(MicroDudeSettings.Default.MicroDudeColor);
            BackgroundOpacity = 0;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.Normal)]
    [Name(ClassificationTypes.Normal)]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    internal sealed class NormalFormat : ClassificationFormatDefinition
    {
        public NormalFormat()
        {
            ForegroundColor = (Color)ColorConverter.ConvertFromString(MicroDudeSettings.Default.InfoColor);
            BackgroundOpacity = 0;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = ClassificationTypes.BuildText)]
    [Name(ClassificationTypes.BuildText)]
    [UserVisible(true)]
    [Order(Before = Priority.Default)]
    internal sealed class BuildTextFormat : ClassificationFormatDefinition
    {
        public BuildTextFormat()
        {
            ForegroundColor = (Color)ColorConverter.ConvertFromString(MicroDudeSettings.Default.InfoColor);
            BackgroundOpacity = 0;
        }
    }
}