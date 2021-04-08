using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Peek.Scan
{
    class Scanner
    {
        private string _gamesPath;
        private string _dbPath = "";
        private string _outputPath = "";

        public Scanner(IConfiguration config)
        {
            _gamesPath = config["GamesPath"];
            _dbPath = config["DbPath"];
            _outputPath = config["OutputPath"];
        }

        public void Run()
        {
            int headerSize = 16;

            if (!Directory.Exists(_gamesPath))
            {
                Console.WriteLine("ERROR: Roms directory doesn't exist at path: {0}", _gamesPath);
                return;
            }

            if (!File.Exists(_dbPath))
            {
                Console.WriteLine("ERROR: OpenVGDB doesn't exist at path: {0}", _dbPath);
                return;
            }

            if (!Directory.Exists(_outputPath))
                Directory.CreateDirectory(_outputPath);

            Console.WriteLine("Scanning...");

            var gameDir = new DirectoryInfo(_gamesPath);
            foreach (var coreDir in gameDir.GetDirectories())
            {
                if (coreDir.Name.StartsWith("."))
                    continue;

                string coreName = coreDir.Name;
                Console.WriteLine("CORE: {0}", coreName);
            }

            //Console.WriteLine("Opening output file...");

            //using (var outputStream = File.Open(outputPath, FileMode.Create))
            //using (var writer = new StreamWriter(outputStream, Encoding.ASCII))
            //{
            //    writer.Write("ROM\tRegion\tYear\tDeveloper\tGenre\n");

            //    Console.WriteLine("Opening database...");

            //    using (var conn = new SqliteConnection($"Data Source=\"{dbPath}\""))
            //    {
            //        conn.Open();

            //        Console.WriteLine("Reading regions");

            //        var regionLookup = conn.Select<Region>("SELECT * FROM REGIONS", p => { })
            //            .SelectMany(o => o.RegionName.Split(',').Select(n => new Region() { RegionID = o.RegionID, RegionName = n.Trim() }))
            //            .ToLookup(o => o.RegionID, o => o.RegionName);

            //        SHA1 sha1 = SHA1.Create();
            //        Regex yearReg = new Regex("([0-9]{4})");

            //        foreach (var file in new DirectoryInfo(romsPath).GetFiles())
            //        {
            //            Console.WriteLine(file.Name);

            //            byte[] hash = null;
            //            byte[] header = new byte[16];
            //            string ext = file.Extension?.ToLower();
            //            if (ext == ".zip")
            //            {
            //                using (var zip = ZipFile.Open(file.FullName, ZipArchiveMode.Read))
            //                {
            //                    foreach (var entry in zip.Entries)
            //                    {
            //                        //ext = Path.GetExtension(entry.Name)?.ToLower();
            //                        //if (ext != ".nes")
            //                        //    continue;

            //                        using (var entryStream = entry.Open())
            //                        {
            //                            if (headerSize > 0)
            //                                entryStream.Read(header, 0, 16);

            //                            hash = sha1.ComputeHash(entryStream);
            //                        }
            //                    }
            //                }
            //            }
            //            else // if (ext == ".nes")
            //            {
            //                using (var fileStream = file.OpenRead())
            //                {
            //                    if (headerSize > 0)
            //                        fileStream.Read(header, 0, 16);

            //                    hash = sha1.ComputeHash(fileStream);
            //                }
            //            }

            //            if (hash == null)
            //            {
            //                Console.WriteLine(" - WARNING: Unable to calculate hash");
            //                continue;
            //            }

            //            string hashString = BitConverter.ToString(hash).Replace("-", "");

            //            var rom = conn.Find<Rom>("SELECT romID, regionID FROM ROMs WHERE romHashSHA1 = $hash", p => { p["$hash"] = hashString; });
            //            if (rom == null)
            //            {
            //                Console.WriteLine(" - Warning: Could not find ROM in database");
            //                continue;
            //            }

            //            var release = conn.Find<Release>("SELECT releaseDeveloper, releaseGenre, releaseDate FROM RELEASES WHERE romID = $romID", p => { p["$romID"] = rom.RomID; });
            //            if (release == null)
            //            {
            //                Console.WriteLine(" - Warning: Could not find release in database");
            //                continue;
            //            }

            //            string year = "";
            //            if (release.ReleaseDate != null)
            //            {
            //                var match = yearReg.Match(release.ReleaseDate);
            //                if (match.Success)
            //                    year = match.Groups[1].Value;
            //            }

            //            string genre = "";
            //            if (release.ReleaseGenre != null)
            //                genre = string.Join("|", release.ReleaseGenre.Split(",").Select(o => o.Trim()));

            //            writer.Write(file.Name);
            //            writer.Write("\t");
            //            writer.Write(string.Join("|", regionLookup[rom.RegionID]));
            //            writer.Write("\t");
            //            writer.Write(Clean(year));
            //            writer.Write("\t");
            //            writer.Write(Clean(release.ReleaseDeveloper));
            //            writer.Write("\t");
            //            writer.Write(Clean(genre));
            //            writer.Write("\n");
            //        }
            //    }
            //}

            Console.WriteLine("DONE!");
        }

        private static string Clean(string s)
        {
            if (s == null)
                return "";

            return s.Trim().Replace(",", "");
        }

        private class Region
        {
            public long RegionID { get; set; }
            public string RegionName { get; set; }
        }

        private class Rom
        {
            public long RomID { get; set; }
            public long RegionID { get; set; }
        }

        private class Release
        {
            public long ReleaseID { get; set; }
            public string ReleaseDeveloper { get; set; }
            public string ReleaseGenre { get; set; }
            public string ReleaseDate { get; set; }
        }
    }
}
