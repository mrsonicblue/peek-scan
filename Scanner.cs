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
        private string _dbPath;
        private string _outputPath;
        private SHA1 _sha1;
        private DateTime _lastProgressDate;
        private int _lastProgressLength;

        public Scanner(IConfiguration config)
        {
            _gamesPath = config["GamesPath"];
            _dbPath = config["DbPath"];
            _outputPath = config["OutputPath"];
            _sha1 = SHA1.Create();
            _lastProgressDate = DateTime.MinValue;
            _lastProgressLength = 0;
        }

        private void Progress(string coreName, int coreIndex, int coreCount, int romIndex, int romCount)
        {
            DateTime now = DateTime.Now;
            if (romIndex == 0 || (now - _lastProgressDate).TotalMilliseconds > 250.0)
            {
                string roms;
                if (romCount == 0)
                    roms = "no roms found";
                else
                    roms = $"processed {romIndex} of {romCount} roms";

                string message = $"\rCore: {coreName} ({coreIndex + 1} of {coreCount})... {roms}";
                int messageLength = message.Length;
                if (messageLength < _lastProgressLength)
                    message += new string(' ', _lastProgressLength - messageLength);

                Console.Write(message);

                _lastProgressDate = now;
                _lastProgressLength = message.Length;
            }
        }

        private int RomHeaderSize(string coreName)
        {
            if (coreName == "NES")
                return 16;

            return 0;
        }

        private string GetHash(FileInfo file, int headerSize)
        {
            byte[] hash = null;
            byte[] header = new byte[headerSize];

            string ext = file.Extension?.ToLower();
            if (ext == ".zip")
            {
                using (var zip = ZipFile.Open(file.FullName, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        using (var entryStream = entry.Open())
                        {
                            if (headerSize > 0)
                                entryStream.Read(header, 0, headerSize);

                            hash = _sha1.ComputeHash(entryStream);
                        }

                        break;
                    }
                }
            }
            else
            {
                using (var fileStream = file.OpenRead())
                {
                    if (headerSize > 0)
                        fileStream.Read(header, 0, headerSize);

                    hash = _sha1.ComputeHash(fileStream);
                }
            }

            if (hash == null)
                return null;

            return BitConverter.ToString(hash).Replace("-", "");
        }

        public void Run()
        {
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

            Regex yearReg = new Regex("([0-9]{4})");

            Console.WriteLine("Opening database...");

            using (var conn = new SqliteConnection($"Data Source=\"{_dbPath}\""))
            {
                conn.Open();

                Console.WriteLine("Reading regions...");

                var regionLookup = conn.Select<Region>("SELECT * FROM REGIONS", p => { })
                    .SelectMany(o => o.RegionName.Split(',').Select(n => new Region() { RegionID = o.RegionID, RegionName = n.Trim() }))
                    .ToLookup(o => o.RegionID, o => o.RegionName);

                Console.WriteLine("Scanning...");

                var coreWhitelist = new string[] { "NES", "SNES" };

                var gameDir = new DirectoryInfo(_gamesPath);
                var coreDirs = gameDir.GetDirectories()
                    .Where(o => !o.Name.StartsWith("."))
                    .Where(o => coreWhitelist.Contains(o.Name))
                    .ToArray();
                int coreCount = coreDirs.Length;
                int coreIndex = 0;
                foreach (var coreDir in coreDirs)
                {
                    string coreName = coreDir.Name;
                    var romFiles = coreDir.GetFiles();
                    int romCount = romFiles.Length;
                    int romIndex = 0;

                    Progress(coreName, coreIndex, coreCount, romIndex, romCount);

                    int headerSize = RomHeaderSize(coreName);
                    string outputPath = Path.Combine(_outputPath, coreName + ".txt");

                    using (var outputStream = File.Open(outputPath, FileMode.Create))
                    using (var writer = new StreamWriter(outputStream, Encoding.ASCII))
                    {
                        writer.Write("ROM\tRegion\tYear\tDeveloper\tGenre\n");

                        foreach (var romFile in romFiles)
                        {
                            Progress(coreName, coreIndex, coreCount, romIndex, romCount);

                            string hash = GetHash(romFile, headerSize);
                            if (hash == null)
                                break;

                            var rom = conn.Find<Rom>("SELECT romID, regionID FROM ROMs WHERE romHashSHA1 = $hash", p => { p["$hash"] = hash; });
                            if (rom == null)
                                continue;

                            var release = conn.Find<Release>("SELECT releaseDeveloper, releaseGenre, releaseDate FROM RELEASES WHERE romID = $romID", p => { p["$romID"] = rom.RomID; });
                            if (release == null)
                                continue;

                            string year = "";
                            if (release.ReleaseDate != null)
                            {
                                var match = yearReg.Match(release.ReleaseDate);
                                if (match.Success)
                                    year = match.Groups[1].Value;
                            }

                            string genre = "";
                            if (release.ReleaseGenre != null)
                                genre = string.Join("|", release.ReleaseGenre.Split(",").Select(o => o.Trim()));

                            writer.Write(romFile.Name);
                            writer.Write("\t");
                            writer.Write(string.Join("|", regionLookup[rom.RegionID]));
                            writer.Write("\t");
                            writer.Write(Clean(year));
                            writer.Write("\t");
                            writer.Write(Clean(release.ReleaseDeveloper));
                            writer.Write("\t");
                            writer.Write(Clean(genre));
                            writer.Write("\n");

                            romIndex++;
                        }
                    }

                    coreIndex++;
                }
            }

            Console.Write("\r" + new string(' ', _lastProgressLength));
            Console.WriteLine("\rDONE!");
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
