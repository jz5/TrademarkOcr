using Google.Apis.Auth.OAuth2;
using Google.Cloud.Vision.V1;
using Grpc.Auth;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TextDetector
{
    public class GoogleCloudVisionDetector : ITextDetector
    {
        private class MySymbol
        {
            public string Text { get; set; }
            public int SpaceWidth { get; set; }
            public TextAnnotation.Types.DetectedBreak.Types.BreakType? BreakType { get; set; }
        }

        private readonly string _googleCredentialFilePath = null;

        public GoogleCloudVisionDetector()
        {
        }

        public GoogleCloudVisionDetector(string googleCredentialFilePath)
        {
            _googleCredentialFilePath = googleCredentialFilePath;
        }

        public bool TryDetectText(string url, out string resultText, out List<string> otherEstimatedText)
        {
            try
            {
                return TryDetectText(Image.FromUri(url), out resultText, out otherEstimatedText);
            }
            catch (InvalidOperationException invalidOpEx)
            {
                // Maybe Credentials error
                Console.WriteLine(invalidOpEx.Message);
                throw;
            }
            catch (AnnotateImageException annotateImageEx)
            {
                if (annotateImageEx.Response.Error.Code == 4 ||
                    annotateImageEx.Response.Error.Code == 14)
                {
                    // Message: We can not access the URL currently. Please download the content and pass it in.
                    // Goto retry
                }
                else
                {
                    Console.WriteLine(annotateImageEx.Message);
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }

            // retry
            try
            {
                var filename = "imagefile";
                using (var client = new WebClient())
                {
                    client.DownloadFile(url, filename);
                    return TryDetectText(Image.FromFile(filename), out resultText, out otherEstimatedText);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        private bool TryDetectText(Image image, out string resultText, out List<string> otherText)
        {
            resultText = null;
            otherText = new List<string>();

            // Create ImageAnnotatorClient
            ImageAnnotatorClient client;
            if (_googleCredentialFilePath != null)
            {
                var googleCredential = GoogleCredential.FromStream(System.IO.File.OpenRead(_googleCredentialFilePath));
                var channel = new Grpc.Core.Channel(ImageAnnotatorClient.DefaultEndpoint.Host, googleCredential.ToChannelCredentials());
                client = ImageAnnotatorClient.Create(channel);
            }
            else
            {
                client = ImageAnnotatorClient.Create();
            }

            //
            var context = new ImageContext();
            context.LanguageHints.Add("ja");
            context.LanguageHints.Add("en");

            // Detect text
            var response = client.DetectDocumentText(image, context);
            if (response == null || string.IsNullOrWhiteSpace(response.Text))
                return false;

            resultText = response.Text.Trim();

            // Remove spaces and Replace characters.
            var lines = resultText.Split(new[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var page in response.Pages)
            {
                foreach (var block in page.Blocks)
                {
                    foreach (var paragraph in block.Paragraphs)
                    {
                        // Create MySymbol list
                        var list = new List<MySymbol>();

                        var symbols = paragraph.Words.SelectMany(w => w.Symbols).ToList();
                        for (var i = 0; i < symbols.Count - 1; i++)
                        {
                            var s = new MySymbol
                            {
                                Text = symbols[i].Text,
                                SpaceWidth = symbols[i + 1].BoundingBox.Vertices.Min(v => v.X)
                                        - symbols[i].BoundingBox.Vertices.Max(v => v.X),
                                BreakType = symbols[i].Property?.DetectedBreak?.Type
                            };

                            list.Add(s);
                        }
                        list.Add(new MySymbol
                        {
                            Text = symbols.Last().Text
                        });

                        if (list.All(i => i.BreakType == null))
                        {
                            continue;
                        }

                        // Remove spaces
                        var median = list.Where(i => i.BreakType == TextAnnotation.Types.DetectedBreak.Types.BreakType.Space).Select(i => i.SpaceWidth)?.Median();
                        var builder = new StringBuilder();
                        foreach (var s in list)
                        {
                            builder.Append(s.Text);

                            if (median.HasValue &&
                                s.BreakType != null &&
                                s.SpaceWidth > median * 1.5)
                                builder.Append(" ");

                            Debug.WriteLine($"{s.Text}: {s.SpaceWidth}");
                        }

                        var text = builder.ToString().Trim();
                        Debug.WriteLine(text);
                        if (!lines.Contains(text))
                            otherText.Add(text);

                        // 1 -> l
                        if (text.Contains("1"))
                        {
                            var text2 = text.Replace("1", "l");
                            if (!lines.Contains(text2))
                                otherText.Add(text2);
                        }
                    }
                }
            }
            return true;
        }
    }

}
