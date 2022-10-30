using CloudExport.Services;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CloudExport
{
    public static class CloudExport
    {
        public static string GetCloudExportDirectory()
        {
            var destDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cloudexport");
            Directory.CreateDirectory(destDir);
            return destDir;
        }

        public static string NormalizeName(string name)
        {
            var ext = Path.GetExtension(name);
            name = Path.GetFileNameWithoutExtension(name);
            if (name.Length > 32)
            {
                name = name[..32];
            }
            StringBuilder sb = new();
            int idx = 0;
            foreach (var c in name)
            {
                if (c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9'
                    || c == '-' || c == ' ' && idx > 0 || c == '_' || c == '.' && idx > 0)
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append($"%{Convert.ToHexString(new[] { Convert.ToByte(c) })}");
                }
                idx++;
            }
            return $"{sb}{ext}";
        }

        public static async Task Init(string locale)
        {
            await RestClient.InitAsync("https://www.stockfleth.eu", GetShortLocale(locale));
        }

        public static async Task<string> AuthenticateAsync(string? user, string? pwd, string? code, string locale)
        {
            var uuid = await GetUUID();
            ClientInfo clientInfo = new() { Name = "CloudExport", UUID = uuid };
            if (user == null)
            {
                user = ConsoleUtils.Read("LABEL_NAME");
            }
            if (pwd == null)
            {
                pwd = ConsoleUtils.ReadSecret("LABEL_PWD");
            }
            var authResult = await RestClient.AuthenticateAsync(user, pwd, clientInfo, GetShortLocale(locale));
            if (authResult.requiresPass2)
            {
                if (code == null)
                {
                    code = ConsoleUtils.ReadSecret("LABEL_SEC_KEY");
                }
                authResult = await RestClient.AuthenticatePass2Async(authResult.token, code);
            }
            var token = authResult.token;
            return token;
        }

        public static async Task<UserModel> GetUserModel(string token)
        {
            return await RestClient.GetUserAsync(token);
        }

        public static async Task ExportDocumentsAsync(string exportDir, string token, string key, bool overwrit, string salt)
        {
            var destDir = exportDir;
            Queue<DocumentItem?> queue = new();
            queue.Enqueue(null);
            while (queue.Any())
            {
                var currentFolder = queue.Dequeue();
                var nextid = currentFolder?.id;
                var items = await RestClient.GetDocumentItemsAsync(token, nextid);
                if (currentFolder == null)
                {
                    currentFolder = items.First(i => i.type == "Volume");
                    Directory.CreateDirectory(GetFilename(destDir, items, currentFolder));
                }
                var folders = items.Where(i => i.parentId == currentFolder.id && i.type == "Folder");
                foreach (var folder in folders)
                {
                    Directory.CreateDirectory(GetFilename(destDir, items, folder));
                    queue.Enqueue(folder);
                }
                var documents = items.Where(i => i.parentId == currentFolder.id && i.type == "Document");
                foreach (var doc in documents)
                {
                    ConsoleUtils.WriteInfo("INFO_READ_DOCUMENT_1", doc.name);
                    var content = await RestClient.DownloadDocumentAsync(token, doc.id);
                    try
                    {
                        var docPath = GetFilename(destDir, items, doc);
                        if (File.Exists(docPath) && !overwrit)
                        {
                            ConsoleUtils.WriteWarning("INFO_FILE_EXISTS_1", docPath);
                            continue;
                        }
                        ConsoleUtils.WriteInfo("INFO_WRITE_FILE_1_2", docPath, $"{doc.size}");
                        await File.WriteAllBytesAsync(docPath, Decrypt(content, key, salt));
                    }
                    catch (Exception ex)
                    {
                        ConsoleUtils.WriteError(ex.Message);
                    }
                }
            }
        }

        public static async Task ExportNotesAsync(string exportDir, string token, string key, bool overwrit, string salt)
        {
            exportDir = Path.Combine(exportDir, ConsoleUtils.Translate("NOTES"));
            Directory.CreateDirectory(exportDir);
            var notes = await RestClient.GetNotesAsync(token);
            if (notes != null)
            {
                foreach (var n in notes)
                {
                    var note = await RestClient.GetNoteAsync(token, n.id);
                    if (note == null) continue;
                    var title = DecodeText(note.title, key, salt);
                    ConsoleUtils.WriteInfo("INFO_READ_NOTE_1", title);
                    var notePath = Path.Combine(exportDir, $"{NormalizeName(title)}.txt");
                    if (File.Exists(notePath) && !overwrit)
                    {
                        ConsoleUtils.WriteWarning("INFO_FILE_EXISTS_1", notePath);
                        continue;
                    }
                    ConsoleUtils.WriteInfo("INFO_WRITE_FILE_1", notePath);
                    try
                    {
                        await File.WriteAllTextAsync(notePath, DecodeText(note.content, key, salt), Encoding.Unicode);
                    }
                    catch (Exception ex)
                    {
                        ConsoleUtils.WriteError(ex.Message);
                    }
                }
            }
        }

        public static async Task ExportDiaryAsyc(string exportDir, string token, string key, bool overwrit, string salt, CultureInfo ci)
        {
            exportDir = Path.Combine(exportDir, ConsoleUtils.Translate("DIARY"));
            Directory.CreateDirectory(exportDir);
            HashSet<int> visitedYears = new();
            HashSet<int> skipYears = new();
            var pattern = ci.DateTimeFormat.MonthDayPattern;
            var all = await RestClient.GetAllDiaryEntriesAsync(token);
            foreach (var dt in all)
            {
                if (skipYears.Contains(dt.Year)) continue;
                ConsoleUtils.WriteInfo("INFO_READ_ENTRY_1", dt.ToString(ci.DateTimeFormat.ShortDatePattern));
                var entry = await RestClient.GetDiaryAsync(token, dt.Year, dt.Month, dt.Day);
                if (entry == null) continue;
                var datestr = dt.ToString("yyyy", CultureInfo.InvariantCulture);
                var entryPath = Path.Combine(exportDir, $"{datestr}.txt");
                try
                {
                    var header = dt.ToString(pattern, ci);
                    var headerline = new string('-', header.Length);
                    var txt = DecodeText(entry.entry, key, salt).Trim();
                    var content = $"{header}\n{headerline}\n{txt}\n\n";
                    if (!visitedYears.Contains(dt.Year))
                    {
                        if (File.Exists(entryPath) && !overwrit)
                        {
                            ConsoleUtils.WriteWarning("INFO_FILE_EXISTS_1", entryPath);
                            skipYears.Add(dt.Year);
                            continue;
                        }
                        ConsoleUtils.WriteInfo("INFO_WRITE_FILE_1", entryPath);
                        await File.WriteAllTextAsync(entryPath, content, Encoding.Unicode);
                    }
                    else
                    {
                        await File.AppendAllTextAsync(entryPath, content, Encoding.Unicode);
                    }
                    visitedYears.Add(dt.Year);
                }
                catch (Exception ex)
                {
                    ConsoleUtils.WriteError(ex.Message);
                }
            }
        }

        // --- private

        private static string GetShortLocale(string locale)
        {
            var idx = locale.IndexOf("-");
            if (idx > 0)
            {
                locale = locale[..(idx)];
            }
            locale = locale.ToLowerInvariant();
            return locale;
        }

        private static async Task<string> GetUUID()
        {
            var uuidFile = Path.Combine(GetCloudExportDirectory(), "uuid.txt");
            string uuid;
            if (!File.Exists(uuidFile))
            {
                uuid = Guid.NewGuid().ToString();
                await File.WriteAllTextAsync(uuidFile, uuid);
            }
            else
            {
                uuid = await File.ReadAllTextAsync(uuidFile);
            }
            return uuid;
        }

        private static string GetFilename(string destDir, List<DocumentItem> items, DocumentItem current)
        {
            List<string> path = new();
            path.Add(NormalizeName(current.name));
            while (current.parentId != null)
            {
                current = items.First(i => i.id == current.parentId);
                path.Add(NormalizeName(current.name));
            }
            path.Add(destDir);
            path.Reverse();
            return Path.Combine(path.ToArray());
        }

        private static byte[] Decrypt(byte[] data, string cryptKey, string salt)
        {
            byte[] iv = data[0..12];
            byte[] chipherText = data[12..^16];
            byte[] tag = data[^16..];
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(
                cryptKey,
                Encoding.UTF8.GetBytes(salt),
                1000,
                HashAlgorithmName.SHA256,
                256 / 8);
            byte[] plainText = new byte[chipherText.Length];
            using (var cipher = new AesGcm(key))
            {
                try
                {
                    cipher.Decrypt(iv, chipherText, tag, plainText);
                }
                catch
                {
                    throw new ArgumentException(ConsoleUtils.Translate("ERROR_DECRYPT"));
                }
            }
            return plainText;
        }

        private static string DecodeText(string encrypted, string cryptKey, string salt)
        {
            if (string.IsNullOrEmpty(encrypted))
            {
                return "";
            }
            var decoded = Decrypt(Convert.FromHexString(encrypted), cryptKey, salt);
            return Encoding.UTF8.GetString(decoded);
        }
    }
}
