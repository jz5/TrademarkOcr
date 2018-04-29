using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions;
using System.Configuration;
using TextDetector;
using Microsoft.WindowsAzure.Storage;
using CoreTweet;
using CoreTweet.Core;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Text.RegularExpressions;

namespace OcrWebJob
{
    public class Functions
    {
        public static void ProcessMethod([TimerTrigger("00:05:00", RunOnStartup = true)] TimerInfo timerInfo)
        {
            var path = ConfigurationManager.AppSettings["GOOGLE_APPLICATION_CREDENTIALS"];
            var detector = new GoogleCloudVisionDetector(path);

            foreach (var s in GetStatuses())
            {
                try
                {
                    Update(detector, s);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex}");
                }
            }
        }

        private static CloudBlockBlob _blob;
        private static CloudBlockBlob GetBlob()
        {
            if (_blob != null) return _blob;

            var containerName = "ocr";
            var blobName = "id.txt";

            // create/get container
            var storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(containerName);
            container.CreateIfNotExists();

            _blob = container.GetBlockBlobReference(blobName);
            return _blob;
        }

        private static Tokens Token
        {
            get
            {
                var consumerKey = ConfigurationManager.AppSettings["TwitterConsumerKey"];
                var consumerSecret = ConfigurationManager.AppSettings["TwitterConsumerSecret"];
                var accessToken = ConfigurationManager.AppSettings["TwitterAccessToken"];
                var accessSecret = ConfigurationManager.AppSettings["TwitterAccessSecret"];

                return Tokens.Create(consumerKey, consumerSecret, accessToken, accessSecret);
            }
        }

        /// <summary>
        /// Get recently @trademark_bot's status
        /// </summary>
        /// <param name="sinceId"></param>
        /// <returns></returns>
        private static List<Status> GetStatuses(int takeCount = 50)
        {
            // Get since_id from Blob
            long sinceId = 990518563923513349;

            var blob = GetBlob();
            if (blob.Exists())
            {
                sinceId = Convert.ToInt64(blob.DownloadText());
            }

            // Get @trademark_bot timeline
            ListedResponse<Status> statuses;
            try
            {
                if (sinceId >= 0)
                {
                    statuses = Token.Statuses.UserTimeline(
                        screen_name => "trademark_bot",
                        since_id => sinceId,
                        count => 200,
                        tweet_mode => "extended");
                }
                else
                {
                    statuses = Token.Statuses.UserTimeline(
                        screen_name => "trademark_bot",
                        count => 1,
                        tweet_mode => "extended");
                }

                return statuses.OrderBy(i => i.Id).Take(takeCount).ToList();
            }
            catch (TwitterException ex)
            {
                Console.WriteLine($"{ex}");
                return new List<Status>();
            }
        }

        private static void Update(ITextDetector detector, Status s)
        {
            var blob = GetBlob();

            var url = s.ExtendedEntities?.Media?.FirstOrDefault()?.MediaUrlHttps;
            if (url == null)
            {
                blob.UploadText(s.Id.ToString());
                return;
            }

            //Console.WriteLine(s.Id);
            //Console.WriteLine(s.FullText);

            var statusUrl = $"https://twitter.com/trademark_bot/status/{s.Id}";

            if (detector.TryDetectText(url, out var responseText, out var otherText))
            {
                var number = "";
                var m = Regex.Match(s.FullText, @"\[商願(?<num>.+?)\]");
                if (m.Success)
                {
                    number = m.Groups["num"].Value;
                }

                try
                {
                    var messages = new List<string>() { "商願" + number, "🇬" + responseText };
                    if (otherText.Any())
                    {
                        otherText[0] = "📝" + otherText[0];
                        messages.AddRange(otherText);
                    }

                    //messages.Add(statusUrl);

                    Token.Statuses.Update(
                        status => string.Join("\n", messages)/*,
                            in_reply_to_status_id => s.id*/);

                }
                catch (TwitterException ex)
                {
                    Console.WriteLine($"Error: id = {s.Id}, Message ={ex.Message}");
                    return;
                }
            }
            else
            {
                Console.WriteLine($"Info: id = {s.Id}, no result");
            }

            blob.UploadText(s.Id.ToString());
        }
    }
}
