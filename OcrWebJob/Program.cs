using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace OcrWebJob
{
    // To learn more about Microsoft Azure WebJobs SDK, please see https://go.microsoft.com/fwlink/?LinkID=320976
    class Program
    {
        // Please set the following connection strings in app.config for this WebJob to run:
        // AzureWebJobsDashboard and AzureWebJobsStorage
        static void Main()
        {
            var config = new JobHostConfiguration();

            if (config.IsDevelopment)
            {
                config.UseDevelopmentSettings();
            }

            config.Queues.BatchSize = 1;
            config.UseCore();
            config.UseTimers();

            var host = new JobHost(config);

            // https://github.com/Azure/azure-webjobs-sdk/wiki/ServicePointManager-settings-for-WebJobs
            // Set this immediately so that it is used by all requests.
            ServicePointManager.DefaultConnectionLimit = Int32.MaxValue;

            // The following code ensures that the WebJob will be running continuously
            host.RunAndBlock();
        }
    }
}
