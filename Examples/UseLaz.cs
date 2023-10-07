using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management.Automation;
using System.Runtime.CompilerServices;

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
			bool isCompressedFile = true;
			lazReader.OpenReader(filePath, ref isCompressedFile);
			uint numberOfPoints = lazReader.Header.NumberOfPointRecords;

			// Check some header values
			Debug.WriteLine(lazReader.Header.MinX);
			Debug.WriteLine(lazReader.Header.MinY);
			Debug.WriteLine(lazReader.Header.MinZ);
			Debug.WriteLine(lazReader.Header.MaxX);
			Debug.WriteLine(lazReader.Header.MaxY);
			Debug.WriteLine(lazReader.Header.MaxZ);

			int classification = 0;
			var point = new Point3D();
			var coordArray = new double[3];

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
				classification = lazReader.Point.Classification;
			}

			// Close the reader
			lazReader.CloseReader();
		}

		private void WriteLaz(string filePath)
		{
			// --- Write Example
			var point = new Point3D();
			var points = new List<Point3D>();

			point.X = 1000.0;
			point.Y = 2000.0;
			point.Z = 100.0;
			points.Add(point);

			point.X = 5000.0;
			point.Y = 6000.0;
			point.Z = 200.0;
			points.Add(point);

            LasZipDll lazWriter = new();
			// Number of point records needs to be set
			lazWriter.Header.NumberOfPointRecords = (UInt32)points.Count;

			// Header Min/Max needs to be set to extents of points
			lazWriter.Header.MinX = points[0].X; // LL Point
			lazWriter.Header.MinY = points[0].Y;
			lazWriter.Header.MinZ = points[0].Z;
			lazWriter.Header.MaxX = points[1].X; // UR Point
			lazWriter.Header.MaxY = points[1].Y;
			lazWriter.Header.MaxZ = points[1].Z;

			// Open the writer and test for errors
			int err = lazWriter.OpenWriter(filePath, true);
			if (err == 0)
			{
				double[] coordArray = new double[3];
				foreach (var p in points)
				{
					coordArray[0] = p.X;
					coordArray[1] = p.Y;
					coordArray[2] = p.Z;

					// Set the coordinates in the lazWriter object
					lazWriter.SetCoordinates(coordArray);

					// Set the classification to ground
					lazWriter.Point.Classification = 2;

					// Write the point to the file
					err = lazWriter.WritePoint();
					if (err != 0) break;
				}

				// Close the writer to release the file (OS lock)
				err = lazWriter.CloseWriter();
			}

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
