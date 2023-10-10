using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management.Automation;

namespace LasZip.Examples
{
	[Cmdlet(VerbsOther.Use, "Laz")]
	public class UseLaz : Cmdlet
	{
        [Parameter(Mandatory = true, HelpMessage = "Path to .laz file to create and then read back.")]
		[ValidateNotNullOrEmpty]
        public string? File { get; set; }

        struct Point3D
		{
			public double X { get; set; }
			public double Y { get; set; }
            public double Z { get; set; }
        }

        protected override void ProcessRecord()
        {
			Debug.Assert(String.IsNullOrWhiteSpace(this.File) == false);

            this.WriteLaz(this.File); // Write a file
            UseLaz.ReadLaz(this.File);  // Read it back
       
			base.ProcessRecord();
        }

		private static void ReadLaz(string filePath)
		{
            LasZipDll lazReader = new();
			lazReader.OpenReader(filePath, out bool _);
			uint numberOfPoints = lazReader.Header.NumberOfPointRecords;

			// Check some header values
			Debug.WriteLine(lazReader.Header.MinX);
			Debug.WriteLine(lazReader.Header.MinY);
			Debug.WriteLine(lazReader.Header.MinZ);
			Debug.WriteLine(lazReader.Header.MaxX);
			Debug.WriteLine(lazReader.Header.MaxY);
			Debug.WriteLine(lazReader.Header.MaxZ);

			int classification;
            Point3D point = new();
			double[] coordArray = new double[3];

			// Loop through number of points indicated
			for (int pointIndex = 0; pointIndex < numberOfPoints; pointIndex++)
			{
				// Read the point
				lazReader.TryReadPoint();
				
				// Get precision coordinates
				lazReader.GetPointCoordinates(coordArray);
				point.X = coordArray[0];
				point.Y = coordArray[1];
				point.Z = coordArray[2];
				
				// Get classification value
				classification = lazReader.Point.ClassificationAndFlags;
			}

			// Close the reader
			lazReader.CloseReader();
		}

		private void WriteLaz(string filePath)
		{
            // --- Write Example
			List<Point3D> points = new() { new() { X = 1000.0, Y = 2000.0, Z = 100.0 },
										   new() { X = 5000.0, Y = 6000.0, Z = 200.0 } };

            LasZipDll lazWriter = new();
			// mandatory fields
			lazWriter.Header.VersionMajor = 1;
			lazWriter.Header.VersionMinor = 4;
			lazWriter.Header.HeaderSize = 375; // bytes when project GUID is included, LAS 1.4 R15 specification Table 3
			lazWriter.Header.OffsetToPointData = lazWriter.Header.HeaderSize; // since no variable length records

			lazWriter.Header.NumberOfPointRecords = (UInt32)points.Count;
            lazWriter.Header.PointDataFormat = 0; // point type 0: only xyz
            lazWriter.Header.PointDataRecordLength = 20; // for point type 0, see LAS specification for other types

            // set extent to to outer bounds of points
            lazWriter.Header.MinX = points[0].X;
			lazWriter.Header.MinY = points[0].Y;
			lazWriter.Header.MinZ = points[0].Z;
			lazWriter.Header.MaxX = points[1].X;
			lazWriter.Header.MaxY = points[1].Y;
			lazWriter.Header.MaxZ = points[1].Z;

			// open the file for write
			lazWriter.OpenWriter(filePath, true);

			// write points
			double[] coordArray = new double[3];
			foreach (Point3D point in points)
			{
				coordArray[0] = point.X;
				coordArray[1] = point.Y;
				coordArray[2] = point.Z;

				// Set the coordinates in the lazWriter object
				lazWriter.SetCoordinates(coordArray);

				// Set the classification to ground
				lazWriter.Point.ClassificationAndFlags = 2;

				// Write the point to the file
				lazWriter.WritePoint();
			}

			// close the writer to release the file
			lazWriter.CloseWriter();

			string? lasWarning = lazWriter.GetLastWarning();
			if (String.IsNullOrEmpty(lasWarning) == false)
			{
				// Show last warning that occurred
				this.WriteWarning(lasWarning);
			}
			// --- Upon completion, file should be 389 bytes
		}
	}
}
