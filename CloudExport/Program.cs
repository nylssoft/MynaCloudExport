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
                string locale = cmd.GetSingleParameter("locale", CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
                if (!locale.StartsWith("de-") && !locale.StartsWith("en-"))
                {
                    locale = "en-US";
                }
                await CloudExport.Init(locale);
                ConsoleUtils.Verbose = cmd.HasParameter("verbose");
                var subcommand = cmd.GetSingleOrDefaultSubcommand();
                if (subcommand == null)
                {
                    Console.WriteLine(ConsoleUtils.Translate("INFO_USAGE"));
                    Console.WriteLine("CloudExport {all|documents|notes|diary}");
                    Console.WriteLine(" [-exportdir <directory>]");
                    Console.WriteLine(" [-user <username>]");
                    Console.WriteLine(" [-password <password>]");
                    Console.WriteLine(" [-code <code>]");
                    Console.WriteLine(" [-key <key>]");
                    Console.WriteLine(" [-locale {de-DE|en-US|...}]");
                    Console.WriteLine(" [-overwrite]");
                    Console.WriteLine(" [-verbose]");
                    return;
                }
                string exportDir = cmd.GetSingleParameter("exportdir", CloudExport.GetCloudExportDirectory());
                string? user = cmd.GetSingleOrDefaultParameter("user");
                string? pwd = cmd.GetSingleOrDefaultParameter("password");
                string? code = cmd.GetSingleOrDefaultParameter("code");
                string? key = cmd.GetSingleOrDefaultParameter("key");
                bool overwrit = cmd.HasParameter("overwrite");
                var token = await CloudExport.AuthenticateAsync(user, pwd, code, locale);
                var userModel = await CloudExport.GetUserModel(token);
                exportDir = Path.Combine(exportDir, CloudExport.NormalizeName(userModel.name));
                if (key == null)
                {
                    key = ConsoleUtils.ReadSecret("LABEL_KEY");
                }
                if (subcommand == "all" || subcommand == "documents")
                {
                    await CloudExport.ExportDocumentsAsync(exportDir, token, key, overwrit, userModel.passwordManagerSalt);
                }
                if (subcommand == "all" || subcommand == "notes")
                {
                    await CloudExport.ExportNotesAsync(exportDir, token, key, overwrit, userModel.passwordManagerSalt);
                }
                if (subcommand == "all" || subcommand == "diary")
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