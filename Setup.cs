using Microsoft.Extensions.Configuration;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Peek.Scan
{
    class Setup
    {
        private string _dbPath;

        public Setup(IConfiguration config)
        {
            _dbPath = config["DbPath"];
        }

        public bool Run()
        {
            Regex yesReg = new Regex("^(|[yY]|[yY][eE][sS])$");

            Console.Write("Checking for OpenVGDB... ");
            bool needOpenVGBD = !File.Exists(_dbPath);
            Console.WriteLine(needOpenVGBD ? "not found" : "found");

            if (needOpenVGBD)
            {
                Console.WriteLine();
                Console.WriteLine("This application reads ROM metadata from OpenVGDB. It must be downloaded.");
                Console.Write(" -- Download now? [Y/n] ");
                string line = Console.ReadLine();

                if (!yesReg.IsMatch(line))
                {
                    Console.WriteLine("ok bye");
                    return false;
                }

                Console.Write("Checking latest version... ");

                var api = new RestClient("https://api.github.com/");
                var releaseRequest = new RestRequest("repos/OpenVGDB/OpenVGDB/releases/latest", Method.GET);
                var releaseResponse = api.Execute<Release>(releaseRequest);

                if (releaseResponse.StatusCode != HttpStatusCode.OK || releaseResponse.Data == null)
                    throw new Exception("Failed to get release data");

                var release = releaseResponse.Data;
                Console.WriteLine(release.tag_name);

                if (release.assets == null || release.assets.Count == 0)
                    throw new Exception("Release does not contain any assets");

                var asset = release.assets.FirstOrDefault(o => o.name == "openvgdb.zip");
                if (asset == null || string.IsNullOrEmpty(asset.browser_download_url))
                    throw new Exception("Release does not contain openvgdb.zip");

                Console.Write("Downloading... ");

                var github = new RestClient();
                var downloadRequest = new RestRequest(asset.browser_download_url, Method.GET);
                downloadRequest.ResponseWriter = responseStream =>
                {
                    using (responseStream)
                    {
                        using (var zip = new ZipArchive(responseStream, ZipArchiveMode.Read))
                        {
                            var entry = zip.GetEntry("openvgdb.sqlite");
                            if (entry == null)
                                throw new Exception("Archive does not contain database");

                            entry.ExtractToFile(_dbPath);
                        }
                    }
                };
                var downloadResponse = github.DownloadData(downloadRequest);

                Console.WriteLine("OK");
            }

            return true;
        }

        private class Release
        {
            public string tag_name { get; set; }
            public List<Asset> assets { get; set; }
        }

        private class Asset
        {
            public string name { get; set; }
            public string browser_download_url { get; set; }
        }
    }
}
