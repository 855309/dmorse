using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dmorse
{
    internal class Signal
    {
        public SignalType Type { get; set; }
        public int Duration { get; set; }
        public double Frequency { get; set; }

        public Signal(SignalType type, int duration, double frequency)
        {
            Type = type;
            Duration = duration;
            Frequency = frequency;
        }

        public Signal(SignalType type, int duration)
        {
            Type = type;
            Duration = duration;
            Frequency = 0;
        }

        public override string ToString()
        {
            return $"Type = SignalType.{this.Type.ToString()}, Duration = {Duration}, Frequency = {Frequency}";
        }
    }
}
