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
using System.Globalization;

namespace CloudExport
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                CommandLine cmd = new(args);
                string locale = cmd.Get("locale", CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
                await CloudExport.Init(locale);
                if (cmd.Has("verbose"))
                {
                    ConsoleUtils.Verbose = true;
                }
                string? subcmd = cmd.Get();
                if (subcmd != "all" && subcmd != "documents" && subcmd != "notes" && subcmd != "diary")
                {
                    Console.WriteLine(ConsoleUtils.Translate("INFO_USAGE"));
                    Console.WriteLine("CloudExport {all|documents|notes|diary}");
                    Console.WriteLine(" [--exportdir=<directory>]");
                    Console.WriteLine(" [--user=<username>]");
                    Console.WriteLine(" [--password=<password>]");
                    Console.WriteLine(" [--code=<code>]");
                    Console.WriteLine(" [--key=<key>]");
                    Console.WriteLine(" [--locale={de-DE|en-US|...}]");
                    Console.WriteLine(" [--overwrite]");
                    Console.WriteLine(" [--verbose]");
                    return;
                }
                string exportDir = cmd.Get("exportdir", CloudExport.GetCloudExportDirectory());
                string? user = cmd.Get("user");
                string? pwd = cmd.Get("password");
                string? code = cmd.Get("code");
                string? key = cmd.Get("key");
                bool overwrit = cmd.Has("overwrite");
                var token = await CloudExport.AuthenticateAsync(user, pwd, code, locale);
                var userModel = await CloudExport.GetUserModel(token);
                exportDir = Path.Combine(exportDir, CloudExport.NormalizeName(userModel.name));
                if (key == null)
                {
                    key = ConsoleUtils.ReadSecret("LABEL_KEY");
                }
                if (subcmd == "all" || subcmd =="documents")
                {
                    await CloudExport.ExportDocumentsAsync(exportDir, token, key, overwrit, userModel.passwordManagerSalt);
                }
                if (subcmd == "all" || subcmd == "notes")
                {
                    await CloudExport.ExportNotesAsync(exportDir, token, key, overwrit, userModel.passwordManagerSalt);
                }
                if (subcmd == "all" || subcmd == "diary")
                {
                    await CloudExport.ExportDiaryAsync(exportDir, token, key, overwrit, userModel.passwordManagerSalt, new CultureInfo(locale));
                }
            }
            catch (Exception ex)
            {
                ConsoleUtils.WriteError(ex.Message);
            }
        }
    }
}