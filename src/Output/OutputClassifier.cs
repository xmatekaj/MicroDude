using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace MicroDude.Output
{
    internal class ClassifierRule
    {
        public Regex Pattern { get; set; }
        public string ClassificationType { get; set; }

        public ClassifierRule(Regex pattern, string classificationType)
        {
            Pattern = pattern;
            ClassificationType = classificationType;
        }
    }

    internal class OutputClassifier : IClassifier
    {
        private readonly IClassificationTypeRegistryService _classificationRegistry;
        private int _initialized;
        private readonly List<ClassifierRule> _classifiers;

        public OutputClassifier(IClassificationTypeRegistryService registry)
        {
            _classificationRegistry = registry;
            _classifiers = new List<ClassifierRule>();
            Initialize();
        }

        private void Initialize()
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 1) return;

            _classifiers.Clear();
            _classifiers.AddRange(new[]
            {
                new ClassifierRule(
                    new Regex(@"(?i)(MicroDude)", RegexOptions.Compiled),
                    ClassificationTypes.MicroDude),
                new ClassifierRule(
                    new Regex(@"(?i)error", RegexOptions.Compiled),
                    ClassificationTypes.BuildError),
                new ClassifierRule(
                    new Regex(@"(?i)warning", RegexOptions.Compiled),
                    ClassificationTypes.BuildWarning),
                new ClassifierRule(
                    new Regex(@"(?i)(Building|Compiling|Linking)", RegexOptions.Compiled),
                    ClassificationTypes.BuildProgress),
                new ClassifierRule(
                    new Regex(@"(?i)(succeeded|success)", RegexOptions.Compiled),
                    ClassificationTypes.BuildSuccess)
            });

            Logger.Log("OutputClassifier initialized with patterns");
        }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            var spans = new List<ClassificationSpan>();

            try
            {
                if (span.Length == 0) return spans;

                var line = span.Snapshot.GetLineFromPosition(span.Start);
                var lineText = line.GetText();

                foreach (var classifier in _classifiers)
                {
                    if (classifier.Pattern.IsMatch(lineText))
                    {
                        var classificationType = _classificationRegistry.GetClassificationType(classifier.ClassificationType);
                        spans.Add(new ClassificationSpan(line.Extent, classificationType));
                        Logger.Log($"Classified line as {classifier.ClassificationType}: {lineText.Trim()}");
                        return spans;
                    }
                }

                // Default classification
                var defaultType = _classificationRegistry.GetClassificationType(ClassificationTypes.BuildText);
                spans.Add(new ClassificationSpan(line.Extent, defaultType));
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in GetClassificationSpans: {ex}");
            }

            return spans;
        }

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        protected virtual void OnClassificationChanged(ClassificationChangedEventArgs e)
        {
            ClassificationChanged?.Invoke(this, e);
        }
    }
}