﻿/*
    Myna Cloud Export
    Copyright (C) 2022-2023 Niels Stockfleth

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
using System.Net.Http.Json;
using System.Text.Json;

namespace CloudExport.Services
{
    public class RestClient
    {
        private static HttpClient? httpClient = null;

        private static Dictionary<string, string>? translateMap = null;
        
        private const string ISO8601_DATEFORMAT = "yyyy-MM-dd'T'HH:mm:ss.fffK";

        public static async Task InitAsync(string cloudUrl, string locale)
        {
            if (httpClient == null || httpClient.BaseAddress != new Uri(cloudUrl))
            {
                httpClient = new HttpClient
                {
                    BaseAddress = new Uri(cloudUrl)
                };
            }
            if (!string.IsNullOrEmpty(locale))
            {
                EnsureRateLimit();
                httpClient.DefaultRequestHeaders.Remove("token");
                var localeUrl = await httpClient.GetFromJsonAsync<string>($"api/pwdman/locale/url/{locale}");
                translateMap = await httpClient.GetFromJsonAsync<Dictionary<string, string>>(localeUrl);
                if (translateMap == null) throw new ArgumentException("Invalid response");
                var localeFile = Path.Combine(AppContext.BaseDirectory, "Locale", $"{locale}.json");
                if (File.Exists(localeFile))
                {
                    var txt = await File.ReadAllTextAsync(localeFile);
                    var map = JsonSerializer.Deserialize<Dictionary<string,string>>(txt);
                    if (map != null)
                    {
                        foreach (var elem in map)
                        {
                            translateMap.Add(elem.Key, elem.Value);
                        }
                    }
                }
            }
        }

        public static string Translate(string symbol)
        {
            if (translateMap != null)
            {
                var arr = symbol.Split(':');
                if (arr.Length > 1)
                {
                    if (translateMap.TryGetValue(arr[0], out var fmt))
                    {
                        for (int idx = 1; idx < arr.Length; idx++)
                        {
                            fmt = fmt.Replace($"{{{idx - 1}}}", arr[idx]);
                        }
                        return fmt;
                    }
                }
                else if (translateMap.TryGetValue(symbol, out var txt))
                {
                    return txt;
                }
            }
            return symbol;
        }

        public static async Task<AuthenticationResult> AuthenticateAsync(
            string username, string password, ClientInfo clientInfo, string locale)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            var url = "api/pwdman/auth";
            if (!string.IsNullOrEmpty(locale))
            {
                url += $"?locale={locale}";
            }
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            var response = await httpClient.PostAsJsonAsync(url, new AuthenticationModel
            {
                Username = username,
                Password = password,
                ClientUUID = clientInfo.UUID,
                ClientName = clientInfo.Name
            });
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<AuthenticationResult>();
            if (ret == null) throw new ArgumentException("Invalid response");
            return ret;
        }

        public static async Task<UserModel> GetUserAsync(string token)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.GetAsync("api/pwdman/user?details=true");
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<UserModel>();
            if (ret == null) throw new ArgumentException("Invalid response");
            return ret;
        }

        public static async Task<AuthenticationResult> AuthenticatePass2Async(string token, string totp)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.PostAsJsonAsync("api/pwdman/auth2", totp);
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<AuthenticationResult>();
            if (ret == null) throw new ArgumentException("Invalid response");
            return ret;
        }

        public static async Task<string> GetPasswordFileAsync(string token)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.GetAsync("api/pwdman/file");
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<string>();
            if (ret == null) throw new ArgumentException("Invalid response");
            return ret;
        }

        public static async Task<List<Note>> GetNotesAsync(string token)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.GetAsync("api/notes/note");
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<List<Note>>();            
            if (ret == null) throw new ArgumentException("Invalid response");
            return ret;
        }

        public static async Task<Note> GetNoteAsync(string token, long id)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.GetAsync($"api/notes/note/{id}");
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<Note>();
            if (ret == null) throw new ArgumentException("Invalid response");
            return ret;
        }

        public static async Task<Diary> GetDiaryAsync(string token, int year, int month, int day)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            var dt = new DateTime(year, month, day);
            var iso8601 = dt.ToString(ISO8601_DATEFORMAT, CultureInfo.InvariantCulture);
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.GetAsync($"api/diary/entry?date={iso8601}");
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<Diary>();
            if (ret == null) throw new ArgumentException("Invalid response");
            return ret;
        }

        public static async Task<List<DateTime>> GetAllDiaryEntriesAsync(string token)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.GetAsync("api/diary/all");
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<List<DateTime>>();
            if (ret == null) throw new ArgumentException("Invalid response");
            return ret;
        }

        public static async Task<List<DocumentItem>> GetDocumentItemsAsync(string token, long? currentId = null)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var url = "api/document/items";
            if (currentId != null)
            {
                url += $"/{currentId}";
            }
            var response = await httpClient.GetAsync(url);
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<List<DocumentItem>>();
            if (ret == null) throw new ArgumentException("Invalid server response.");
            return ret;
        }

        public static async Task<byte[]> DownloadDocumentAsync(string token, long id)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.GetAsync($"api/document/download/{id}");
            await EnsureSuccessAsync(response);
            return await response.Content.ReadAsByteArrayAsync();
        }

        public static async Task<string> GetContactsAsync(string token)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.GetAsync("api/contacts");
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<string>();
            if (ret == null) throw new ArgumentException("Invalid response");
            return ret;
        }

        public static async Task<long> CreateNewNoteAsync(string token, string title)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.PostAsJsonAsync("api/notes/note",
                new
                {
                    Title = title
                });
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<long>();
            return ret;
        }

        public static async Task<DateTime> UpdateNoteAsync(string token, long id, string title, string content)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.PutAsJsonAsync("api/notes/note",
                new
                {
                    Id = id,
                    Title = title,
                    Content = content
                });
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<DateTime>();
            return ret;
        }

        public static async Task SaveDiaryAsync(string token, int year, int month, int day, string entry)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            var dt = new DateTime(year, month, day);
            var iso8601 = dt.ToString(ISO8601_DATEFORMAT, CultureInfo.InvariantCulture);
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.PostAsJsonAsync("api/diary/entry",
                new
                {
                    Date = iso8601,
                    Entry = entry
                });
            await EnsureSuccessAsync(response);
        }

        public static async Task SaveContactsAsync(string token, string content)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.PutAsJsonAsync("api/contacts", content);
            await EnsureSuccessAsync(response);
        }

        public static async Task SavePasswordFileAsync(string token, string content)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.PostAsJsonAsync("api/pwdman/file", content);
            await EnsureSuccessAsync(response);
        }


        public static async Task<DocumentItem> CreateVolume(string token, string volumeName)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.PostAsJsonAsync("api/document/volume", volumeName);
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<DocumentItem>();
            if (ret == null) throw new ArgumentException("Invalid server response.");
            return ret;
        }

        public static async Task<DocumentItem> CreateFolder(string token, long nodeId, string folderName)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var response = await httpClient.PostAsJsonAsync($"api/document/folder/{nodeId}", folderName);
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<DocumentItem>();
            if (ret == null) throw new ArgumentException("Invalid server response.");
            return ret;
        }

        public static async Task<DocumentItem> UploadDocument(string token, long parentId, string name, byte [] data)
        {
            if (httpClient == null) throw new ArgumentException("RestClient not initialized.");
            EnsureRateLimit();
            httpClient.DefaultRequestHeaders.Remove("token");
            httpClient.DefaultRequestHeaders.Add("token", token);
            var formData = new MultipartFormDataContent
            {
                { new ByteArrayContent(data), "document-file", name },
                { new StringContent("false"), "overwrite" }
            };
            var response = await httpClient.PostAsync($"api/document/upload/{parentId}", formData);
            await EnsureSuccessAsync(response);
            var ret = await response.Content.ReadFromJsonAsync<DocumentItem>();
            if (ret == null) throw new ArgumentException("Invalid server response.");
            return ret;
        }

        // --- private

        private static void EnsureRateLimit()
        {
            Thread.Sleep(100); // 10 request per seconds are allowed only
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response)
        {
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                ProblemDetails? problemDetails = null;
                try
                {
                    problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
                }
                catch
                {
                    // ignored, no prolem details returned
                }
                if (problemDetails == null)
                {
                    throw new ArgumentException($"Invalid response. Error code is {response.StatusCode}.");
                }
                var message = Translate(problemDetails.title);
                if (problemDetails.title == "ERROR_INVALID_TOKEN")
                {
                    throw new InvalidTokenException(message);
                }
                throw new ArgumentException(message);
            }
        }
    }
}
