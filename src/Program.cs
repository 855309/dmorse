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
        public static Decoder decoder = new Decoder(false, new string[]{});
        public static Generator generator = new Generator(new string[]{}, "", 0, "", 0, 0);

        public static WaveInEvent deviceInput = new WaveInEvent();
        public static int devSampleRate = 48000;

        public static Timer decTimer = null;
        public static int timerInterval = 3000; // hmm
        public static List<Argument> argList = new List<Argument>();
        public static bool debug = false;

        // 0 => check if there
        // 1 => get val
        public static List<(string, string, int)> argvls = new List<(string, string, int)>() 
        { 
            // arg, default val
            ("debug", "", 0), 
            ("out", "out.wav", 1), 
            ("wpm", "25", 1), 
            ("freq", "800", 1),
            ("vol", "0.5", 1)
        }; 

        static void PrintUsage()
        {
            Console.WriteLine("Usage: dmorse [decode/generate] [audio/text file] [options]...");
            Console.WriteLine("\t --debug:           Print debug messages.");
            Console.WriteLine("\t --out <string>:    Set the output audio file.");
            Console.WriteLine("\t --wpm <int>:       Set the output wpm. (Default: 25)");
            Console.WriteLine("\t --freq <int>:      Set the output frequency. (Default: 800)");
            Console.WriteLine("\t --vol <double>:    Set the output volume. Min. 0, Max. 1 (Default: 0.5)");
        }

        static List<Argument> ParseArgs(string[] args)
        {
            List<Argument> argList = new List<Argument>();

            if (args.Length < 1)
            {
                PrintUsage();
                Environment.Exit(-1);
            }

            argList.Add(new Argument("instr", args[0]));
            argList.Add(new Argument("file", args[1]));

            for (int i = 0; i < args.Length; i++)
            {
                foreach (var a in argvls)
                {
                    if (args[i] == "--" + a.Item1)
                    {
                        if (a.Item3 == 0)
                        {
                            argList.Add(new Argument(a.Item1, ""));
                        }
                        else
                        {
                            if (i + 1 < args.Length)
                            {
                                argList.Add(new Argument(a.Item1, args[i + 1]));
                            }
                            else
                            {
                                PrintUsage();
                                Environment.Exit(-2);
                            }
                        }

                        break;
                    }
                }
            }

            return argList;
        }

        static bool CheckArg(string name)
        {
            foreach (Argument arg in argList)
            {
                if (arg.Name == name)
                {
                    return true;
                }
            }

            return false;
        }

        static string GetArgVal(string name)
        {
            foreach (Argument arg in argList)
            {
                if (arg.Name == name)
                {
                    return arg.Value;
                }
            }

            foreach (var a in argvls)
            {
                if (a.Item1 == name)
                {
                    return a.Item2; // return default
                }
            }

            return "undefined";
        }

        static void Main(string[] args)
        {
            string[] alphabetData = File.ReadAllLines("morse/symbols.txt");

            // test(alphabetData);

            argList = ParseArgs(args);
            debug = CheckArg("debug");

            string instr = GetArgVal("instr");
            if (instr == "decode")
            {
                decoder = new Decoder(debug, alphabetData);

                new Program().DecodeAsync(GetArgVal("file")).GetAwaiter().GetResult();
            }
            else if (instr == "generate")
            {
                string strdata = File.ReadAllText(GetArgVal("file")).Trim().ToUpper();

                int wpm = Convert.ToInt32(GetArgVal("wpm"));
                string outfile = GetArgVal("out");
                int freq = Convert.ToInt32(GetArgVal("freq"));
                double vol = Convert.ToDouble(GetArgVal("vol"));

                generator = new Generator(alphabetData, strdata, wpm, outfile, freq, vol);

                (WaveHeaderChunk header, WaveFormatChunk format, WaveDataChunk data) = generator.GenerateAudio();
                generator.WriteWavefile(header, format, data);
            }
        }

        public async Task DecodeAsync(string path)
        {
            string[] pts = path.Split(':');
            if (pts[0] == "device" && pts.Length == 2)
            {
                if (pts[1] == "list")
                {
                    for (int i = 0; i < WaveInEvent.DeviceCount; i++)
                    {
                        Console.WriteLine("{0} {1}", i, WaveInEvent.GetCapabilities(i).ProductName);
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

        string cbuf = "";
        //string cStr = "";
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
                Console.WriteLine(cbuf + output);
                buffer.Clear();

                cbuf += output;
            }
        }
    }

    internal class Argument
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public Argument(string n, string v)
        {
            this.Name = n;
            this.Value = v;
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

                double[] L = new double[sampleCount];
                double[] R = new double[sampleCount];

                if (channelCount == 1)
                {
                    for (int i = 0; i < sampleCount; i++)
                    {
                        L[i] = br.ReadInt16();
                    }
                }
                else if (channelCount == 2)
                {
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
