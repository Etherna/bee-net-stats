using CsvHelper;
using Etherna.BeeNet.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Etherna.BeeNetStats
{
    public static class Program
    {
        public const string CsvOutputFileName = "output.csv";
        public const int DefaultIterations = 10;

        public static readonly (int, string)[] TestFilesSize =
        [
            (1024 * 1024 * 100,                       "100MB"),
            (1024 * 1024 * 200,                       "200MB"),
            (1024 * 1024 * 500,                       "500MB"),
            (1024 * 1024 * 1024,                      "1GB"),
            (Math.Min(int.MaxValue, Array.MaxLength), "2GB")
        ];

        public static readonly ushort[] TestCompactLevel =
        [
            0, 1, 2, 5, 10, 20, 50, 100, 200, 500,
            // 1000, 2000, 5000, 10000, 20000, 50000
        ];
        
        static async Task Main(string[] args)
        {
            // Run with several iteration if required to output stats.
            // Otherwise, run only once for demo.
            var iterations = args.Length == 0 ? DefaultIterations : int.Parse(args[0], CultureInfo.InvariantCulture);
            
            // Create CSV file.
            using StreamWriter writer = new StreamWriter(CsvOutputFileName);
            using var csv = new CsvWriter(writer, CultureInfo.GetCultureInfo("it-it"));
            csv.WriteHeader<OutputCsvRecord>();
            await csv.NextRecordAsync();
            
            // Start test timer.
            var testStartTime = DateTimeOffset.Now;

            // Run tests.
            for (int i = 0; i < TestFilesSize.Length; i++)
            for (int j = 0; j < TestCompactLevel.Length; j++)
            {
                var fileSize = TestFilesSize[i].Item1;
                var compactLevel = TestCompactLevel[j];
                    
                var totalDepth = 0;
                var totalTime = new TimeSpan();
                var totalBucketsPerCollision = new List<int>();
                var totalMissedOptimisticHashing = 0L;

                for (int k = 0; k < iterations; k++)
                {
                    Console.WriteLine($"Testing {TestFilesSize[i].Item2} random data, compactLevel {compactLevel}, iteration {k}");
                
                    // Generate random file (in memory).
                    Console.Write("Generating random data...");
                    var data = RandomNumberGenerator.GetBytes(fileSize);
                    Console.WriteLine(" Done.");
                        
                    // Run test.
                    var result = await RunTestAsync(data, compactLevel);
                    
                    // Report results.
                    totalDepth += result.UploadResult.PostageStampIssuer.Buckets.RequiredPostageBatchDepth;
                    totalTime += result.Duration;
                    totalMissedOptimisticHashing += result.UploadResult.MissedOptimisticHashing;
                    
                    var resultBucketsPerCollision = result.UploadResult.PostageStampIssuer.Buckets.CountBucketsByCollisions();
                    while (totalBucketsPerCollision.Count < resultBucketsPerCollision.Length)
                        totalBucketsPerCollision.Add(0);
                    for (int l = 0; l < resultBucketsPerCollision.Length; l++)
                        totalBucketsPerCollision[l] += resultBucketsPerCollision[l];
                    
                    // Print result.
                    Console.WriteLine($"Process took {result.Duration.TotalSeconds} seconds");
                    Console.WriteLine(
                        $"Required depth: {result.UploadResult.PostageStampIssuer.Buckets.RequiredPostageBatchDepth}");
                    Console.WriteLine($"Missed optimistic hashing: {
                        result.UploadResult.MissedOptimisticHashing}");
                    Console.WriteLine($"Amount buckets per collision:");
                    for (int l = 0; l < resultBucketsPerCollision.Length; l++)
                        Console.WriteLine($"  [{l}] = {resultBucketsPerCollision[l]}");
                
                    Console.WriteLine("-----");
                }

                var avgDepth = (double)totalDepth / iterations;
                var avgTime = totalTime / iterations;

                // Write record to CSV.
                csv.WriteRecord(new OutputCsvRecord(
                    avgDepth: avgDepth,
                    avgSeconds: avgTime.TotalSeconds,
                    compactLevel: TestCompactLevel[j],
                    sourceFileSize: TestFilesSize[i].Item2));
                await csv.NextRecordAsync();
                await writer.FlushAsync();
                
                Console.WriteLine();
                Console.WriteLine($"  Completed test with {TestFilesSize[i].Item2} random data, compactLevel {compactLevel}");
                Console.WriteLine($"  Average required depth: {avgDepth}");
                Console.WriteLine($"  Average duration: {avgTime.TotalSeconds} seconds");
                Console.WriteLine($"  Average missed optimistic hashing: {
                    (double)totalMissedOptimisticHashing / iterations}");
                Console.WriteLine( "  Average amount buckets per collision:");
                for (int l = 0; l < totalBucketsPerCollision.Count; l++)
                    Console.WriteLine($"    [{l}] = {(double)totalBucketsPerCollision[l] / iterations}");
                
                Console.WriteLine();
                Console.WriteLine("*************");
                Console.WriteLine();
                Console.WriteLine();
            }
            
            // Print test end.
            var testDuration = DateTimeOffset.Now - testStartTime;
            Console.WriteLine();
            Console.WriteLine($"Completed. Test duration {testDuration}");
        }

        private static async Task<(UploadEvaluationResult UploadResult, TimeSpan Duration)> RunTestAsync(
            byte[] data,
            ushort compactLevel)
        {   
            var start = DateTime.UtcNow;
        
            var fileService = new CalculatorService();
            var result = await fileService.EvaluateFileUploadAsync(
                data,
                "text/plain",
                "testFile.txt",
                compactLevel: compactLevel);
        
            var duration = DateTime.UtcNow - start;

            return (result, duration);
        }
    }
}