using HDF5CSharp;
using HDF5CSharp.Example;
using HDF5CSharp.Example.DataTypes;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HDF5_CSharp.Example.UnitTest
{
    public class BaseClass
    {
        protected ILogger Logger { get; }
        protected KamaAcquisitionFile kama { get; set; }
        protected string AcquisitionScanProtocolPath { get; set; }
        private List<string> Errors { get; set; }
        protected BaseClass()
        {
            Errors = new List<string>();
            AcquisitionScanProtocolPath = "AcquisitionScanProtocol.json";
            var loggerFactory = LoggerFactory.Create(builder =>
            {
            });
            Logger = loggerFactory.CreateLogger<BaseClass>();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            Errors = new List<string>();
            Hdf5.Settings.EnableErrorReporting(true);
            Hdf5Utils.LogWarning = (s) => Errors.Add(s);
            Hdf5Utils.LogError = (s) => Errors.Add(s);
        }
        [TestCleanup]
        public void TestCleanup()
        {
            Assert.IsTrue(Errors.Count == 0, "Errors were found");
            Errors.Clear();
        }
    }
    [TestClass]
    public class H5FileCreationTests : BaseClass
    {
        [TestMethod]
        public async Task CreatePatientInfoTest()
        {
            string data = new AcquisitionProtocolParameters() { AcquisitionDescription = "proto" }.AsJson();
            string filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "h5test.h5");
            kama = new KamaAcquisitionFile(filename, Logger);
            CreatePatientDetails(kama);
            UpdateSystemInformation(kama);


            AcquisitionProtocolParameters parameters = AcquisitionProtocolParameters.FromJson(data);
            await kama.StartLogging(parameters);
            Random rnd = new Random();

            string lineVoltage, lineCurrent, lineEcg;
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            int i = 0;

            for (int j = 0; j < 1000; j++)
            {
                ElectrodeFrame sample = new ElectrodeFrame();
                var itemsVoltage = Enumerable.Range(0, 100).Select(itm => rnd.Next().ToString()).ToList();
                var itemsCurrent = Enumerable.Range(0, 100).Select(itm => rnd.Next().ToString()).ToList();
                sample.timestamp = long.Parse(itemsCurrent.First());
                sample.SaturationMask = Convert.ToUInt64(itemsCurrent.Skip(1).Take(1).Single().Trim(), 16);
                var evenvals = itemsCurrent.Skip(2).Where((c, i) => i % 2 == 0).Select(float.Parse);
                var oddvals = itemsCurrent.Skip(2).Where((c, i) => i % 2 != 0).Select(float.Parse);

                sample.ComplexCurrentMatrix = evenvals.Zip(oddvals).Select(itm => (itm.First, itm.Second)).ToArray();

                evenvals = itemsVoltage.Skip(2).Where((c, i) => i % 2 == 0).Select(float.Parse);
                oddvals = itemsVoltage.Skip(2).Where((c, i) => i % 2 != 0).Select(float.Parse);

                sample.ComplexVoltageMatrix = evenvals.Zip(oddvals).Select(itm => (itm.First, itm.Second)).ToArray();
                kama.AppendElectrodeSample(sample);
            }

            ECGFrame frame = new ECGFrame();
            for (int j = 0; j < 100; j++)

            {
                var items = Enumerable.Range(0, 100).Select(itm => rnd.Next().ToString()).ToList();
                double LA_RA = double.Parse(items[0]);
                double LL_RA = double.Parse(items[1]);
                long time = long.Parse(items[2]);

                double LA_RA_filtered = double.Parse(items[4]);
                double LL_RA_filtered = double.Parse(items[5]);

                frame.FrameData.Add((LA_RA, LL_RA, time));
                frame.FilteredFrameData.Add((LA_RA_filtered, LL_RA_filtered, time));
            }

            kama.AppendEcgSample(frame);
            await kama.StopProcedure();
            var fileId = Hdf5.OpenFile(filename);

            Stopwatch st = Stopwatch.StartNew();
            var readback = Hdf5.ReadDatasetToArray<float>(fileId, "/eit/d1/voltages.re");
            st.Stop();
            Console.WriteLine("read time: " + st.ElapsedMilliseconds);
            Hdf5.CloseFile(fileId);
            File.Delete(filename);



        }

        [TestMethod]
        public void TestManyPatientsWriteRead()
        {
            Hdf5.Settings.EnableErrorReporting(true);
            Hdf5Utils.LogError += (s) => { Assert.Fail(s);};
            string filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{nameof(TestManyPatientsWriteRead)}.h5");
            var fileId = Hdf5.CreateFile(filename);
            var groupId = Hdf5.CreateOrOpenGroup(fileId, "root");
            PatientsContainer container = new PatientsContainer(fileId, groupId, Logger);
            container.FlushData();
            container.Dispose();
            Hdf5.CloseGroup(groupId);
            Hdf5.CloseFile(fileId);
            fileId = Hdf5.OpenFile(filename, true);
            var readBack = Hdf5.ReadObject<PatientsContainer>(fileId, "root/patients");
            Assert.IsTrue(container.Equals(readBack));
            Hdf5.CloseFile(fileId);
            File.Delete(filename);

        }


        [TestMethod]
        public async Task TestFullFileWriteRead()
        {
            string filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test2.h5");
            Console.WriteLine(filename);
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            kama = new KamaAcquisitionFile(filename, Logger);
            ProcedureInfo info = new ProcedureInfo
            {
                Age = 18,
                FirstName = "Lior",
                LastName = "Banai",
                ExamDate = DateTime.Now,
                Procedure = "test",
                ProcedureID = "ID",
                Patient = new PatientInfo()
            };

            kama.SavePatientInfo(info);
            kama.UpdateSystemInformation("32423423", new[] { "11", "12" });
            kama.SetProcedureInformation(info);
            var acq = new AcquisitionProtocolParameters() { AcquisitionDescription = "none" };
            string data = acq.AsJson();
            AcquisitionProtocolParameters parameters = AcquisitionProtocolParameters.FromJson(data);
            await kama.StartLogging(parameters);
            var ecgTask = WriteECGData(kama, 2, 1);
            var eitTask = WriteEITData(parameters, kama, 5, 1);
            var seTask = WriteSystemEvents(kama);
            await Task.WhenAll(ecgTask, eitTask, seTask);
            var ecgTestData = await ecgTask;
            var eitsTestData = await eitTask;

            kama.StopRecording();
            await kama.StopProcedure();


            using (KamaAcquisitionReadOnlyFile readFile = new KamaAcquisitionReadOnlyFile(filename))
            {
                readFile.ReadSystemInformation();
                readFile.ReadProcedureInformation();
                readFile.ReadPatientInformation();
                Assert.IsTrue(readFile.PatientInformation.Equals(kama.PatientInfo));
                Assert.IsTrue(readFile.ProcedureInformation.Equals(kama.ProcedureInformation));
                Assert.IsTrue(readFile.SystemInformation.Equals(kama.SystemInformation));

                readFile.ReadECGData();
                Assert.IsTrue(readFile.ECG.Equals(ecgTestData));
                readFile.ReadEITData();
                Assert.IsTrue(readFile.EITs.SequenceEqual(eitsTestData));
                readFile.ReadSystemEvents();
            }

            File.Delete(filename);
        }

        [TestMethod]
        public async Task TestCalibrationsData()
        {
            string filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestCalibrationsData.h5");
            Console.WriteLine(filename);
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            kama = new KamaAcquisitionFile(filename, Logger);
            ProcedureInfo info = new ProcedureInfo
            {
                Age = 18,
                FirstName = "First",
                LastName = "Last",
                ExamDate = DateTime.Now,
                Procedure = "test",
                ProcedureID = "ID",
                Patient = new PatientInfo()
            };

            kama.SavePatientInfo(info);
            kama.UpdateSystemInformation("32423423", new[] { "11", "12" });
            var CalibInfo = new CalibrationsSystemInformation { SystemNewHWContent = "none" };
            string data = CalibInfo.ToJson();
            CalibrationsSystemInformation calib = CalibrationsSystemInformation.FromJson(data);
            kama.AddCalibrationsData(calib);
            await kama.StopProcedure();
            File.Delete(filename);
        }

        [TestMethod]
        public async Task TestsSystemInformation()
        {
            string filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestsSystemInformation.h5");
            Console.WriteLine(filename);
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            kama = new KamaAcquisitionFile(filename, Logger);
            ProcedureInfo info = new ProcedureInfo
            {
                Age = 18,
                FirstName = "First",
                LastName = "Last",
                ExamDate = DateTime.Now,
                Procedure = "test",
                ProcedureID = "ID",
                Patient = new PatientInfo()
            };

            kama.SavePatientInfo(info);
            kama.UpdateSystemInformation("32423423", new[] { "11", "12" });
            await kama.StopProcedure();
            File.Delete(filename);
        }

        private void CreatePatientDetails(KamaAcquisitionFile file)
        {
            ProcedureInfo info = new ProcedureInfo
            {
                Age = 18,
                FirstName = "First",
                LastName = "Last",
                ExamDate = DateTime.Now,
                Procedure = "test",
                ProcedureID = "ID",
                Patient = new PatientInfo()
            };

            file.SavePatientInfo(info);
        }

        private void UpdateSystemInformation(KamaAcquisitionFile file)
        {
            kama.UpdateSystemInformation("32423423", new[] { "11", "12" });
        }

        private async Task<ECGData> WriteECGData(KamaAcquisitionFile file, int loop,
            int sleepsBetweenWritesMilliseconds)
        {
            var result = await Task.Factory.StartNew(() =>
            {
                Random random = new Random();
                List<ECGFrame> ecgFrames = new List<ECGFrame>();
                for (int index = 0; index < loop; index++)
                {
                    ECGFrame frame = new ECGFrame
                    {
                        FilteredFrameData = Enumerable.Range(0, 240)
                                 .Select(i => (random.NextDouble(), random.NextDouble(),
                                     DateTimeOffset.Now.ToUnixTimeMilliseconds())).ToList(),
                        FrameData = Enumerable.Range(0, 240)
                                 .Select(i => (random.NextDouble(), random.NextDouble(),
                                     DateTimeOffset.Now.ToUnixTimeMilliseconds())).ToList(),
                    };

                    file.AppendEcgSample(frame);
                    ecgFrames.Add(frame);
                    Thread.Sleep(sleepsBetweenWritesMilliseconds);
                }
                ECGData ecgdata = new ECGData();
                ecgdata.SamplingRate = ECGFrame.AcqSampleRate;
                ecgdata.StartDateTime = DateTimeOffset.FromUnixTimeMilliseconds(ecgFrames.First().FrameData.First().Timestamp).DateTime;
                ecgdata.EndDateTime = DateTimeOffset.FromUnixTimeMilliseconds(ecgFrames.Last().FrameData.Last().Timestamp).DateTime;

                ecgdata.FilteredSignal = new double[ecgFrames.Count * ecgFrames.First().FilteredFrameData.Count, 2];
                ecgdata.UnfilteredSignal = new double[ecgFrames.Count * ecgFrames.First().FrameData.Count, 2];
                ecgdata.Timestamps = new long[ecgFrames.Count * ecgFrames.First().FrameData.Count, 1];
                for (var index = 0; index < ecgFrames.Count; index++)
                {
                    ECGFrame data = ecgFrames[index];
                    for (var i = 0; i < data.FrameData.Count; i++)
                    {
                        var sample = data.FrameData[i];
                        ecgdata.UnfilteredSignal[index * data.FrameData.Count + i, 0] = sample.LA_RA;
                        ecgdata.UnfilteredSignal[index * data.FrameData.Count + i, 1] = sample.LL_RA;
                    }


                    for (var i = 0; i < data.FilteredFrameData.Count; i++)
                    {
                        var sample = data.FilteredFrameData[i];
                        ecgdata.FilteredSignal[index * data.FilteredFrameData.Count + i, 0] = sample.LA_RA;
                        ecgdata.FilteredSignal[index * data.FilteredFrameData.Count + i, 1] = sample.LL_RA;
                    }

                    for (var i = 0; i < data.FilteredFrameData.Count; i++)
                    {
                        var sample = data.FilteredFrameData[i];
                        ecgdata.Timestamps[index * data.FilteredFrameData.Count + i, 0] = sample.Timestamp;

                    }
                }

                return ecgdata;
            });
            return result;
        }

        private async Task<List<EITEntry>> WriteEITData(AcquisitionProtocolParameters parameters,
            KamaAcquisitionFile file, int loop, int sleepsBetweenWritesMilliseconds)
        {

            var result = await Task.Factory.StartNew(() =>
            {
                Random random = new Random();
                List<ElectrodeFrame> samples = new List<ElectrodeFrame>();
                for (int index = 0; index < loop; index++)
                {
                    ElectrodeFrame frame = new ElectrodeFrame();
                    frame.ComplexCurrentMatrix = Enumerable.Range(0, 2441)
                        .Select(i => ((float)random.NextDouble(), (float)random.NextDouble())).ToArray();
                    frame.ComplexVoltageMatrix = Enumerable.Range(0, 2441)
                        .Select(i => ((float)random.NextDouble(), (float)random.NextDouble())).ToArray();

                    var timestamp = DateTimeOffset.Now;
                    frame.timestamp = timestamp.ToUnixTimeMilliseconds();
                    ulong saturationMask = (ulong)random.Next();
                    saturationMask = (saturationMask << 32);
                    saturationMask = saturationMask | (ulong)random.Next();
                    frame.SaturationMask = saturationMask;
                    Thread.Sleep(sleepsBetweenWritesMilliseconds);
                    file.AppendElectrodeSample(frame);
                    samples.Add(frame);
                }

                EITEntry entry = new EITEntry
                {
                    Configuration = parameters.AsJson(),
                    StartDateTime = DateTimeOffset.FromUnixTimeMilliseconds(samples.First().timestamp).DateTime,
                    EndDateTime = DateTimeOffset.FromUnixTimeMilliseconds(samples.Last().timestamp).DateTime,
                    VoltagesReal = new float[samples.Count, samples[0].ComplexVoltageMatrix.Length],
                    VoltagesIm = new float[samples.Count, samples[0].ComplexVoltageMatrix.Length],
                    CurrentsIm = new float[samples.Count, samples[0].ComplexVoltageMatrix.Length],
                    CurrentsReal = new float[samples.Count, samples[0].ComplexVoltageMatrix.Length],
                    Saturation = new ulong[samples.Count, 1],
                    Timestamps = new long[samples.Count, 1]
                };

                for (var i = 0; i < samples.Count; i++)
                {
                    ElectrodeFrame electrodeFrame = samples[i];

                    for (int j = 0; j < electrodeFrame.ComplexVoltageMatrix.Length; j++)
                    {
                        entry.VoltagesReal[i, j] = electrodeFrame.ComplexVoltageMatrix[j].Re;
                        entry.VoltagesIm[i, j] = electrodeFrame.ComplexVoltageMatrix[j].Im;
                    }

                    for (int j = 0; j < electrodeFrame.ComplexCurrentMatrix.Length; j++)
                    {
                        entry.CurrentsReal[i, j] = electrodeFrame.ComplexCurrentMatrix[j].Re;
                        entry.CurrentsIm[i, j] = electrodeFrame.ComplexCurrentMatrix[j].Im;
                    }

                    entry.Saturation[i, 0] = electrodeFrame.SaturationMask;
                    entry.Timestamps[i, 0] = electrodeFrame.timestamp;
                }

                return entry;
            });
            return new List<EITEntry> { result
    };
        }

        private Task WriteSystemEvents(KamaAcquisitionFile file)
        {
            Array values = Enum.GetValues(typeof(SystemEventType));
            Random random = new Random();

            return Task.Factory.StartNew(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    SystemEventType set = (SystemEventType)values.GetValue(random.Next(values.Length));
                    SystemEventModel se = new SystemEventModel(set, DateTimeOffset.Now.ToUnixTimeMilliseconds(), "Empty");
                    file.AppendSystemEvent(se);
                }
            });
        }

    }
}
