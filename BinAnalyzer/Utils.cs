using System;
using System.Collections.Generic;
using System.Text;

namespace BinAnalyzer
{
    class Utils
    {
        /// <summary>
        /// Creates count disjoint intervalls between min and max which cover all values between and including min,max
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="count"></param>
        /// <returns>number,start,end</returns>
        public static IEnumerable<Tuple<ulong,ulong, ulong>> buildIntervals(ulong min, ulong max, ulong count)
        {
            max++;
            var res = new List<Tuple<ulong,ulong, ulong>>();
            ulong step = (max - min) / count;
            if (step <= 0) return null;
            ulong mod = (max - min) % count;
            ulong begin = min;
            ulong end;
            for (ulong i = 0; i < count; i++)
            {
                end = begin + step;
                if (mod > 0) {
                    mod--;
                    end++;
                }
                var val = new Tuple<ulong,ulong, ulong>(i,begin, end-1);
                res.Add(val);
                begin = end;
            }
            return res;//should be unreachable
        }
    }
}
