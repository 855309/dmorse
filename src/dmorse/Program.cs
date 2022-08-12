using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FftSharp;
using NAudio.Wave;

namespace dmorse
{
    internal class Program // TODO: Think how to fix real-time decoding (not working currently)
    {
        public static Decoder decoder = null;
        public static WaveInEvent deviceInput = null;
        public static int devSampleRate = 48000;

        public static Timer decTimer = null;
        public static int timerInterval = 1000; // hmm

        static void Main(string[] args)
        {
            string[] alphabetData;
            bool debug = false;

            if (args.Length < 1)
            {
                Console.WriteLine("Usage: dmorse [audiofile] [options]...");
                Environment.Exit(-1);
            }

            string path = args[0];

            alphabetData = File.ReadAllLines("morse/symbols.txt");

            foreach (string arg in args)
            {
                if (arg == "--debug")
                {
                    debug = true;

                    break;
                }
            }

            decoder = new Decoder(debug, alphabetData);

            new Program().MainAsync(path).GetAwaiter().GetResult();
        }

        public async Task MainAsync(string path)
        {
            string[] pts = path.Split(':');
            if (pts[0] == "device" && pts.Length == 2)
            {
                if (pts[1] == "list")
                {
                    for (int i = 0; i < WaveIn.DeviceCount; i++)
                    {
                        Console.WriteLine("{0} {1}", i, WaveIn.GetCapabilities(i).ProductName);
                    }

                    Environment.Exit(0);
                }

                int deviceIndex;
                if (!int.TryParse(pts[1], out deviceIndex))
                {
                    Console.WriteLine("Device index should be an integer.");
                    Environment.Exit(-1);
                }

                deviceInput = new WaveInEvent();
                deviceInput.DeviceNumber = deviceIndex;
                deviceInput.WaveFormat = new WaveFormat(rate: devSampleRate, bits: 16, channels: 1);
                deviceInput.DataAvailable += OnDeviceInput;
                deviceInput.BufferMilliseconds = 20;
                deviceInput.StartRecording();

                decTimer = new Timer(TimerCallback, null, timerInterval, Timeout.Infinite);

                await Task.Delay(-1);
            }
            else
            {
                (int sampleRate, double[] data) = AudioFile.ReadMono(path);

                double[][] fftdata = decoder.Transform(data, sampleRate);
                List<Signal> signaldata = decoder.ParseFFTData(fftdata, sampleRate);

                Console.WriteLine(decoder.DecodeSignal(signaldata));
            }
        }

        List<double> buffer = new List<double>();
        private void OnDeviceInput(object sender, WaveInEventArgs args)
        {
            int bytesPerSample = deviceInput.WaveFormat.BitsPerSample / 8;
            int samplesRecorded = args.BytesRecorded / bytesPerSample;

            for (int i = 0; i < samplesRecorded; i++)
                buffer.Add(BitConverter.ToInt16(args.Buffer, i * bytesPerSample));
        }

        private void TimerCallback(object state)
        {
            Thread decodeThread = new Thread(DecodeBuffer);
            decodeThread.Start();

            decTimer.Change(timerInterval, Timeout.Infinite);
        }

        // string lastStr = "";
        private void DecodeBuffer()
        {
            double[][] fftdata = decoder.Transform(buffer.ToArray(), devSampleRate);
            List<Signal> signaldata = decoder.ParseFFTData(fftdata, devSampleRate);
            if (signaldata.Count == 0)
            {
                buffer.Clear();
                return;
            }

            string output = decoder.DecodeSignal(signaldata).Trim();

            if (output != "")
            {
                Console.Write(output);
                buffer.Clear();
            }
        }
    }

    internal static class Extensions
    {
        public static bool InRange(this double obj, double f, double l)
        {
            return f <= obj && obj <= l;
        }

        public static bool InRange(this int obj, double f, double l)
        {
            return f <= obj && obj <= l;
        }

        public static bool InBounds(this double obj, double x, double n)
        {
            return obj - n <= x && x <= obj + n;
        }

