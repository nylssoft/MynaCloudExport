/*
    Myna Cloud Export
    Copyright (C) 2022-2025 Niels Stockfleth

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
                string hostname = cmd.GetSingleParameter("hostname", "www.stockfleth.eu");
                string locale = cmd.GetSingleParameter("locale", CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
                if (!locale.StartsWith("de-") && !locale.StartsWith("en-"))
                {
                    locale = "en-US";
                }
                await CloudExport.Init(hostname, locale);
                ConsoleUtils.Verbose = cmd.HasParameter("verbose");
                var subcommand = cmd.GetSingleOrDefaultSubcommand();
                if (subcommand == null)
                {
                    Console.WriteLine(ConsoleUtils.Translate("INFO_USAGE"));
                    Console.WriteLine("CloudExport {all|documents|notes|diary|passwords|contacts}");
                    Console.WriteLine(" [-hostname <hostname>]");
                    Console.WriteLine(" [-exportdir <directory>]");
                    Console.WriteLine(" [-user <username>]");
                    Console.WriteLine(" [-password <password>]");
                    Console.WriteLine(" [-code <code>]");
                    Console.WriteLine(" [-key <key>]");
                    Console.WriteLine(" [-masterpassword <masterpassword>]");
                    Console.WriteLine(" [-locale {de-DE|en-US|...}]");
                    Console.WriteLine(" [-overwrite]");
                    Console.WriteLine(" [-verbose]");
                    Console.WriteLine();
                    Console.WriteLine("CloudExport copy");
                    Console.WriteLine(" [-hostname <hostname>]");
                    Console.WriteLine(" [-user <username>] [-password <password>] [-code <code>] [-key <key>]");
                    Console.WriteLine(" [-targetuser <username>] [-targetpassword <password>] [-targetcode <code>] [-targetkey <key>]");
                    Console.WriteLine(" [-locale {de-DE|en-US|...}]");
                    Console.WriteLine(" [-verbose]");
                    return;
                }
                string exportDir = cmd.GetSingleParameter("exportdir", CloudExport.GetCloudExportDirectory());
                string? user = cmd.GetSingleOrDefaultParameter("user");
                string? pwd = cmd.GetSingleOrDefaultParameter("password");
                string? code = cmd.GetSingleOrDefaultParameter("code");
                string? key = cmd.GetSingleOrDefaultParameter("key");
                string? masterpwd = cmd.GetSingleOrDefaultParameter("masterpassword");
                bool overwrit = cmd.HasParameter("overwrite");
                var token = await CloudExport.AuthenticateAsync(user, pwd, code, locale);
                var userModel = await CloudExport.GetUserModel(token);
                exportDir = Path.Combine(exportDir, CloudExport.NormalizeName(userModel.name));
                key ??= ConsoleUtils.ReadSecret("LABEL_KEY");
                if (subcommand == "copy")
                {
                    await Copy(cmd, token, key, userModel, locale);
                }
                if (subcommand == "all" || subcommand == "documents")
                {
                    if (userModel.hasDocuments) 
                    {
                        await CloudExport.ExportDocumentsAsync(exportDir, token, key, overwrit, userModel.passwordManagerSalt);
                    }
                    else
                    {
                        ConsoleUtils.WriteInfo("NO_DOCUMENTS");
                    }
                }
                if (subcommand == "all" || subcommand == "notes")
                {
                    if (userModel.hasNotes)
                    {
                        await CloudExport.ExportNotesAsync(exportDir, token, key, overwrit, userModel.passwordManagerSalt);
                    }
                    else
                    {
                        ConsoleUtils.WriteInfo("NO_NOTES");
                    }
                }
                if (subcommand == "all" || subcommand == "diary")
                {
                    if (userModel.hasDiary)
                    {
                        await CloudExport.ExportDiaryAsync(exportDir, token, key, overwrit, userModel.passwordManagerSalt, new CultureInfo(locale));
                    }
                    else
                    {
                        ConsoleUtils.WriteInfo("NO_DIARY");
                    }
                }
                if (subcommand == "all" || subcommand == "passwords")
                {
                    if (userModel.hasPasswordManagerFile)
                    {
                        await CloudExport.ExportPasswordItemsAsync(exportDir, masterpwd, CloudExport.NormalizeName(userModel.name), token, key, overwrit, userModel.passwordManagerSalt, new CultureInfo(locale));
                    }
                    else
                    {
                        ConsoleUtils.WriteInfo("NO_PASSWORDS");
                    }
                }
                if (subcommand == "all" || subcommand == "contacts")
                {
                    if (userModel.hasContacts)
                    {
                        await CloudExport.ExportContactsAsync(exportDir, token, key, overwrit, userModel.passwordManagerSalt);
                    }
                    else
                    {
                        ConsoleUtils.WriteInfo("NO_CONTACTS");
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleUtils.WriteError(ex.Message);
            }
        }

        private static async Task Copy(CommandLine cmd, string token, string key, UserModel userModel, string locale)
        {
            string? targetUser = cmd.GetSingleOrDefaultParameter("targetuser");
            string? targetPwd = cmd.GetSingleOrDefaultParameter("targetpassword");
            string? targetCode = cmd.GetSingleOrDefaultParameter("targetcode");
            string? targetKey = cmd.GetSingleOrDefaultParameter("targetkey");
            var targetToken = await CloudExport.AuthenticateTargetAsync(targetUser, targetPwd, targetCode, locale);
            var targetUserModel = await CloudExport.GetUserModel(targetToken);
            targetKey ??= ConsoleUtils.ReadSecret("LABEL_TARGET_KEY");
            if (userModel.hasNotes)
            {
                if (targetUserModel.hasNotes)
                {
                    ConsoleUtils.WriteWarning("INFO_TARGET_NOTES_EXIST");
                }
                else
                {
                    await CloudExport.CopyNotesAsync(token, key, userModel.passwordManagerSalt, targetToken, targetKey, targetUserModel.passwordManagerSalt);
                }
            }
            if (userModel.hasDiary)
            {
                if (targetUserModel.hasDiary)
                {
                    ConsoleUtils.WriteWarning("INFO_TARGET_DIARY_EXIST");
                }
                else
                {
                    await CloudExport.CopyDiaryAsync(token, key, userModel.passwordManagerSalt, targetToken, targetKey, targetUserModel.passwordManagerSalt, new CultureInfo(locale));
                }
            }
            if (userModel.hasContacts)
            {
                if (targetUserModel.hasContacts)
                {
                    ConsoleUtils.WriteWarning("INFO_TARGET_CONTACTS_EXIST");
                }
                else
                {
                    await CloudExport.CopyContactsAsync(token, key, userModel.passwordManagerSalt, targetToken, targetKey, targetUserModel.passwordManagerSalt);
                }
            }
            if (userModel.hasPasswordManagerFile)
            {
                if (targetUserModel.hasPasswordManagerFile)
                {
                    ConsoleUtils.WriteWarning("INFO_TARGET_PASSWORDS_EXIST");
                }
                else
                {
                    await CloudExport.CopyPasswordItemsAsync(token, key, userModel.passwordManagerSalt, targetToken, targetKey, targetUserModel.passwordManagerSalt);
                }
            }
            if (userModel.hasDocuments)
            {
                if (targetUserModel.hasDocuments)
                {
                    ConsoleUtils.WriteWarning("INFO_TARGET_DOCUMENTS_EXIST");
                }
                else
                {
                    await CloudExport.CopyDocumentsAsync(token, key, userModel.passwordManagerSalt, targetToken, targetKey, targetUserModel.passwordManagerSalt);
                }
            }
        }

    }
}