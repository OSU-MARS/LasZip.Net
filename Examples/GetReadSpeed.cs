using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;

namespace LasZip.Examples
{
    [Cmdlet(VerbsCommon.Get, "ReadSpeed")]
    public class GetReadSpeed : Cmdlet
    {
        [Parameter(Mandatory = true, HelpMessage = "Path to .las or .laz file to read.")]
        [ValidateNotNullOrEmpty]
        public string? File { get; set; }

        protected override void ProcessRecord() 
        {
            Debug.Assert(this.File != null);
            Stopwatch stopwatch = new();
            stopwatch.Start();

            LasZipDll lazReader = new();
            lazReader.OpenReader(this.File, out bool _);
            // LAS 1.4 spec requires the legacy number of points be set to 0
            UInt64 totalNumberOfPoints = UInt64.Max(lazReader.Header.ExtendedNumberOfPointRecords, lazReader.Header.NumberOfPointRecords);

            const UInt64 maxBatchSize = 1000000; // 1 million points
            PointBatchXyz currentPointBatch = new((int)UInt64.Min(totalNumberOfPoints, maxBatchSize));
            List<PointBatchXyz> points = new(1);
            int pointsInBatch = 0;
            UInt64 pointsRead = 0;
            for (UInt64 point = 0; point < totalNumberOfPoints; ++point)
            {
                lazReader.ReadPoint();
                if (pointsInBatch >= currentPointBatch.Capacity)
                {
                    points.Add(currentPointBatch);
                    pointsRead += (UInt64)pointsInBatch;

                    UInt64 pointsRemaining = totalNumberOfPoints - pointsRead;
                    currentPointBatch = new((int)UInt64.Min(pointsRemaining, maxBatchSize));
                    pointsInBatch = 0;
                }

                currentPointBatch.X[pointsInBatch] = lazReader.Point.X;
                currentPointBatch.Y[pointsInBatch] = lazReader.Point.Y;
                currentPointBatch.Z[pointsInBatch] = lazReader.Point.Z;
                ++pointsInBatch;
            }

            currentPointBatch.Count = pointsInBatch;
            points.Add(currentPointBatch);
            pointsRead += (UInt64)pointsInBatch;

            stopwatch.Stop();
            FileInfo fileInfo = new(this.File);
            double megabytesRead = fileInfo.Length / 1.0E6;
            double gigabytesRead = fileInfo.Length / 1.0E9;
            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            this.WriteVerbose("Read " + gigabytesRead.ToString("0.00") + " GB and " + pointsRead.ToString("#,#") + " points in " + elapsedSeconds.ToString("0.000") + " s: " + (pointsRead / (1E6 * elapsedSeconds)).ToString("0.00") + " Mpoints/s, " + (megabytesRead / elapsedSeconds).ToString("0.0") + " MB/s.");
            // this.WriteObject(stopwatch.Elapsed);
            base.ProcessRecord();
        }
    }
}
