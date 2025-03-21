﻿/*
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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace CloudExport
{
    public static class CloudExport
    {
        private enum TransformType { Encrypt, Decrypt };
        private enum SecretType { Key, IV };

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
            if (name.Length > 40)
            {
                var shorthash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(name)))[..8]; // add only 8 characters (4 bytes, MD5 hash has 8 bytes)
                name = $"{name[..31]}_{shorthash}";
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

        public static async Task Init(string hostname, string locale)
        {
            await RestClient.InitAsync($"https://{hostname}", GetShortLocale(locale));
        }

        public static async Task<string> AuthenticateAsync(string? user, string? pwd, string? code, string locale)
        {
            var uuid = await GetUUID();
            ClientInfo clientInfo = new() { Name = "CloudExport", UUID = uuid };
            user ??= ConsoleUtils.Read("LABEL_NAME");
            pwd ??= ConsoleUtils.ReadSecret("LABEL_PWD");
            var authResult = await RestClient.AuthenticateAsync(user, pwd, clientInfo, GetShortLocale(locale));
            if (authResult.requiresPass2)
            {
                code ??= ConsoleUtils.ReadSecret("LABEL_SEC_KEY");
                authResult = await RestClient.AuthenticatePass2Async(authResult.token, code);
            }
            var token = authResult.token;
            return token;
        }

        public static async Task<string> AuthenticateTargetAsync(string? targetUser, string? targetPwd, string? targetCode, string locale)
        {
            var uuid = await GetUUID();
            ClientInfo clientInfo = new() { Name = "CloudExport", UUID = uuid };
            targetUser ??= ConsoleUtils.Read("LABEL_TARGET_NAME");
            targetPwd ??= ConsoleUtils.ReadSecret("LABEL_TARGET_PWD");
            var authResult = await RestClient.AuthenticateAsync(targetUser, targetPwd, clientInfo, GetShortLocale(locale));
            if (authResult.requiresPass2)
            {
                targetCode ??= ConsoleUtils.ReadSecret("LABEL_TARGET_SEC_KEY");
                authResult = await RestClient.AuthenticatePass2Async(authResult.token, targetCode);
            }
            var targetToken = authResult.token;
            return targetToken;
        }

        public static async Task<UserModel> GetUserModel(string token)
        {
            return await RestClient.GetUserAsync(token);
        }

        // --- export

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
                        if (!string.IsNullOrEmpty(doc.accessRole))
                        {
                            await File.WriteAllBytesAsync(docPath, content);
                        }
                        else
                        {
                            await File.WriteAllBytesAsync(docPath, Decrypt(content, key, salt));
                        }
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
            foreach (var n in notes)
            {
                var note = await RestClient.GetNoteAsync(token, n.id);
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

        public static async Task ExportDiaryAsync(string exportDir, string token, string key, bool overwrit, string salt, CultureInfo ci)
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

        public static async Task ExportContactsAsync(string exportDir, string token, string key, bool overwrit, string salt)
        {
            var encrypted = await RestClient.GetContactsAsync(token);
            var plainText = DecodeText(encrypted, key, salt);
            if (plainText == null) return;
            var contacts = JsonSerializer.Deserialize<Contacts>(plainText);
            if (contacts == null || contacts.items == null) return;
            var name = ConsoleUtils.Translate("CONTACTS");
            exportDir = Path.Combine(exportDir, name);
            Directory.CreateDirectory(exportDir);
            string csvFile = Path.Combine(exportDir, $"{name}.csv");
            string jsonFile = Path.Combine(exportDir, $"{name}.json");
            if (File.Exists(csvFile) || File.Exists(jsonFile))
            {
                if (overwrit)
                {
                    File.Delete(jsonFile);
                    File.Delete(csvFile);
                }
                else
                {
                    if (File.Exists(csvFile))
                    {
                        ConsoleUtils.WriteWarning("INFO_FILE_EXISTS_1", csvFile);
                    }
                    if (File.Exists(jsonFile))
                    {
                        ConsoleUtils.WriteWarning("INFO_FILE_EXISTS_1", jsonFile);
                    }
                    return;
                }
            }
            ConsoleUtils.WriteInfo("INFO_WRITE_FILE_1", jsonFile);
            File.WriteAllText(jsonFile, plainText, Encoding.Unicode);
            List<string> lines = new();
            StringBuilder sb = new();
            AddCSVColumn(sb, ConsoleUtils.Translate("NAME"));
            AddCSVColumn(sb, ConsoleUtils.Translate("ADDRESS"));
            AddCSVColumn(sb, ConsoleUtils.Translate("PHONE"));
            AddCSVColumn(sb, ConsoleUtils.Translate("BIRTHDAY"));
            AddCSVColumn(sb, ConsoleUtils.Translate("EMAIL"));
            AddCSVColumn(sb, ConsoleUtils.Translate("NOTE"), last: true);
            lines.Add(sb.ToString());
            foreach (ContactItem contactItem in contacts.items)
            {
                sb = new();
                AddCSVColumn(sb, contactItem.name);
                AddCSVColumn(sb, contactItem.address);
                AddCSVColumn(sb, contactItem.phone);
                AddCSVColumn(sb, contactItem.birthday);
                AddCSVColumn(sb, contactItem.email);
                AddCSVColumn(sb, contactItem.note, last: true);
                lines.Add(sb.ToString());
            }
            ConsoleUtils.WriteInfo("INFO_WRITE_FILE_1", csvFile);
            File.AppendAllLines(csvFile, lines, Encoding.Unicode);
        }

        public static async Task ExportPasswordItemsAsync(string exportDir, string? masterpwd, string username, string token, string key, bool overwrit, string salt, CultureInfo ci)
        {
            masterpwd ??= ConsoleUtils.ReadSecret("LABEL_MASTERPWD");
            var pwdDir = Path.Combine(exportDir, ConsoleUtils.Translate("PASSWORDS"));
            var keyDir = Path.Combine(pwdDir, ConsoleUtils.Translate("PASSWORD_KEYS"));
            Directory.CreateDirectory(pwdDir);
            Directory.CreateDirectory(keyDir);
            string repositoryFile = Path.Combine(pwdDir, $"{username}.pwd");
            if (File.Exists(repositoryFile))
            {
                if (overwrit)
                {
                    File.Delete(repositoryFile);
                    foreach (var fname in Directory.GetFiles(keyDir))
                    {
                        if (fname.EndsWith(".iv") || fname.EndsWith(".kv2"))
                        {
                            File.Delete(fname);
                        }
                    }
                }
                else
                {
                    ConsoleUtils.WriteWarning("INFO_FILE_EXISTS_1", repositoryFile);
                    return;
                }
            }
            var encrypted = await RestClient.GetPasswordFileAsync(token);
            var plainText = Decrypt(Convert.FromHexString(encrypted), key, salt);
            var pwdItems = JsonSerializer.Deserialize<List<PasswordItem>>(plainText);
            if (pwdItems == null) return;
            foreach (var pwd in pwdItems)
            {
                pwd.Password = DecodeText(pwd.Password, key, salt);
            }
            var id = Guid.NewGuid().ToString();
            var pattern = ci.DateTimeFormat.FullDateTimePattern;
            using (var rijAlg = Aes.Create())
            {
                WriteNewKey(keyDir, id, masterpwd);
                var iv = ReadSecret(keyDir, id, SecretType.IV);
                var encryptedKey = ReadSecret(keyDir, id, SecretType.Key);
                rijAlg.Key = TransformKey(encryptedKey, iv, masterpwd, TransformType.Decrypt);
                rijAlg.IV = iv;
                var cryptoTransform = rijAlg.CreateEncryptor();
                var doc = new XmlDocument();
                var rootElem = doc.CreateElement("PasswordRepository");
                doc.AppendChild(rootElem);
                AddElement(doc, rootElem, "Version", new Version(0, 5, 2).ToString());
                AddElement(doc, rootElem, "Name", ConsoleUtils.Translate("PASSWORDS"));
                AddElement(doc, rootElem, "Description", ConsoleUtils.Translate("PASSWORD_EXPORT_FROM_1", DateTime.Now.ToString(pattern, ci)));
                var passwordsElem = doc.CreateElement("Passwords");
                foreach (var pwd in pwdItems)
                {
                    var passwordElem = doc.CreateElement("Password");
                    AddElement(doc, passwordElem, "Id", Guid.NewGuid().ToString());
                    AddElement(doc, passwordElem, "Name", pwd.Name);
                    AddElement(doc, passwordElem, "Description", pwd.Description);
                    AddElement(doc, passwordElem, "Url", pwd.Url);
                    var cipherLogin = Encrypt(cryptoTransform, pwd.Login);
                    AddElement(doc, passwordElem, "CipherLogin", Convert.ToBase64String(cipherLogin));
                    var cipherPassword = Encrypt(cryptoTransform, pwd.Password);
                    AddElement(doc, passwordElem, "CipherPassword", Convert.ToBase64String(cipherPassword));
                    passwordsElem.AppendChild(passwordElem);
                }
                rootElem.AppendChild(passwordsElem);
                using (var ms = new MemoryStream())
                {
                    doc.Save(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    using (var fs = new FileStream(repositoryFile, FileMode.Create))
                    {
                        var header = new byte[6] { 23, 9, 78, 121, 108, 115 };
                        fs.Write(header, 0, 6);
                        var guid = new Guid(id);
                        fs.Write(guid.ToByteArray(), 0, 16);
                        Encrypt(cryptoTransform, ms, fs);
                        ConsoleUtils.WriteInfo("INFO_WRITE_FILE_1", repositoryFile);
                    }
                }
            }
        }

        // --- copy

        public static async Task CopyNotesAsync(string token, string key, string salt, string targetToken, string targetKey, string targetSalt)
        {
            var notes = await RestClient.GetNotesAsync(token);
            foreach (var n in notes)
            {
                var note = await RestClient.GetNoteAsync(token, n.id);
                try
                {
                    var title = DecodeText(note.title, key, salt);
                    ConsoleUtils.WriteInfo("INFO_COPY_NOTE_1", title);
                    var content = DecodeText(note.content, key, salt);
                    var encodedTitle = EncodeText(title, targetKey, targetSalt);
                    var encodedContent = EncodeText(content, targetKey, targetSalt);
                    var nodeid = await RestClient.CreateNewNoteAsync(targetToken, encodedTitle);
                    await RestClient.UpdateNoteAsync(targetToken, nodeid, encodedTitle, encodedContent);
                }
                catch (Exception ex)
                {
                    ConsoleUtils.WriteError(ex.Message);
                }
            }
        }

        public static async Task CopyDiaryAsync(string token, string key, string salt, string targetToken, string targetKey, string targetSalt, CultureInfo ci)
        {
            var all = await RestClient.GetAllDiaryEntriesAsync(token);
            foreach (var dt in all)
            {
                ConsoleUtils.WriteInfo("INFO_COPY_ENTRY_1", dt.ToString(ci.DateTimeFormat.ShortDatePattern));
                var entry = await RestClient.GetDiaryAsync(token, dt.Year, dt.Month, dt.Day);
                try
                {
                    var txt = DecodeText(entry.entry, key, salt).Trim();
                    if (!string.IsNullOrEmpty(txt))
                    {
                        var encodedTxt = EncodeText(txt, targetKey, targetSalt);
                        await RestClient.SaveDiaryAsync(targetToken, dt.Year, dt.Month, dt.Day, encodedTxt);
                    }
                }
                catch (Exception ex)
                {
                    ConsoleUtils.WriteError(ex.Message);
                }
            }
        }

        public static async Task CopyContactsAsync(string token, string key, string salt, string targetToken, string targetKey, string targetSalt)
        {
            ConsoleUtils.WriteInfo("INFO_COPY_CONTACTS");
            var encrypted = await RestClient.GetContactsAsync(token);
            var plainText = DecodeText(encrypted, key, salt);
            if (plainText == null) return;
            var encodedText = EncodeText(plainText, targetKey, targetSalt);
            await RestClient.SaveContactsAsync(targetToken, encodedText);
        }

        public static async Task CopyPasswordItemsAsync(string token, string key, string salt, string targetToken, string targetKey, string targetSalt)
        {
            ConsoleUtils.WriteInfo("INFO_COPY_PASSWORDS");
            var encrypted = await RestClient.GetPasswordFileAsync(token);
            var plainData = Decrypt(Convert.FromHexString(encrypted), key, salt);
            var pwdItems = JsonSerializer.Deserialize<List<PasswordItem>>(plainData);
            if (pwdItems == null) return;
            foreach (var pwd in pwdItems)
            {
                pwd.Password = DecodeText(pwd.Password, key, salt);
                pwd.Password = EncodeText(pwd.Password, targetKey, targetSalt);
            }
            plainData = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(pwdItems));
            var encodedData = Encrypt(plainData, targetKey, targetSalt);
            await RestClient.SavePasswordFileAsync(targetToken, Convert.ToHexString(encodedData));
        }

        public static async Task CopyDocumentsAsync(string token, string key, string salt, string targetToken, string targetKey, string targetSalt)
        {
            Queue<DocumentItem?> queue = new();
            queue.Enqueue(null);
            var targetFolderIdmap = new Dictionary<long, long>();
            while (queue.Any())
            {
                var currentFolder = queue.Dequeue();
                var nextid = currentFolder?.id;
                var items = await RestClient.GetDocumentItemsAsync(token, nextid);
                if (currentFolder == null)
                {
                    currentFolder = items.First(i => i.type == "Volume");
                    var currentTargetFolder = await RestClient.CreateVolume(targetToken, currentFolder.name);
                    targetFolderIdmap[currentFolder.id] = currentTargetFolder.id;
                }
                else if (currentFolder.parentId != null)
                {
                    var targetParentId = targetFolderIdmap[currentFolder.parentId.Value];
                    var currentTargetFolder = await RestClient.CreateFolder(targetToken, targetParentId, currentFolder.name);
                    targetFolderIdmap[currentFolder.id] = currentTargetFolder.id;
                }
                var folders = items.Where(i => i.parentId == currentFolder.id && i.type == "Folder");
                foreach (var folder in folders)
                {
                    queue.Enqueue(folder);
                }
                var documents = items.Where(i => i.parentId == currentFolder.id && i.type == "Document");
                foreach (var doc in documents)
                {
                    ConsoleUtils.WriteInfo("INFO_COPY_DOCUMENT_1", doc.name);
                    var content = await RestClient.DownloadDocumentAsync(token, doc.id);
                    try
                    {
                        var targetFolderId = targetFolderIdmap[currentFolder.id];
                        if (!string.IsNullOrEmpty(doc.accessRole))
                        {
                            await RestClient.UploadDocument(targetToken, targetFolderId, doc.name, content);
                        }
                        else
                        {
                            var plainData = Decrypt(content, key, salt);
                            var encodedData = Encrypt(plainData, targetKey, targetSalt);
                            await RestClient.UploadDocument(targetToken, targetFolderId, doc.name, encodedData);
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleUtils.WriteError(ex.Message);
                    }
                }
            }
        }

        // --- private

        private static void AddCSVColumn(StringBuilder sb, string? val, bool last = false)
        {
            if (val != null)
            {
                sb.Append($"\"{val}\"");
            }
            if (!last)
            {
                sb.Append(',');
            }
        }

        private static byte[] ReadSecret(string keyDirectory, string id, SecretType st)
        {
            var keyfile = keyDirectory + "\\";
            keyfile += st == SecretType.Key ? $"{id}.kv2" : $"{id}.iv";
            if (!File.Exists(keyfile))
            {
                throw new FileNotFoundException($"Key file '{keyfile}' not found.");
            }
            return Read(keyfile);
        }

        private static byte[] Read(string filename)
        {
            const int CHUNK_SIZE = 8192;
            using (var ms = new MemoryStream())
            {
                using (var fs = new FileStream(filename, FileMode.Open))
                {
                    int readCount;
                    var buffer = new byte[CHUNK_SIZE];
                    while ((readCount = fs.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        ms.Write(buffer, 0, readCount);
                    }
                }
                return ms.ToArray();
            }
        }

        private static void WriteNewKey(string keyDirectory, string id, string securePassword)
        {
            using (var rijAlg = Aes.Create())
            {
                rijAlg.KeySize = 256;
                rijAlg.GenerateIV();
                var ivFile = $"{keyDirectory}\\{id}.iv";
                using (var fsiv = new FileStream(ivFile, FileMode.Create))
                {
                    fsiv.Write(rijAlg.IV, 0, rijAlg.IV.Length);
                }
                ConsoleUtils.WriteInfo("INFO_WRITE_FILE_1", ivFile);
                rijAlg.GenerateKey();
                var encryptedKey = TransformKey(rijAlg.Key, rijAlg.IV, securePassword, TransformType.Encrypt);
                var keyFile = $"{keyDirectory}\\{id}.kv2";
                using (var fskv = new FileStream(keyFile, FileMode.Create))
                {
                    fskv.Write(encryptedKey, 0, encryptedKey.Length);
                }
                ConsoleUtils.WriteInfo("INFO_WRITE_FILE_1", keyFile);
                Array.Clear(encryptedKey, 0, encryptedKey.Length);
            }
        }

        private static byte[] TransformKey(byte[] key, byte[] iv, string pwd, TransformType t)
        {
            using (var sha265 = SHA256.Create())
            {
                using (var rijAlg = Aes.Create())
                {
                    rijAlg.KeySize = 256;
                    var bytes = Encoding.UTF8.GetBytes(pwd);
                    rijAlg.Key = sha265.ComputeHash(bytes);
                    Array.Clear(bytes, 0, bytes.Length);
                    rijAlg.IV = iv;
                    using (var destStream = new MemoryStream())
                    {
                        using (var sourceStream = new MemoryStream(key))
                        {
                            switch (t)
                            {
                                case TransformType.Encrypt:
                                    Encrypt(rijAlg.CreateEncryptor(), sourceStream, destStream);
                                    break;
                                case TransformType.Decrypt:
                                    Decrypt(rijAlg.CreateDecryptor(), sourceStream, destStream);
                                    break;
                                default:
                                    break;
                            }
                        }
                        return destStream.ToArray();
                    }
                }
            }
        }

        private static void Decrypt(ICryptoTransform cryptoTransform, Stream input, Stream output)
        {
            using (var cs = new CryptoStream(input, cryptoTransform, CryptoStreamMode.Read))
            {
                cs.CopyTo(output);
            }
        }

        private static void Encrypt(ICryptoTransform cryptoTransform, Stream input, Stream output)
        {
            using (var cs = new CryptoStream(output, cryptoTransform, CryptoStreamMode.Write))
            {
                input.CopyTo(cs);
            }
        }

        private static byte[] Encrypt(ICryptoTransform cryptoTransform, string plainText)
        {
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write))
                {
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                    return ms.ToArray();
                }
            }
        }

        private static void AddElement(XmlDocument xmldoc, XmlElement parentelem, string elemname, string elemvalue)
        {
            var childelem = xmldoc.CreateElement(elemname);
            childelem.InnerText = elemvalue;
            parentelem.AppendChild(childelem);
        }

        private static string GetShortLocale(string locale)
        {
            var idx = locale.IndexOf("-");
            if (idx > 0)
            {
                locale = locale[..idx];
            }
            locale = locale.ToLowerInvariant();
            return locale;
        }

        private static async Task<string> GetUUID()
        {
            var uuidFile = Path.Combine(GetCloudExportDirectory(), "clientinfo.txt");
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
            using (var cipher = new AesGcm(key, tag.Length))
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

        private static byte[] Encrypt(byte[] data, string cryptKey, string salt)
        {
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(
                cryptKey,
                Encoding.UTF8.GetBytes(salt),
                1000,
                HashAlgorithmName.SHA256,
                256 / 8);
            var iv = new byte[12];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }
            var encoded = new byte[data.Length];
            var tag = new byte[16];
            using (var cipher = new AesGcm(key, tag.Length))
            {
                cipher.Encrypt(iv, data, encoded, tag);
            }
            var ret = new byte[iv.Length + encoded.Length + tag.Length];
            iv.CopyTo(ret, 0);
            encoded.CopyTo(ret, iv.Length);
            tag.CopyTo(ret, iv.Length + encoded.Length);
            return ret;
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

        private static string EncodeText(string text, string cryptKey, string salt)
        {
            var data = Encoding.UTF8.GetBytes(text);
            var encrypted = Encrypt(data, cryptKey, salt);
            return Convert.ToHexString(encrypted);
        }

    }
}
