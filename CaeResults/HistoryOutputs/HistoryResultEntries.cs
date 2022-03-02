﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CaeMesh;
using CaeGlobals;

namespace CaeResults
{
    [Serializable]
    public class HistoryResultEntries : NamedClass
    {
        // Variables                                                                                                                
        private List<double> _time;
        private List<double> _values;
        private List<int> _count;
        private bool _local;


        // Properties                                                                                                               
        public List<double> Time { get { return _time; } set { _time = value; } }
        public List<double> Values { get { return _values; } set { _values = value; } }
        public List<int> Count { get { return _count; } set { _count = value; } }
        public bool Local { get { return _local; } set { _local = value; } }


        // Constructor                                                                                                              
        public HistoryResultEntries(string name, bool local)
            : base()
        {
            _checkName = false;
            _name = name;
            _time = new List<double>();
            _values = new List<double>();
            _count = new List<int>();
            _local = local;
        }


        // Methods                                                                                                                  
        public void Add(double time, double value)
        {
            _time.Add(time);
            _values.Add(value);
            _count.Add(1);
        }
        public void Append(HistoryResultEntries historyResultEntry)
        {
            _time.AddRange(historyResultEntry.Time);
            _values.AddRange(historyResultEntry.Values);
        }
        public void ShiftTime(double timeSthift)
        {
            List<double> newTime = new List<double>();
            foreach (var time in _time) newTime.Add(time + timeSthift);
            _time = newTime;
        }
        public void SumValue(double value)
        {
            _values[_values.Count - 1] += value;
            _count[_count.Count - 1]++;
        }
        public void ComputeAverage()
        {
            for (int i = 0; i < _values.Count; i++)
            {
                if (_count[i] > 1) _values[i] /= _count[i];
            }
        }

    }
}
