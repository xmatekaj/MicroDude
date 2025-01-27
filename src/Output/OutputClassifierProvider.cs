using System;
using System.ComponentModel.Composition;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace MicroDude.Output
{
    [Export(typeof(IClassifierProvider))]
    [ContentType("output")]
    internal class OutputClassifierProvider : IClassifierProvider
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry = null;

        private static OutputClassifier _classifier;
        private ITextBuffer _buffer;
        public void ResetClassifier()
        {
            _classifier = null;
            GetClassifier(_buffer);
        }

        public IClassifier GetClassifier(ITextBuffer buffer)
        {
            try
            {
                if (_classifier == null)
                {
                    Interlocked.CompareExchange(
                        ref _classifier,
                        new OutputClassifier(ClassificationRegistry),
                        null);
                }
                _buffer = buffer;
                return _classifier;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in GetClassifier: {ex}");
                throw;
            }
        }
    }
}