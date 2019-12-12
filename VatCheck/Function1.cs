using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace FunctionApp1
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static async Task Run(
            [BlobTrigger("nip-list/{name}", Connection = "")]Stream myBlob,
            string name,
            ILogger log,
            [Blob("nip-results/{name}", FileAccess.Write)]Stream outputBlob)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");

            var reader = new StreamReader(myBlob);
            var completed = 0;
            var successRequests = 0;
            var results = new List<string>();
            while (!reader.EndOfStream)
            {
                var nip = reader.ReadLine().Trim();
                var client = new RestClient($"https://wl-api.mf.gov.pl/api/search/nip/{nip}?date={DateTime.Now.Date.ToString("yyyy-MM-dd")}");
                var request = new RestRequest(Method.GET);
                var response = await client.ExecuteTaskAsync<RootObject>(request);
                var success = response.IsSuccessful && response.Data?.result?.subject != null;
                completed++;

                if (!success)
                {
                    results.Add($"{nip} error");
                }
                else
                {
                    successRequests++;
                    var obj = response.Data.result;
                    var status = obj.subject.statusVat;
                    var accountNumbers = string.Join(",", obj.subject.accountNumbers);
                    results.Add($"{nip} {status} {accountNumbers}");
                }

                log.LogInformation($"\r[*] STATS. Completed: {completed} Successfull: {successRequests}");
            }
            var writer = new StreamWriter(outputBlob);
            results.ForEach(r => writer.WriteLine(r));
            writer.Flush();
            log.LogInformation("Done!");
        }
    }

    public class Subject
    {
        public string name { get; set; }
        public string nip { get; set; }
        public string statusVat { get; set; }
        public string regon { get; set; }
        public string residenceAddress { get; set; }
        public string registrationLegalDate { get; set; }
        public List<string> accountNumbers { get; set; }
        public bool hasVirtualAccounts { get; set; }
    }

    public class Result
    {
        public Subject subject { get; set; }
        public string requestId { get; set; }
    }

    public class RootObject
    {
        public Result result { get; set; }
    }
}
