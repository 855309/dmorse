using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dmorse
{
    internal class Decoder
    {
        public bool debug { get; set; }
        public string[] alphabetData { get; set; }

        public const int stepsize = 100;
        public const int fftsize = 512;

        public Decoder(bool dbg, string[] alpData)
        {
            this.debug = dbg;
            this.alphabetData = alpData;
        }

        public string DecodeSignal(List<Signal> msignal)
        {
            int maxFullDur = msignal.Max((s) =>
            {
                return s.Type == SignalType.Full ? s.Duration : 0;
            });

            int minFullDur = msignal.Min((s) =>
            {
                return s.Type == SignalType.Full ? s.Duration : maxFullDur;
            });

            int maxEmpDur = msignal.Max((s) =>
            {
                return s.Type == SignalType.Empty ? s.Duration : 0;
            });

            int minEmpDur = msignal.Min((s) =>
            {
                return s.Type == SignalType.Empty ? s.Duration : maxEmpDur;
            });

            if (minEmpDur == 1) // i'm not very sure about this
            {
                if (debug) Console.WriteLine("WPM is high, trying to adjust the spacing... (Result can be affected)");

                //minEmpDur++;

                /* getting the next min. space duration */
                int min = msignal.Min((s) =>
                {
                    if (s.Type == SignalType.Empty && s.Duration != minEmpDur)
                    {
                        return s.Duration;
                    }
                    else
                    {
                        return maxEmpDur;
                    }
                });

                minEmpDur = min;
            }

            if (minFullDur == 1)
            {
                if (debug) Console.WriteLine("Error signal or very high (60<) WPM. Trying to fix... (Result can be affected)");

                /* getting the next min. space dur. */
                int min = msignal.Min((s) =>
                {
                    if (s.Type == SignalType.Full && s.Duration != minFullDur)
                    {
                        return s.Duration;
                    }
                    else
                    {
                        return maxFullDur;
                    }
                });

                minFullDur = min;
            }

            List<List<Signal>> words = new List<List<Signal>>();

            if (maxEmpDur > minEmpDur * 5)
            {
                List<Signal> cList = new List<Signal>();
                foreach (Signal sig in msignal)
                {
                    if (sig.Type == SignalType.Empty && sig.Duration > minEmpDur * 5)
                    {
                        words.Add(cList.ToList());

                        cList.Clear();
                    }
                    else
                    {
                        cList.Add(sig);
                    }
                }

                words.Add(cList);
            }
            else
            {
                words.Add(msignal);
            }

            List<List<string>> fdataLst = new List<List<string>>();
            foreach (List<Signal> word in words)
            {
                maxEmpDur = word.Max((s) =>
                {
                    return s.Type == SignalType.Empty ? s.Duration : 0;
                });

                double fullTolerance = (maxFullDur - minFullDur) / 4;
                double empTolerance = (maxEmpDur - minEmpDur) / 4;

                /* parsing letters/symbols */
                List<List<Signal>> symbols = new List<List<Signal>>();
                if (maxEmpDur > minEmpDur * 2)
                {
                    List<Signal> cList = new List<Signal>();
                    foreach (Signal sig in word)
                    {
                        if (sig.Type == SignalType.Empty && sig.Duration > minEmpDur * 2)
                        {
                            symbols.Add(cList.ToList());

                            cList.Clear();
                        }
                        else
                        {
                            cList.Add(sig);
                        }
                    }

                    symbols.Add(cList);
                }
                else
                {
                    symbols.Add(word);
                }

                /* converting word to str */
                List<string> wordLstData = new List<string>();
                foreach (List<Signal> symbol in symbols)
                {
                    string symStr = "";
                    foreach (Signal s in symbol)
                    {
                        if (s.Type == SignalType.Full)
                        {
                            if (maxFullDur.InBounds(s.Duration, fullTolerance))
                            {
                                symStr += "1";
                            }

                            if (minFullDur.InBounds(s.Duration, fullTolerance))
                            {
                                symStr += "0";
                            }
                        }
                    }

                    wordLstData.Add(symStr);
                }

                fdataLst.Add(wordLstData);
            }

            /*List<string> dbgstr = new List<string>();
            foreach (var d in fdataLst)
            {
                dbgstr.Add(string.Join(" ", d));
            }
            string finalData = string.Join(" / ", dbgstr);

            Console.WriteLine(finalData);*/

            List<string> outstrLst = new List<string>();
            foreach (List<string> word in fdataLst)
            {
                string wdata = "";
                foreach (string symbol in word)
                {
                    foreach (string entry in alphabetData)
                    {
                        if (entry.Trim() == "") continue;

                        string[] pts = entry.Split(' ');
                        if (pts[0] == symbol)
                        {
                            wdata += pts[1];
                            break;
                        }
                    }
                }

                outstrLst.Add(wdata);
            }

            return string.Join(" ", outstrLst);
        }

        private double mFreq(double[] arr)
        {
            int maxcount = 0;
            double max_freq = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                int count = 0;
                for (int j = 0; j < arr.Length; j++)
                {
                    if (arr[i] == arr[j])
                    {
                        count++;
                    }
                }

                if (count > maxcount)
                {
                    maxcount = count;
                    max_freq = arr[i];
                }
            }

            return max_freq;
        }

        public double[][] Transform(double[] data, int sampleRate)
        {
            int stepsize = 100;
            int fftsize = 512;

            int nyquist = sampleRate / 2;
            int height = fftsize / 2;

            int fftcn = (data.Length - fftsize) / stepsize;
            double[][] fftdB = new double[fftcn][];

            var window = new FftSharp.Windows.Hanning().Create(fftsize);

            Parallel.For(0, fftcn, newFftIndex =>
            {
                double[] buffer = new double[fftsize];
                long sourceIndex = newFftIndex * stepsize;
                for (int i = 0; i < fftsize; i++)
                    buffer[i] = data[sourceIndex + i] * window[i];

                fftdB[newFftIndex] = FftSharp.Transform.FFTpower(buffer);
            });

            return fftdB;
        }

        public List<Signal> ParseFFTData(double[][] fftdB, int sampleRate)
        {
            List<double> freqData = new List<double>();
            List<double> dBData = new List<double>();

            double filterdB = 50; // min. dB for peak frequency
            // TODO: Make filter dB relative

            int height = fftsize / 2; // for the frequency map

            double[] freqfft = FftSharp.Transform.FFTfreq(sampleRate, height);
            for (int i = 0; i < fftdB.Length; i++)
            {
                double[] nfft = fftdB[i];
                double m = 0;
                int mindex = 0;

                for (int j = 0; j < nfft.Length; j++)
                {
                    double nf = nfft[j];
                    if (nf > m && nf > filterdB)
                    {
                        m = nf;
                        mindex = j;
                    }
                }

                double pfreq = freqfft[mindex];

                freqData.Add(pfreq);
                dBData.Add(m);
            }

            List<Signal> msignal = new List<Signal>();

            int spaceCount = 0;
            int signalCount = 0;
            double signalFreq = 0;
            bool started = false;

            List<double> zeroRemovedF = new List<double>();
            zeroRemovedF = zeroRemovedF.Concat(freqData).ToList();
            zeroRemovedF.RemoveAll(i => (i == 0));
            double avfreq = mFreq(zeroRemovedF.ToArray());

            double avdB = dBData.Max();

            for (int j = 0; j < dBData.Count; j++)
            {
                if (dBData[j] == 0 || !avdB.InBounds(dBData[j], 10) || !avfreq.InBounds(freqData[j], 20))
                {
                    if (started)
                    {
                        if (signalCount != 0)
                        {
                            msignal.Add(new Signal(SignalType.Full, signalCount, signalFreq));
                        }

                        signalCount = 0;
                        signalFreq = 0;
                        spaceCount++;
                    }
                }
                else
                {
                    if (!started) started = true;

                    if (spaceCount != 0)
                    {
                        msignal.Add(new Signal(SignalType.Empty, spaceCount, 0));
                    }

                    signalFreq = freqData[j];
                    spaceCount = 0;
                    signalCount++;
                }
            }

            return msignal;
        }
    }
}
