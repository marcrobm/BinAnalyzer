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
        /// <param name="min"> The start of the range in which to generate Intervalls</param>
        /// <param name="max"> The end f the Range in which to generate Intervalls</param>
        /// <param name="count"> The number of intervalls to place in the range min->max</param>
        /// <returns>number,start,end</returns>
        public static IEnumerable<Tuple<long,long, long>> buildIntervals(long min, long max, long count)
        {
            max++;
            var res = new List<Tuple<long,long, long>>();
            long step = ((max - min) / count);
            if (step <= 0) return null;
            long mod = (max - min) % count; // ensured that the entire range will be covered
            long begin = min;
            long end;
            for (long intervall = 0; intervall < count; intervall++)
            {
                end = begin + step;
                if (mod > 0) {
                    mod--;
                    end++;
                }
                var val = new Tuple<long,long, long>(intervall,begin, end-1);
                res.Add(val);
                begin = end;
            }
            return res;
        }
    }
}
