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
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Peek scanner");

            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddCommandLine(args)
                .Build();

            if (!new Setup(config).Run())
                return;

            new Scanner(config).Run();
        }
    }
}
