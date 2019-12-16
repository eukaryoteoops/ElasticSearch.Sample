using System;
using System.IO;
using System.Linq;
using Elasticsearch.Net;
using Nest;
using Newtonsoft.Json;
using Serilog;
using Serilog.Filters;
using Serilog.Formatting.Elasticsearch;
using Serilog.Sinks.Elasticsearch;

namespace ElasticSearch.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            // serilog integrate with elasticsearch
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.FromLogContext()
                .Filter.ByExcluding(Matching.FromSource("Microsoft"))
                .WriteTo.RollingFile($"{Directory.GetCurrentDirectory()}/Log/log-{{Date}}.txt")
                .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
                {
                    AutoRegisterTemplate = true,
                    AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv6,
                    IndexFormat = "serilog-{0:yyyyMMdd}",
                    TypeName = "sample"
                })
                .WriteTo.Console(new ElasticsearchJsonFormatter())
                .CreateLogger();
            Log.Debug($"write at {DateTime.UtcNow.ToString("yyyyMMdd HHmmss")}");

            // Nest operation
            var client = new ElasticClient(new ConnectionSettings(new Uri("http://localhost:9200"))
                    .DefaultIndex("test_index")
                    .DefaultMappingFor<Tweet>(m => m.IndexName("test_index_tweet")));

            var tweet = new Tweet
            {
                //Id = 1,
                User = "kimchy",
                PostDate = DateTime.Now,
                Message = "Trying out NEST, so far so good?"
            };

            InsertData(client, tweet);
            var result = SearchData<Tweet>(client, s => s
                                                .From(0)
                                                .Size(10)
                                                .Query(q =>
                                                    q.Term(t => t.Field(f => f.Id).Value(1)) ||
                                                    q.Match(m => m.Field(f => f.Message).Query("good"))));
            result.Documents.ToList().ForEach(o => Console.WriteLine(JsonConvert.SerializeObject(o)));

            Console.ReadKey();
        }

        /// <summary>
        /// Search data
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="client"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        private static ISearchResponse<T> SearchData<T>(ElasticClient client, Func<SearchDescriptor<T>, ISearchRequest> func) where T : class
        {
            return client.Search<T>(func);
        }

        /// <summary>
        ///  v7.0 remove types [https://www.elastic.co/guide/en/elasticsearch/reference/master/removal-of-types.html]
        /// Insert data   
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="client"></param>
        /// <param name="tweet"></param>
        private static void InsertData<T>(ElasticClient client, T tweet) where T : class
        {
            var id = Guid.NewGuid();
            client.Index(tweet, des => des
                            .Id(id) //define id 
                            .OpType(OpType.Create));//put if absent
        }
    }

    internal class Tweet
    {
        public int Id { get; set; }
        public string User { get; set; }
        public DateTime PostDate { get; set; }
        public string Message { get; set; }
    }
}
