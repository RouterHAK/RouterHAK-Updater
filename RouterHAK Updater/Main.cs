using Ionic.Zip;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace RouterHAK_Updater
{
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();
        }

        public string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
        #region Internet Connectivity Check Function
        public static bool CheckForInternetConnection()
        {
            try
            {
                Ping myPing = new Ping();
                String host = "1.1.1.1";
                byte[] buffer = new byte[32];
                int timeout = 1000;
                PingOptions pingOptions = new PingOptions();
                PingReply reply = myPing.Send(host, timeout, buffer, pingOptions);
                return (reply.Status == IPStatus.Success);
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion
        private void Log(string text)
        {
            LogBox.Invoke((MethodInvoker)delegate
            {
                LogBox.AppendText(text + "\n");
            });
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            #region Initialize folder structure

            string MainDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + "RouterHAK";
            // Create folder structure.
            if (!Directory.Exists(MainDirectory))
            {
                Directory.CreateDirectory(MainDirectory);
            }

            if (!Directory.Exists(MainDirectory + Path.DirectorySeparatorChar + "Models"))
            {
                Directory.CreateDirectory(MainDirectory + Path.DirectorySeparatorChar + "Models");
            }

            if (!Directory.Exists(MainDirectory + Path.DirectorySeparatorChar + "Modules"))
            {
                Directory.CreateDirectory(MainDirectory + Path.DirectorySeparatorChar + "Modules");
            }

            // Clean up incase the last updated failed.
            if (File.Exists(MainDirectory + Path.DirectorySeparatorChar + "Models.zip"))
            {
                File.Delete(MainDirectory + Path.DirectorySeparatorChar + "Models.zip");
            }

            if (Directory.Exists(MainDirectory + Path.DirectorySeparatorChar + "CPE-Models-main"))
            {
                Directory.Delete(MainDirectory + Path.DirectorySeparatorChar + "CPE-Models-main", true);
            }
            #endregion
            #region Download Modules
            new Thread(() =>
            {
                if (CheckForInternetConnection())
                {
                    string UpdateFile = "https://download.routerhak.com/updater.json";

                    WebClient client = new WebClient();

                    ServicePointManager.Expect100Continue = true;
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                    ServicePointManager.DefaultConnectionLimit = 9999;

                    var files = JObject.Parse(client.DownloadString(UpdateFile))["files"];

                    Dictionary<string, string> FileQueue = new Dictionary<string, string>();

                    foreach (var file in files)
                    {
                        string path = file["path"].ToString().Replace('/', Path.DirectorySeparatorChar);
                        string hash = file["hash"].ToString();
                        string url = file["url"].ToString();

                        string directory = Path.GetDirectoryName(MainDirectory + Path.DirectorySeparatorChar + path);
                        string FilePath = MainDirectory + Path.DirectorySeparatorChar + path;

                        if (!Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        SHA1Managed sha1 = new SHA1Managed();

                        if (!File.Exists(FilePath) || ByteArrayToString(sha1.ComputeHash(File.ReadAllBytes(FilePath))) != hash)
                        {
                            FileQueue.Add(FilePath, url);
                        }
                    }

                    foreach (var file in FileQueue.Select((Entry, Index) => new { Entry, Index }))
                    {
                        Log("Downloading File: " + file.Entry.Key.Replace(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + Path.DirectorySeparatorChar + "RouterHAK" + Path.DirectorySeparatorChar, ""));
                        client.DownloadFile(file.Entry.Value, file.Entry.Key);
                        totalProgressBar.Invoke((MethodInvoker)delegate
                        {
                            decimal progress = (file.Index + 1m) / FileQueue.Count * 100m;
                            totalProgressBar.Value = (int)Math.Round(progress, 0);
                        });
                    }
                    Log("Download completed.");
                    #endregion
                    #region Unzip + Cleanup Models
                    // Read the ZIP archive.
                    using (ZipFile zip = ZipFile.Read(MainDirectory + Path.DirectorySeparatorChar + "Models.zip"))
                    {
                        // Extract all files.
                        foreach (ZipEntry entry in zip)
                        {
                            Log("Unzipping file: " + entry.FileName);
                            entry.Extract(MainDirectory);
                        }
                    }
                    // Delete the Models.zip archive. This means that the hashcheck will fail each time forcing the models to re-download. THIS IS INTENTIONAL.
                    Log("Deleting Models.zip");
                    File.Delete(MainDirectory  + Path.DirectorySeparatorChar + "Models.zip");
                    // Move the extracted folder and overwrite any old models with the same name.
                    Log("Moving CPE-Models-main to Models folder.");
                    FileSystem.MoveDirectory(MainDirectory + Path.DirectorySeparatorChar + "CPE-Models-main", MainDirectory + Path.DirectorySeparatorChar + "Models", true);
                    Log("Finished, launching...");
                    Process.Start(MainDirectory + Path.DirectorySeparatorChar + "RouterHAK.exe");
                    Thread.Sleep(1000);
                    Environment.Exit(0);
                    #endregion
                }
                else
                {
                    Log("No internet, launching...");
                    Process.Start(MainDirectory + Path.DirectorySeparatorChar + "RouterHAK.exe");
                    Thread.Sleep(1000);
                    Environment.Exit(0);
                }
            }).Start();
        }
    }
}
