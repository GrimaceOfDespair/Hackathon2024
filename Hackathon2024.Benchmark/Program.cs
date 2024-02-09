using System.Text;

namespace Hackathon2024.Benchmark
{
    using Azure.Storage.Blobs;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Running;
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Data = System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, object>[]>;


    [MemoryDiagnoser]
    [JsonExporter(indentJson: true, excludeMeasurements: true)]
    public class HackathonBenchmark
    {
        private TemplateRenderer _renderer;
        private StringReader _template;
        private Data _data;

        [GlobalSetup]
        public void Init()
        {
            _renderer = new TemplateRenderer();
            _template = new StringReader(File.ReadAllText("template.html"));
            _data = JsonSerializer.Deserialize<Data>(File.ReadAllText("data.json"));
        }

        [Benchmark]
        public void TemplateRenderer_DefaultRender()
        {
            new TemplateRenderer()
                .RenderTemplate(_template, TextWriter.Null, _data);
        }
    }
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<HackathonBenchmark>();

            await using var reportJson = File.OpenRead("BenchmarkDotNet.Artifacts/results/Hackathon2024.Benchmark.HackathonBenchmark-report.json");
            var report = JsonSerializer.Deserialize<BenchmarkReport>(reportJson);

            if (report.Benchmarks.Length == 0)
            {
                await Console.Error.WriteLineAsync("No benchmarks detected");
                return;
            }

            if (args.Length == 2)
            {
                string input = args[0];
                StringBuilder sanitizedTeamName = new(input.Length);

                foreach (char character in input)
                {
                    if (char.IsLetterOrDigit(character) || character == '_' || character == '-')
                    {
                        sanitizedTeamName.Append(character);
                    }
                }

                string team = sanitizedTeamName.ToString();
                var result = report.Benchmarks[0].Statistics.Percentiles.P95;

                var teamData = JsonSerializer.Serialize(
                    new
                    {
                        team,
                        result
                    },
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                    });

                var sasUrl = new Uri(args[1]);

                Console.WriteLine($"Publishing results for {team}:");
                Console.WriteLine(teamData);

                await new BlobContainerClient(sasUrl)
                    .GetBlobClient($"{team}.json")
                    .UploadAsync(BinaryData.FromString(teamData), overwrite: true);
            }
        }
    }
}
