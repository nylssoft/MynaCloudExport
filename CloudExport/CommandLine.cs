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

        public bool HasParameter(string param)
        {
            return paramDict.ContainsKey(param);
        }

        public string? GetSingleOrDefaultParameter(string param)
        {
            paramDict.TryGetValue(param, out var l);
            if (l != null && l.Count > 0)
            {
                return l[0];
            }
            return null;
        }

        public string GetSingleParameter(string param, string def)
        {
            var ret = GetSingleOrDefaultParameter(param);
            return ret == null ? def : ret;
        }

        public List<string>? GetParameters(string param)
        {
            paramDict.TryGetValue(param, out var l);
            return l;
        }

        public string? GetSingleOrDefaultSubcommand()
        {
            var l = GetSubcommands();
            if (l != null && l.Count() == 1)
            {
                return l[0];
            }
            return null;
        }

        public List<string>? GetSubcommands()
        {
            return GetParameters("");            
        }

        private void ParseCommandLine(string[] args)
        {
            string? lastParam = null;
            List<string> subCommands = new();
            List<string> lastArgs = new();
            foreach (var arg in args)
            {
                var param = arg;
                if (param.StartsWith("-") && param.Length > 1 && param[1] != '-')
                {
                    if (lastParam != null)
                    {
                        paramDict[lastParam] = lastArgs;
                        lastArgs = new();
                    }
                    lastParam = arg[1..].ToLowerInvariant();
                }
                else
                {
                    if (param.StartsWith("-") && arg.Length > 1)
                    {
                        param = arg[1..];
                    }
                    if (lastParam == null)
                    {
                        subCommands.Add(param.ToLowerInvariant());
                    }
                    else
                    {
                        lastArgs.Add(param);
                    }
                }
            }
            if (lastParam != null)
            {
                paramDict[lastParam] = lastArgs;
            }
            if (subCommands.Count > 0)
            {
                paramDict[""] = subCommands;
            }
        }
    }
}
