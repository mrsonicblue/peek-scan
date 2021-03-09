using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Peek.Tab
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: Peek.Tab.exe [openvgdb path] [rom path] [output path]");
                return;
            }

            string dbPath = args[0];
            string romsPath = args[1];
            string outputPath = args[2];

            if (!File.Exists(dbPath))
            {
                Console.WriteLine("ERROR: OpenVGDB doesn't exist at path: {0}", dbPath);
                return;
            }

            if (!Directory.Exists(romsPath))
            {
                Console.WriteLine("ERROR: Roms directory doesn't exist at path: {0}", romsPath);
                return;
            }

            var outputPathParent = Directory.GetParent(outputPath);
            if (!outputPathParent.Exists)
            {
                Console.WriteLine("ERROR: Output path directory doesn't exist at path: {0}", outputPathParent.FullName);
                return;
            }

            Console.WriteLine("Opening output file...");

            using (var outputStream = File.OpenWrite(outputPath))
            using (var writer = new StreamWriter(outputStream, Encoding.ASCII))
            {
                writer.Write("ROM\tRegion\tYear\tDeveloper\tGenre\n");

                Console.WriteLine("Opening database...");

                using (var conn = new SqliteConnection($"Data Source=\"{dbPath}\""))
                {
                    conn.Open();

                    Console.WriteLine("Reading regions");

                    var regionLookup = conn.Select<Region>("SELECT * FROM REGIONS", p => { })
                        .SelectMany(o => o.RegionName.Split(',').Select(n => new Region() { RegionID = o.RegionID, RegionName = n.Trim() }))
                        .ToLookup(o => o.RegionID, o => o.RegionName);

                    SHA1 sha1 = SHA1.Create();

                    foreach (var file in new DirectoryInfo(romsPath).GetFiles())
                    {
                        Console.WriteLine(file.Name);

                        byte[] hash = null;
                        byte[] header = new byte[16];
                        string ext = file.Extension?.ToLower();
                        if (ext == ".zip")
                        {
                            using (var zip = ZipFile.Open(file.FullName, ZipArchiveMode.Read))
                            {
                                foreach (var entry in zip.Entries)
                                {
                                    ext = Path.GetExtension(entry.Name)?.ToLower();
                                    if (ext != ".nes")
                                        continue;

                                    using (var entryStream = entry.Open())
                                    {
                                        entryStream.Read(header, 0, 16);
                                        hash = sha1.ComputeHash(entryStream);
                                    }
                                }
                            }
                        }
                        else if (ext == ".nes")
                        {
                            using (var fileStream = file.OpenRead())
                            {
                                fileStream.Read(header, 0, 16);
                                hash = sha1.ComputeHash(fileStream);
                            }
                        }

                        if (hash == null)
                        {
                            Console.WriteLine(" - WARNING: Unable to calculate hash");
                            continue;
                        }

                        string hashString = BitConverter.ToString(hash).Replace("-", "");

                        var rom = conn.Find<Rom>("SELECT romID, regionID FROM ROMs WHERE romHashSHA1 = $hash", p => { p["$hash"] = hashString; });
                        if (rom == null)
                        {
                            Console.WriteLine(" - Warning: Could not find ROM in database");
                            continue;
                        }

                        var release = conn.Find<Release>("SELECT releaseDeveloper, releaseGenre, releaseDate FROM RELEASES WHERE romID = $romID", p => { p["$romID"] = rom.RomID; });
                        if (release == null)
                        {
                            Console.WriteLine(" - Warning: Could not find release in database");
                            continue;
                        }

                        string year = "";
                        if (release.ReleaseDate != null && release.ReleaseDate.Length >= 4)
                            year = release.ReleaseDate.Substring(release.ReleaseDate.Length - 4);

                        string genre = "";
                        if (release.ReleaseGenre != null)
                            genre = string.Join("|", release.ReleaseGenre.Split(",").Select(o => o.Trim()));

                        writer.Write(file.Name);
                        writer.Write("\t");
                        writer.Write(string.Join("|", regionLookup[rom.RegionID]));
                        writer.Write("\t");
                        writer.Write(Clean(year));
                        writer.Write("\t");
                        writer.Write(Clean(release.ReleaseDeveloper));
                        writer.Write("\t");
                        writer.Write(Clean(genre));
                        writer.Write("\n");
                    }
                }
            }

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
