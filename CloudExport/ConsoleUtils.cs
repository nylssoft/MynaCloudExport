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
using CloudExport.Services;

namespace CloudExport
{
    public static class ConsoleUtils
    {
        public static bool Verbose { get; set; } = false;

        public static string Read(string label)
        {
            WriteLabel(label);
            var ret = Console.ReadLine();
            return ret != null ? ret : "";
        }

        public static string ReadSecret(string label)
        {
            WriteLabel(label);
            var secret = string.Empty;
            ConsoleKey key;
            do
            {
                var keyInfo = Console.ReadKey(intercept: true);
                key = keyInfo.Key;
                if (key == ConsoleKey.Backspace && secret.Length > 0)
                {
                    Console.Write("\b \b");
                    secret = secret[0..^1];
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    Console.Write("*");
                    secret += keyInfo.KeyChar;
                }
            }
            while (key != ConsoleKey.Enter);
            Console.WriteLine();
            return secret;
        }

        public static void WriteError(string msg, params string[] args)
        {
            WritePrefix(Translate("ERROR"), ConsoleColor.White, Translate(msg, args), ConsoleColor.Red);
        }

        public static void WriteWarning(string msg, params string[] args)
        {
            WritePrefix(Translate("WARNING"), ConsoleColor.Red, Translate(msg, args));
        }

        public static void WriteInfo(string msg, params string[] args)
        {
            if (Verbose)
            {
                WritePrefix(Translate("INFO"), ConsoleColor.Yellow, Translate(msg, args));
            }
        }

        public static Dictionary<string, List<string>> ParseCommandLine(string[] args)
        {
            Dictionary<string, List<string>> ret = new();
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
                if (!ret.TryGetValue(key, out var l))
                {
                    l = new List<string>();
                    ret[key] = l;
                }
                l.Add(val);
            }
            return ret;
        }

        public static string Translate(string symbol, params string[] args)
        {
            if (args.Length == 0)
            {
                return RestClient.Translate(symbol);
            }
            return string.Format(RestClient.Translate(symbol), args);
        }

        // --- private

        private static void WritePrefix(string prefix, ConsoleColor col, string msg, ConsoleColor? bg = null)
        {
            var old = Console.ForegroundColor;
            ConsoleColor? oldbg = null;
            Console.ForegroundColor = col;
            if (bg != null)
            {
                oldbg = Console.BackgroundColor;
                Console.BackgroundColor = bg.Value;
            }
            Console.Write($"{prefix}: {msg}");
            Console.ForegroundColor = old;
            if (oldbg != null)
            {
                Console.BackgroundColor = oldbg.Value;
            }
            Console.WriteLine();
        }

        private static void WriteLabel(string label)
        {
            label = RestClient.Translate(label);
            if (!string.IsNullOrEmpty(label))
            {
                Console.Write(label);
                if (!label.EndsWith(":"))
                {
                    Console.Write(":");
                }
                Console.Write(" ");
            }
        }

    }
}
