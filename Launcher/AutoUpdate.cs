namespace Launcher
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;

    public static class AutoUpdate
    {
        private static readonly HttpClient Client = new();
        private static string post_url = "https://www.ownedcore.com/forums/mmo/path-of-exile/poe-bots-programs/953353-gamehelper-light-version-of-poehud-exile-api.html#post4325338";
        private static string login_url = "https://www.ownedcore.com/forums/login.php?do=login";
        private static string version_file_name = "VERSION.txt";
        private static string self_exe_name = AppDomain.CurrentDomain.FriendlyName;

        public static async Task<(bool, string)> IsNewVersionFoundAsync(string gameHelperDir)
        {
            var versionFile = Path.Combine(gameHelperDir, version_file_name);
            if (!File.Exists(versionFile))
            {
                Console.WriteLine($"{versionFile} is missing, skipping upgrade process.");
                return (false, string.Empty);
            }

            var currentVersion = File.ReadAllText(versionFile).Trim();

            try
            {
                var body = await Client.GetStringAsync(post_url);
                var downloadUrlStart = body.IndexOf("&gt;&gt;&gt; <a href=\"") + "&gt;&gt;&gt; <a href=\"".Length;
                var downloadUrlEnd = body.IndexOf("-zip", downloadUrlStart) + "-zip".Length;
                var versionStart = body.IndexOf("GameHelper-Release-") + "GameHelper-Release-".Length;
                var versionEnd = body.IndexOf(".zip", versionStart);
                if (body.AsSpan(versionStart, versionEnd - versionStart).ToString() != currentVersion)
                {
                    return (true, body[downloadUrlStart..downloadUrlEnd]);
                }
                else
                {
                    return (false, string.Empty);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to determine IsNewVersionFound due to " + ex.Message);
            }

            return (false, string.Empty);
        }

        public static async Task<bool> UpgradeGameHelper(string gameHelperDir, string downloadUrl, string username, string passwordMD5)
        {
            Console.WriteLine("Signing into ownedcore website.");
            using var resp = await Client.PostAsync(login_url, new FormUrlEncodedContent(new Dictionary<string, string>()
            {
                { "vb_login_username", username },
                { "vb_login_md5password", passwordMD5 },
                { "do", "login" },
                { "cookieuser", "1" }
            }));

            var respMsg = resp.EnsureSuccessStatusCode();
            if (!resp.Headers.TryGetValues("set-cookie", out var loginCookies))
            {
                Console.WriteLine("No cookies found. Failed to login into ownedcore website.");
                return false;

            }

            Console.WriteLine($"Downloading new version from:{downloadUrl}");
            bool isLoginCookieFound = false;
            foreach (var cookie in loginCookies)
            {
                if (cookie.Contains("ocf_userid") || cookie.Contains("ocf_password"))
                {
                    isLoginCookieFound = true;
                }

                Client.DefaultRequestHeaders.Add("Cookie", cookie[..cookie.IndexOf(';')]);
            }

            if (!isLoginCookieFound)
            {
                Console.WriteLine("Login cookie not found. Failed to login into ownedcore website.");
                return false;
            }

            var release_file_name = "release.zip";
            using var respDownloadFile = await Client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            using var fs = new FileStream(release_file_name, FileMode.Create);
            await respDownloadFile.Content.CopyToAsync(fs);

            Process.Start("powershell.exe", $"Start-sleep -Seconds 3; " +
                $"Expand-Archive -Force {release_file_name} .; " +
                $"Remove-Item -Force {release_file_name}; " +
                $"./{self_exe_name}.exe -Force");
            return true;
        }
    }
}
