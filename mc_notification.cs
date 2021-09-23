using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;

namespace ktkr.Function
{
    public static class mc_notification
    {
        [FunctionName("mc_notification")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            // for non use keyvault
            //var ACCESS_TOKEN = "<Your LINE token>";

            // for usr key vault
            var ACCESS_TOKEN = Environment.GetEnvironmentVariable("LINEKEY");

            log.LogInformation("Minecraft Login/Logout notification");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            // format check
            if (data?.schemaId == null)
            {
                // illigal format
                log.LogError("illigal format");
                return new BadRequestResult();
            }
            else
            {
                if (data?.schemaId != "Microsoft.Insights/LogAlert")
                {
                    // illigal format
                    log.LogError("illigal format");
                    return new BadRequestResult();
                }
            }

            int count = data?.data.ResultCount;
            for (int i = 0; i < count; i++)
            {
                // localtime
                string tm = data?.data.SearchResult.tables[0].rows[i][0];

                // action
                string ac = data?.data.SearchResult.tables[0].rows[i][1]; // action
                if (ac == "connected")
                {
                    ac = "接続";
                }
                else if (ac == "disconnected")
                {
                    ac = "切断";
                }

                // user name
                string us = data?.data.SearchResult.tables[0].rows[i][2];

                // xuid
                string id = data?.data.SearchResult.tables[0].rows[i][3];

                // generate message
                //string msg = "["+ ac + "] "+ us + "(" + id + ")";
                string msg = "[" + ac + "] " + us;
                log.LogInformation(msg);

                var json = "{\"messages\":[{\"text\": \"" + msg + "\",\"type\": \"text\"}]}";
                using (var client = new HttpClient())
                {

                    // 通知するメッセージ
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    // ヘッダーにアクセストークンを追加
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ACCESS_TOKEN);
                    // 実行
                    var result = await client.PostAsync("https://api.line.me/v2/bot/message/broadcast", content);
                    //log.LogInformation(json);
                }
            }
            return new OkObjectResult("OK");
        }
    }
}
