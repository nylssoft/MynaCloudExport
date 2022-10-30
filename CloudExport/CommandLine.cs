using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudExport
{
    public class CommandLine
    {
        private readonly Dictionary<string, List<string>> paramDict;

        public CommandLine(string[] args)
        {
             paramDict = new Dictionary<string, List<string>>();
            ParseCommandLine(args);
        }

        public bool Has(string param)
        {
            return paramDict.ContainsKey(param);
        }

        public string? Get(string param)
        {
            paramDict.TryGetValue(param, out var l);
            if (l != null && l.Count > 0)
            {
                return l[0];
            }
            return null;
        }

        public string Get(string param, string def)
        {
            var ret = Get(param);
            return ret == null ? def : ret;
        }

        private void ParseCommandLine(string[] args)
        {
            foreach (var arg in args)
            {
                var key = "";
                var val = "";
                var idx1 = arg.IndexOf("--");
                if (idx1 >= 0)
                {
                    var idx2 = arg.IndexOf("=", idx1 + 2);
                    if (idx2 > 0)
                    {
                        key = arg[(idx1 + 2)..(idx2)].ToLowerInvariant();
                        val = arg[(idx2 + 1)..];
                    }
                    else
                    {
                        key = arg[(idx1 + 2)..].ToLowerInvariant();
                    }
                }
                else
                {
                    val = arg;
                }
                if (!paramDict.TryGetValue(key, out var l))
                {
                    l = new List<string>();
                    paramDict[key] = l;
                }
                l.Add(val);
            }
        }
    }
}
