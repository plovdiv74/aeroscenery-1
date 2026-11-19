using AeroScenery.AFS2;
using AeroScenery.Common;
using AeroScenery.OrthoPhotoSources;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AeroScenery.Download
{

    public class DownloadManager
    {
        private int downloadThreads = 4;
        private readonly ILog log = LogManager.GetLogger("AeroScenery");

        private CancellationTokenSource cancellationTokenSource;

        public DownloadManager()
        {
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void StopDownloads()
        {
            cancellationTokenSource.Cancel();
        }

        public async Task DownloadImageTiles(OrthophotoSource orthophotoSource, List<ImageTile> imageTiles, IProgress<DownloadThreadProgress> threadProgress, string downloadDirectory)
        {

            // Reset cancellation token status
            cancellationTokenSource = new CancellationTokenSource();

            if (imageTiles.Count > 0)
            {
                log.InfoFormat("Beginning download of {0} image tiles from {1}", imageTiles.Count, orthophotoSource.ToString());

                // What is already on disk, listed once: only tiles without an image file are downloaded, and .aero
                // files that exist aren't written again, so a rerun only fills the gaps.
                var existingFiles = new HashSet<string>(
                    Directory.Exists(downloadDirectory) ? Directory.EnumerateFiles(downloadDirectory).Select(Path.GetFileName) : Enumerable.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);
                int toDownload = imageTiles.Count(t => NeedsDownload(t, existingFiles));
                log.InfoFormat("{0} of them are already downloaded, {1} to download", imageTiles.Count - toDownload, toDownload);

                int downloadsPerThread = imageTiles.Count / this.downloadThreads;
                int downloadsPerThreadMod = imageTiles.Count % this.downloadThreads;

                var tasks = new List<Task>();

                // Spawn the required number of threads
                for (int i = 0; i < this.downloadThreads; i++)
                {
                    var threadNumber = i;

                    tasks.Add(Task.Run(async () =>
                    {
                        var xmlSerializer = new XmlSerializer(typeof(ImageTile));


                        var maxWait = AeroSceneryManager.Instance.Settings.DownloadWaitMs.Value + AeroSceneryManager.Instance.Settings.DownloadWaitRandomMs.Value;
                        var minWait = AeroSceneryManager.Instance.Settings.DownloadWaitMs.Value - AeroSceneryManager.Instance.Settings.DownloadWaitRandomMs.Value;
                        Random random = new Random();


                        var downloadThreadProgress = new DownloadThreadProgress();
                        downloadThreadProgress.TotalFiles = downloadsPerThread;
                        downloadThreadProgress.DownloadThreadNumber = threadNumber;

                        if (threadNumber == this.downloadThreads - 1)
                        {
                            downloadThreadProgress.TotalFiles += downloadsPerThreadMod;
                        }

                        //Debug.WriteLine("Thread " + threadNumber.ToString());
                        var cookieContainer = new CookieContainer();

                        // Decompression on: the request accepts gzip, and the servers sometimes use it (saving such a
                        // response as is left gzip data in a .jpg file).
                        using (var handler = new HttpClientHandler()
                        {
                            CookieContainer = cookieContainer,
                            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                        })


                        using (HttpClient httpClient = new HttpClient(handler))
                        {
                            long lastDownloadTimestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

                            // Work through this threads share of downloads
                            for (int j = 0 + (threadNumber * downloadsPerThread); j < (threadNumber + 1) * downloadsPerThread; j++)
                            {
                                if (this.cancellationTokenSource.Token.IsCancellationRequested)
                                {
                                    break;
                                }

                                // We nee to re-eval this. If the user has cancelled, image tiles will be cleared
                                if (imageTiles != null && imageTiles.Count > 0 && NeedsDownload(imageTiles[j], existingFiles))
                                {
                                    int waitTime = random.Next(minWait, maxWait);
                                    var waitTimeSpan = new TimeSpan(waitTime * TimeSpan.TicksPerMillisecond);
                                    await Task.Delay(waitTimeSpan);

                                    this.DownloadFile(httpClient, cookieContainer, imageTiles[j], downloadDirectory, orthophotoSource);
                                }

                                // We nee to re-eval this. If the user has cancelled, image tiles will be cleared
                                if (imageTiles != null && imageTiles.Count > 0 && !existingFiles.Contains(imageTiles[j].FileName + ".aero"))
                                {
                                    this.SaveImageTileAeroFile(xmlSerializer, imageTiles[j], downloadDirectory);
                                }

                                downloadThreadProgress.FilesDownloaded++;
                                threadProgress.Report(downloadThreadProgress);

                                //Debug.WriteLine("Thread " + threadNumber.ToString() + " Index " + j.ToString());
                            }

                            // If this is the 'last' thread, also work through the remainder 
                            if (threadNumber == this.downloadThreads - 1)
                            {
                                for (int k = 0; k < downloadsPerThreadMod; k++)
                                {
                                    if (this.cancellationTokenSource.Token.IsCancellationRequested)
                                    {
                                        break;
                                    }


                                    var index = k + (downloadsPerThread * this.downloadThreads);

                                    // We nee to re-eval this. If the user has cancelled, image tiles will be cleared
                                    if (imageTiles != null && imageTiles.Count > 0 && NeedsDownload(imageTiles[index], existingFiles))
                                    {
                                        int waitTime = random.Next(minWait, maxWait);
                                        var waitTimeSpan = new TimeSpan(waitTime * TimeSpan.TicksPerMillisecond);
                                        await Task.Delay(waitTimeSpan);

                                        this.DownloadFile(httpClient, cookieContainer, imageTiles[index], downloadDirectory, orthophotoSource);
                                    }

                                    // We nee to re-eval this. If the user has cancelled, image tiles will be cleared
                                    if (imageTiles != null && imageTiles.Count > 0 && !existingFiles.Contains(imageTiles[index].FileName + ".aero"))
                                    {
                                        this.SaveImageTileAeroFile(xmlSerializer, imageTiles[index], downloadDirectory);
                                    }


                                    downloadThreadProgress.FilesDownloaded++;
                                    threadProgress.Report(downloadThreadProgress);

                                    //Debug.WriteLine("Thread " + threadNumber.ToString() + "Index " + k.ToString());

                                }
                            }


                        }

                        xmlSerializer = null;

                    }, this.cancellationTokenSource.Token));
                }

                await Task.WhenAll(tasks);
                log.InfoFormat("Finished download of {0} image tiles from {1}", imageTiles.Count, orthophotoSource.ToString());

            }
        }

        private void DownloadFile(HttpClient httpClient, CookieContainer cookieContainer, ImageTile imageTile, string path, OrthophotoSource orthophotoSource)
        {
            string fullFilePath = path + imageTile.FileName + "." + imageTile.ImageExtension;

            cookieContainer = new CookieContainer();
            cookieContainer.Add(new Uri(imageTile.URL), new Cookie("APISID", Guid.NewGuid().ToString()));
            cookieContainer.Add(new Uri(imageTile.URL), new Cookie("NID", "119=" + Guid.NewGuid().ToString()));
            cookieContainer.Add(new Uri(imageTile.URL), new Cookie("NID", "129=" + Guid.NewGuid().ToString()));

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AeroSceneryManager.Instance.Settings.UserAgent);
            httpClient.DefaultRequestHeaders.Referrer = new Uri("http://google.com/");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");

            // A tile is tried a few times: a timeout or a server error is often gone a few seconds later, and a
            // missing tile leaves a hole that keeps whole TTC tiles from being written.
            const int Attempts = 3;
            for (int attempt = 1; attempt <= Attempts; attempt++)
            {
                try
                {
                    using (var response = httpClient.GetAsync(imageTile.URL).Result)
                    {
                        // If we are Bing we might be served a valid image but there is really no tile available: the
                        // tile info header is then no-tile, and the image shouldn't be saved
                        if (orthophotoSource == OrthophotoSource.Bing && response.Headers.Contains("X-VE-Tile-Info") &&
                            response.Headers.GetValues("X-VE-Tile-Info").FirstOrDefault() == "no-tile")
                        {
                            // Remembered, so the tile isn't asked for again, and the TTC writer makes it transparent
                            File.WriteAllText(path + imageTile.FileName + AFS4.SourceTiles.NoImageryExtension, imageTile.URL);
                            return;
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new HttpRequestException(String.Format("HTTP {0} {1}", (int)response.StatusCode, response.ReasonPhrase));
                        }

                        var bytes = response.Content.ReadAsByteArrayAsync().Result;
                        if (!IsImage(bytes))
                        {
                            throw new InvalidDataException(String.Format("the response ({0} bytes, {1}) isn't a JPEG or PNG image",
                                bytes.Length, response.Content.Headers.ContentType));
                        }

                        // Written to a temporary file first, so an interrupted download never leaves a partial tile
                        var temporaryPath = fullFilePath + ".download";
                        File.WriteAllBytes(temporaryPath, bytes);
                        if (File.Exists(fullFilePath))
                        {
                            File.Delete(fullFilePath);
                        }

                        File.Move(temporaryPath, fullFilePath);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    if (attempt == Attempts)
                    {
                        log.Error(String.Format("There was an error downloading {0} ({1} attempts)", imageTile.URL, Attempts), ex);
                    }
                    else
                    {
                        log.Warn(String.Format("Downloading {0} failed (attempt {1} of {2}): {3}", imageTile.URL, attempt, Attempts,
                            (ex.InnerException ?? ex).Message));
                        Thread.Sleep(TimeSpan.FromSeconds(5 * attempt));
                    }
                }
            }
        }

        /// <summary>
        /// Whether a tile has to be downloaded: it has neither an image file nor a no-imagery marker yet (existingFiles:
        /// the names in the download folder). A file on disk is kept: downloads are checked to be images before they're
        /// saved, and the TTC writer reads the gzip-wrapped files earlier versions saved.
        /// </summary>
        private static bool NeedsDownload(ImageTile imageTile, HashSet<string> existingFiles)
        {
            return !existingFiles.Contains(imageTile.FileName + "." + imageTile.ImageExtension) &&
                   !existingFiles.Contains(imageTile.FileName + AFS4.SourceTiles.NoImageryExtension);
        }

        // JPEG (FF D8 FF) or PNG (89 50 4E 47) by the file's first bytes.
        private static bool IsImage(byte[] bytes)
        {
            return (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) ||
                   (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47);
        }

        private void SaveImageTileAeroFile(XmlSerializer xmlSerializer, ImageTile imageTile, string path)
        {
            using (TextWriter tw = new StreamWriter(path + imageTile.FileName + ".aero"))
            {
                xmlSerializer.Serialize(tw, imageTile);
            }
        }

    }
}
