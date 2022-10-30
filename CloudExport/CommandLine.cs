/*
    Myna Cloud Export
    Copyright (C) 2022 Niels Stockfleth

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
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
