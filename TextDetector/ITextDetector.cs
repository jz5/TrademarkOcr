using System.Collections.Generic;

namespace TextDetector
{
    public interface ITextDetector
    {
        bool TryDetectText(string url, out string resultText, out List<string> otherEstimatedText);
    }
}
