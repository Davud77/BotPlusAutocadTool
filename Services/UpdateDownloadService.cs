// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BotPlusAutocadTool.Services
{
    public sealed record UpdateDownloadProgress(long Received, long Total)
    {
        public double Percent => Total > 0 ? Math.Min(100, Received * 100.0 / Total) : 0;
    }

    public static class UpdateDownloadService
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        public static Task<string> DownloadAsync(ReleaseVersion release, string directory,
            IProgress<UpdateDownloadProgress> progress, CancellationToken token) =>
            DownloadAsync(Client, release, directory, progress, token);

        internal static async Task<string> DownloadAsync(HttpClient client, ReleaseVersion release, string directory,
            IProgress<UpdateDownloadProgress> progress, CancellationToken token)
        {
            if (release.InstallerUrl == null || release.InstallerName == null)
                throw new InvalidDataException("В этом выпуске нет установщика для автоматической загрузки.");
            string expected = release.InstallerSha256;
            if (expected == null && release.ChecksumsUrl != null)
            {
                using var response = await client.GetAsync(release.ChecksumsUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using var buffer = new MemoryStream();
                var bytes = new byte[4096];
                int count;
                while ((count = await input.ReadAsync(bytes.AsMemory(), token).ConfigureAwait(false)) != 0)
                {
                    if (buffer.Length + count > 65536) throw new InvalidDataException("Файл контрольных сумм слишком большой.");
                    buffer.Write(bytes, 0, count);
                }
                foreach (string line in System.Text.Encoding.UTF8.GetString(buffer.ToArray()).Split('\n'))
                {
                    var match = Regex.Match(line.Trim(), @"^([a-fA-F0-9]{64})\s+\*?(.+)$");
                    if (match.Success && match.Groups[2].Value == release.InstallerName) expected = match.Groups[1].Value;
                }
            }
            if (expected == null || !Regex.IsMatch(expected, "^[a-fA-F0-9]{64}$"))
                throw new InvalidDataException("В выпуске нет корректной SHA-256 для установщика.");

            Directory.CreateDirectory(directory);
            string destination = Path.Combine(directory, release.InstallerName);
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using (var response = await client.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    long total = release.InstallerSize > 0 ? release.InstallerSize : response.Content.Headers.ContentLength ?? 0;
                    if (total > 512L * 1024 * 1024) throw new InvalidDataException("Размер установщика превышает допустимый.");
                    using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                    using var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = new byte[65536];
                    long received = 0;
                    progress?.Report(new UpdateDownloadProgress(0, total));
                    int count;
                    while ((count = await source.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) != 0)
                    {
                        received += count;
                        if (received > 512L * 1024 * 1024 || (total > 0 && received > total))
                            throw new InvalidDataException("Размер загрузки не совпадает с опубликованным установщиком.");
                        await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                        hash.AppendData(buffer, 0, count);
                        progress?.Report(new UpdateDownloadProgress(received, total));
                    }
                    if (received == 0 || (total > 0 && received != total)) throw new InvalidDataException("Установщик скачан не полностью.");
                    string actual = Convert.ToHexString(hash.GetHashAndReset());
                    if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("SHA-256 не совпала. Повторите загрузку установщика.");
                    await output.FlushAsync(token).ConfigureAwait(false);
                }
                token.ThrowIfCancellationRequested();
                File.Move(temporary, destination, overwrite: true);
                return destination;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
