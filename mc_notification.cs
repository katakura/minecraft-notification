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
using Microsoft.Azure.Cosmos.Table;

namespace ktkr.Function
{
    public class CustomerEntity : TableEntity
    {
        public CustomerEntity() { }
        public CustomerEntity(string xuid)
        {
            PartitionKey = "1";
            RowKey = xuid;
        }
        public string Name { get; set; }

    }

    public class WorldEntity : TableEntity
    {
        public WorldEntity() { }
        public WorldEntity(string rowkey)
        {
            PartitionKey = "1";
            RowKey = rowkey;
        }
        public string Name { get; set; }

    }

    public static class mc_notification
    {
        static string ACCESS_TOKEN;
        static string STORAGE_KEY;

        [FunctionName("mc_notification")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            // get access token
            ACCESS_TOKEN = Environment.GetEnvironmentVariable("LINEKEY");
            if (ACCESS_TOKEN == null)
            {
                log.LogError("Can't get Access token");
                return new BadRequestResult();
            }
            // get storage account key
            STORAGE_KEY = Environment.GetEnvironmentVariable("StorageConnectionString");
            if (STORAGE_KEY == null)
            {
                log.LogError("Can't get storage account string");
                return new BadRequestResult();
            }
            var storageAccount = CloudStorageAccount.Parse(STORAGE_KEY);
            var tableClient = storageAccount.CreateCloudTableClient();

            var userTable = tableClient.GetTableReference("users");
            var worldTable = tableClient.GetTableReference("worlds");

            log.LogInformation("Minecraft Login/Logout notification");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            //log.LogInformation(requestBody);

            // format check
            if (data?.schemaId != "Microsoft.Insights/LogAlert")
            {
                // illigal format
                log.LogError("illigal format");
                return new BadRequestResult();
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


                // xuid
                string id = data?.data.SearchResult.tables[0].rows[i][3];

                // user name
                string us = data?.data.SearchResult.tables[0].rows[i][2];
                var entity = new CustomerEntity(id);
                var tableResult = await userTable.ExecuteAsync(TableOperation.Retrieve<CustomerEntity>(entity.PartitionKey, entity.RowKey));
                var usersresult = tableResult.Result as CustomerEntity;
                if (usersresult != null)
                {
                    us = usersresult.Name;
                }

                // world name
                string wn = data?.data.SearchResult.tables[0].rows[i][4];
                var wnentity = new WorldEntity(wn);
                var wntableResult = await worldTable.ExecuteAsync(TableOperation.Retrieve<WorldEntity>(wnentity.PartitionKey, wnentity.RowKey));
                var worldresult = wntableResult.Result as WorldEntity;
                if (worldresult != null)
                {
                    wn = worldresult.Name;
                }

                // generate message
                //string msg = "["+ ac + "] "+ us + "(" + id + ")";
                string msg1 = "[" + ac + "]";
                string msg2 = "Name : " + us;
                string msg3 = "World: " + wn;

                //var json = "{\"messages\":[{\"text\": \"" + msg1 + "\",\"type\": \"text\"},{\"text\": \"" + msg2 + "\",\"type\": \"text\"},{\"text\": \"" + msg3 + "\",\"type\": \"text\"}]}";
                var json = "{\"messages\":[{\"text\": \"" + msg1 + "\\n" + msg2 + "\\n" + msg3 + "\",\"type\": \"text\"}]}";
                log.LogInformation(json);

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