        public static bool InBounds(this int obj, double x, double n)
        {
            return obj - n <= x && x <= obj + n;
        }

        public static List<double> getPlVal(this List<Signal> lst)
        {
            List<double> sgn = new List<double>();
            foreach (Signal s in lst)
            {
                sgn = sgn.Concat(Enumerable.Repeat(s.Frequency, s.Duration)).ToList();
            }

            return sgn;
        }
    }

    public static class AudioFile
    {
        /* https://github.com/swharden/Spectrogram/blob/main/src/Spectrogram/WavFile.cs */

        private static (string id, uint length) ChunkInfo(BinaryReader br, long position)
        {
            br.BaseStream.Seek(position, SeekOrigin.Begin);
            string chunkID = new string(br.ReadChars(4));
            uint chunkBytes = br.ReadUInt32();
            return (chunkID, chunkBytes);
        }

        public static (int sampleRate, double[] L) ReadMono(string filePath)
        {
            (int sampleRate, double[] L, _) = ReadStereo(filePath);
            return (sampleRate, L);
        }

        public static (int sampleRate, double[] L, double[] R) ReadStereo(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                var (id, length) = ChunkInfo(br, 0);
                if (id != "RIFF")
                    throw new InvalidOperationException($"Unsupported WAV format (first chunk ID was '{id}', not 'RIFF')");

                var fmtChunk = ChunkInfo(br, 12);
                if (fmtChunk.id != "fmt ")
                    throw new InvalidOperationException($"Unsupported WAV format (first chunk ID was '{fmtChunk.id}', not 'fmt ')");
                if (fmtChunk.length != 16)
                    throw new InvalidOperationException($"Unsupported WAV format (expect 16 byte 'fmt' chunk, got {fmtChunk.length} bytes)");

                int audioFormat = br.ReadUInt16();
                if (audioFormat != 1)
                    throw new NotImplementedException("Unsupported WAV format (audio format must be 1, indicating uncompressed PCM data)");

                int channelCount = br.ReadUInt16();
                if (channelCount < 0 || channelCount > 2)
                    throw new NotImplementedException($"Unsupported WAV format (must be 1 or 2 channel, file has {channelCount})");

                int sampleRate = (int)br.ReadUInt32();

                int byteRate = (int)br.ReadUInt32();

                ushort blockSize = br.ReadUInt16();
                //Console.WriteLine($"block size: {blockSize} bytes per sample");

                ushort bitsPerSample = br.ReadUInt16();
                //Console.WriteLine($"resolution: {bitsPerSample}-bit");
                if (bitsPerSample != 16)
                    throw new NotImplementedException("Only 16-bit WAV files are supported");

                long nextChunkPosition = 36;
                int maximumChunkNumber = 42;
                long firstDataByte = 0;
                long dataByteCount = 0;
                for (int i = 0; i < maximumChunkNumber; i++)
                {
                    var chunk = ChunkInfo(br, nextChunkPosition);
                    if (chunk.id == "data")
                    {
                        firstDataByte = nextChunkPosition + 8;
                        dataByteCount = chunk.length;
                        break;
                    }
                    nextChunkPosition += chunk.length + 8;
                }
                if (firstDataByte == 0 || dataByteCount == 0)
                    throw new InvalidOperationException("Unsupported WAV format (no 'data' chunk found)");

                long sampleCount = dataByteCount / blockSize;

                double[] L = null;
                double[] R = null;

                if (channelCount == 1)
                {
                    L = new double[sampleCount];
                    for (int i = 0; i < sampleCount; i++)
                    {
                        L[i] = br.ReadInt16();
                    }
                }
                else if (channelCount == 2)
                {
                    L = new double[sampleCount];
                    R = new double[sampleCount];
                    for (int i = 0; i < sampleCount; i++)
                    {
                        L[i] = br.ReadInt16();
                        R[i] = br.ReadInt16();
                    }
                }

                return (sampleRate, L, R);
            }
        }
    }
}
