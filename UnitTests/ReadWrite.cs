using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;

namespace LasZip.UnitTests
{
    [TestClass]
    public class ReadWrite
    {
        private static readonly Version Las11;
        private static readonly Version Las12;
        private static readonly Version Las14;

        private PointCloudValidator? psmeValidator;
        private string? unitTestPath;

        public TestContext? TestContext { get; set; }

        static ReadWrite()
        {
            ReadWrite.Las11 = new(1, 1);
            ReadWrite.Las12 = new(1, 2);
            ReadWrite.Las14 = new(1, 4);
        }

        [TestInitialize]
        public void TestInitialize()
        {
            this.psmeValidator = new()
            {
                GeneratingSoftware = "QT Modeler 8.4.1836\0\0\0\0\0\0\0\0\0\0\0\0\0",
            };
            this.unitTestPath = Path.Combine(this.TestContext!.TestRunDirectory!, "..\\..\\UnitTests");
        }

        [TestMethod]
        public void ReadLasLaz()
        {
            Debug.Assert((this.psmeValidator != null) && (this.unitTestPath != null) && (this.TestContext != null));

            PointCloud psme110las = psmeValidator.ReadAndValidate(Path.Combine(unitTestPath, "PSME LAS 1.1 point type 0.las"), ReadWrite.Las11, 0);
            PointCloud psme111laz = psmeValidator.ReadAndValidate(Path.Combine(unitTestPath, "PSME LAS 1.1 point type 1.laz"), ReadWrite.Las11, 1);
            PointCloud psme120laz = psmeValidator.ReadAndValidate(Path.Combine(unitTestPath, "PSME LAS 1.2 point type 0.laz"), ReadWrite.Las12, 0);
            PointCloud psme121las = psmeValidator.ReadAndValidate(Path.Combine(unitTestPath, "PSME LAS 1.2 point type 1.las"), ReadWrite.Las12, 1);
            PointCloud psme146las = psmeValidator.ReadAndValidate(Path.Combine(unitTestPath, "PSME LAS 1.4 point type 6.las"), ReadWrite.Las14, 6);
            // fails because encoder is not yet supported
            PointCloud psme146laz = psmeValidator.ReadAndValidate(Path.Combine(unitTestPath, "PSME LAS 1.4 point type 6.laz"), ReadWrite.Las14, 6);

            Assert.IsTrue(PointCloud.HeaderCoreAndPointXyzEqual(psme110las, psme111laz));
            Assert.IsTrue(PointCloud.HeaderCoreAndPointXyzEqual(psme111laz, psme120laz));
            Assert.IsTrue(PointCloud.HeaderCoreAndPointXyzEqual(psme120laz, psme121las));
            Assert.IsTrue(PointCloud.HeaderCorePointIntensityXyzEqual(psme111laz, psme121las));
            Assert.IsTrue(PointCloud.HeaderCorePointIntensityXyzEqual(psme121las, psme146las));
            Assert.IsTrue(PointCloud.HeaderCorePointIntensityXyzEqual(psme146las, psme146laz));
        }

        [TestMethod]
        public void ReadLasWriteLaz()
        {
            Debug.Assert((this.psmeValidator != null) && (this.unitTestPath != null) && (this.TestContext != null) && (this.TestContext.TestRunResultsDirectory != null));

            PointCloud psme121las = psmeValidator.ReadAndValidate(Path.Combine(unitTestPath, "PSME LAS 1.2 point type 1.las"), ReadWrite.Las12, 1);

            string psme121lazPath = Path.Combine(this.TestContext.TestRunResultsDirectory, "PSME LAS 1.2 point type 1.laz");
            LasZipDll lasWriter = new();
            lasWriter.SetHeader(psme121las.Header);
            lasWriter.Header.GlobalEncoding = 0;
            lasWriter.OpenWriter(psme121lazPath, true);

            for (int pointIndex = 0; pointIndex < psme121las.Count; ++pointIndex)
            {
                lasWriter.Point.Gpstime = psme121las.Gpstime[pointIndex];
                lasWriter.Point.Intensity = psme121las.Intensity[pointIndex];
                lasWriter.Point.X = psme121las.X[pointIndex];
                lasWriter.Point.Y = psme121las.Y[pointIndex];
                lasWriter.Point.Z = psme121las.Z[pointIndex];
                lasWriter.WritePoint();
            }

            lasWriter.CloseWriter();

            PointCloud psme121laz = psmeValidator.ReadAndValidate(psme121lazPath, ReadWrite.Las12, 1);
            // fails because expectations are not flexibile enough to accommodate write differences
            // TODO: update expectations once LasZip.Net is modernized
            Assert.IsTrue(PointCloud.HeaderCorePointGpstimeIntensityXyzEqual(psme121las, psme121laz));
        }

        [TestMethod]
        public void ReadLazWriteLas()
        {
            Debug.Assert((this.psmeValidator != null) && (this.unitTestPath != null) && (this.TestContext != null) && (this.TestContext.TestRunResultsDirectory != null));

            PointCloud psme120laz = psmeValidator.ReadAndValidate(Path.Combine(unitTestPath, "PSME LAS 1.2 point type 0.laz"), ReadWrite.Las12, 0);

            string psme120lasPath = Path.Combine(this.TestContext.TestRunResultsDirectory, "PSME LAS 1.2 point type 0.las");
            LasZipDll lasWriter = new();
            lasWriter.SetHeader(psme120laz.Header);
            lasWriter.Header.GlobalEncoding = 16;
            lasWriter.OpenWriter(psme120lasPath, false);

            for (int pointIndex = 0; pointIndex < psme120laz.Count; ++pointIndex)
            {
                lasWriter.Point.Intensity = psme120laz.Intensity[pointIndex];
                lasWriter.Point.X = psme120laz.X[pointIndex];
                lasWriter.Point.Y = psme120laz.Y[pointIndex];
                lasWriter.Point.Z = psme120laz.Z[pointIndex];
                lasWriter.WritePoint();
            }

            lasWriter.CloseWriter();

            PointCloud psme120las = psmeValidator.ReadAndValidate(psme120lasPath, ReadWrite.Las12, 0);
            // fails because expectations are not flexibile enough to accommodate write differences
            // TODO: update expectations once LasZip.Net is modernized
            Assert.IsTrue(PointCloud.HeaderCorePointIntensityXyzEqual(psme120laz, psme120las));
        }
    }
}