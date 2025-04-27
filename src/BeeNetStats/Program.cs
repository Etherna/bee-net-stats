using CsvHelper;
using Etherna.BeeNet.Hashing;
using Etherna.BeeNet.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Etherna.BeeNetStats
{
    internal static class Program
    {
        public const string CsvOutputFileName = "output.csv";
        public const int DefaultIterations = 10;

        public static readonly (int, string)[] TestFilesSizes =
        [
            (1024 * 1024 * 100, "100MB"),
            (1024 * 1024 * 200, "200MB"),
            (1024 * 1024 * 500, "500MB"),
            // (1024 * 1024 * 501,                       "501MB"),
            // (1024 * 1024 * 502,                       "502MB"),
            // (1024 * 1024 * 503,                       "503MB"),
            // (1024 * 1024 * 504,                       "504MB"),
            (1024 * 1024 * 1024, "1GB"),
            (Math.Min(int.MaxValue, Array.MaxLength), "2GB")
        ];

        public static readonly ushort[] TestCompactLevels =
        [
            0, 1, 2, 4, 8, 16, 32, 64, 128, 256,
            512, 1024, 2048, 4096, 8192, 16384, 32768, 65535
        ];

        static async Task Main(string[] args)
        {
            // Run with several iteration if required to output stats.
            // Otherwise, run only once for demo.
            var iterations = args.Length == 0 ? DefaultIterations : int.Parse(args[0], CultureInfo.InvariantCulture);

            // Create CSV file.
            using StreamWriter writer = new StreamWriter(CsvOutputFileName);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteHeader<OutputCsvRecord>();
            await csv.NextRecordAsync();

            // Start test timer.
            var testStartTime = DateTimeOffset.Now;

            // Run tests.
            for (int i = 0; i < TestFilesSizes.Length; i++)
                foreach (var compactLevel in TestCompactLevels)
                {
                    var fileSize = TestFilesSizes[i].Item1;

                    var totalChunks = 0L;
                    var totalDepth = 0;
                    var totalTime = TimeSpan.Zero;
                    var totalBucketsPerCollision = new List<int>();
                    var totalMissedOptimisticHashing = 0L;

                    for (int k = 0; k < iterations; k++)
                    {
                        Console.WriteLine($"Testing {TestFilesSizes[i].Item2} random data, compactLevel {compactLevel}, iteration {k}");

                        // Generate random file (in memory).
                        Console.Write("Generating random data...");
                        var data = RandomNumberGenerator.GetBytes(fileSize);
                        Console.WriteLine(" Done.");

                        // Run test.
                        Console.Write("Chunking data with manifest...");
                        var result = await RunTestAsync(data, compactLevel);
                        Console.WriteLine(" Done.");

                        // Report results.
                        if (totalChunks != 0 &&
                            totalChunks != result.UploadResult.PostageStampIssuer.Buckets.TotalChunks)
                            throw new InvalidOperationException("Total chunks has unexpected value");
                        totalChunks = result.UploadResult.PostageStampIssuer.Buckets.TotalChunks;
                        totalDepth += result.UploadResult.PostageStampIssuer.Buckets.RequiredPostageBatchDepth;
                        totalTime += result.Duration;
                        totalMissedOptimisticHashing += result.UploadResult.MissedOptimisticHashing;

                        var resultBucketsPerCollision = result.UploadResult.PostageStampIssuer.Buckets.CountBucketsByCollisions();
                        while (totalBucketsPerCollision.Count < resultBucketsPerCollision.Length)
                            totalBucketsPerCollision.Add(0);
                        for (int l = 0; l < resultBucketsPerCollision.Length; l++)
                            totalBucketsPerCollision[l] += resultBucketsPerCollision[l];

                        // Print result.
                        Console.WriteLine($"Chunking took {result.Duration.TotalSeconds} seconds");
                        Console.WriteLine(
                            $"Required depth: {result.UploadResult.PostageStampIssuer.Buckets.RequiredPostageBatchDepth}");
                        Console.WriteLine($"Missed optimistic hashing: {
                            result.UploadResult.MissedOptimisticHashing}");
                        Console.WriteLine($"Buckets by total collisions:");
                        for (int l = 0; l < resultBucketsPerCollision.Length; l++)
                            Console.WriteLine($"  [{l}] = {resultBucketsPerCollision[l]}");
                        Console.WriteLine($"Total chunks: {result.UploadResult.PostageStampIssuer.Buckets.TotalChunks}");

                        Console.WriteLine("-----");
                    }

                    var avgDepth = (double)totalDepth / iterations;
                    var avgTime = totalTime / iterations;

                    // Write record to CSV.
                    csv.WriteRecord(new OutputCsvRecord(
                        avgDepth: avgDepth,
                        avgSeconds: avgTime.TotalSeconds,
                        totalChunks: totalChunks,
                        compactLevel: compactLevel,
                        sourceFileSize: TestFilesSizes[i].Item2));
                    await csv.NextRecordAsync();
                    await writer.FlushAsync();

                    Console.WriteLine();
                    Console.WriteLine($"  Completed test with {TestFilesSizes[i].Item2} random data, compactLevel {compactLevel}");
                    Console.WriteLine($"  Average required depth: {avgDepth}");
                    Console.WriteLine($"  Average duration: {avgTime.TotalSeconds} seconds");
                    Console.WriteLine($"  Average missed optimistic hashing: {
                        (double)totalMissedOptimisticHashing / iterations}");
                    Console.WriteLine("  Average amount buckets per collision:");
                    for (int l = 0; l < totalBucketsPerCollision.Count; l++)
                        Console.WriteLine($"    [{l}] = {(double)totalBucketsPerCollision[l] / iterations}");
                    Console.WriteLine($"  Total chunks: {totalChunks}");

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

            var chunkService = new ChunkService();
            var result = await chunkService.UploadSingleFileAsync(
                data,
                "text/plain",
                "testFile.txt",
                new Hasher(),
                compactLevel: compactLevel);

            var duration = DateTime.UtcNow - start;

            return (result, duration);
        }
    }
}