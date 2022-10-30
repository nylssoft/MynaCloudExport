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
                string? subcmd = cmd.Get("");
                if (string.IsNullOrEmpty(subcmd) || subcmd != "all" && subcmd != "documents" && subcmd != "notes" && subcmd != "diary")
                {
                    Console.WriteLine(ConsoleUtils.Translate("INFO_USAGE"));
                    Console.WriteLine("CloudExport [all|documents|notes|diary]");
                    Console.WriteLine(" [--exportdir=<directory>]");
                    Console.WriteLine(" [--user=<username>]");
                    Console.WriteLine(" [--password=<password>]");
                    Console.WriteLine(" [--code=<code>]");
                    Console.WriteLine(" [--key=<key>]");
                    Console.WriteLine(" [--locale={de-DE|en-US}]");
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
                    await CloudExport.ExportDiaryAsyc(exportDir, token, key, overwrit, userModel.passwordManagerSalt, new CultureInfo(locale));
                }
            }
            catch (Exception ex)
            {
                ConsoleUtils.WriteError(ex.Message);
            }
        }
    }
}