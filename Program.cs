using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using LogAnalytics.Client;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace fwlogs2loganalytics
{
    public enum GuardianLogType
    {
        API_Log = 1,
        Unknown_Log = 2
    }

    class Program
    {

        private static Dictionary<string, string> environment = new Dictionary<string, string>()
        {
            {"tableName", "apifirewall_log_1"},
            {"tags", string.Empty},
            {"tickInterval", "10"},
        };

        private static int inTimer = 0;
        private static uint tickInterval = 10;
        private static uint currentTick = 0;

        private static LogAnalyticsClient laclient = null;

        private static List<string> guardianStateFiles = new List<string> { };
        private static List<string> seenFiles = new List<string> { };

        private static ILogger logger;

        static async void WriteEvent(GuardianEntity entity)
        {
            await laclient.SendLogEntry(entity, environment["tableName"]);
        }

        static void ProcessEvironment()
        {
            // Read all the evironment variables
            environment["workspaceId"] = Environment.GetEnvironmentVariable("FW2LA_WORKSPACE_ID");
            environment["workspaceKey"] = Environment.GetEnvironmentVariable("FW2LA_WORKSPACE_KEY");

            if (string.IsNullOrEmpty(environment["workspaceKey"]))
                throw new ArgumentException("No workspace key provided, unable to proceed");

            if (string.IsNullOrEmpty(environment["workspaceId"]))
                throw new ArgumentException("No workspace ID provided, unable to proceed");

            environment["tableName"] = Environment.GetEnvironmentVariable("FW2LA_TABLENAME");
            environment["tableName"] ??= "apifirewall_log_1";

            environment["logs_folder"] = Environment.GetEnvironmentVariable("FW2LA_LOGS_FOLDER");
            environment["logs_folder"] ??= Directory.GetCurrentDirectory();
            environment["state_folder"] = Environment.GetEnvironmentVariable("FW2LA_STATE_FOLDER");
            environment["state_folder"] ??= Path.Join(Directory.GetCurrentDirectory(), ".state");

            environment["tickInterval"] = Environment.GetEnvironmentVariable("FW2LA_TICK_INTERVAL");
            environment["tickInterval"] ??= "10";

            tickInterval = uint.Parse(environment["tickInterval"]);

            foreach (KeyValuePair<string, string> item in environment)
            {
                if (item.Key.Contains("KEY", StringComparison.CurrentCultureIgnoreCase)) {
                    Console.WriteLine("{0} -> {1}", item.Key, GetMaskedToken(item.Value));
                }
                else {
                    Console.WriteLine("{0} -> {1}", item.Key, item.Value);
                }
            }
        }

        static dynamic ExamineFolder(string folder)
        {
            string apiLogFilePath = null;
            string unknownLogFilePath = null;
            string api_id = null;

            if (! seenFiles.Contains(folder))
            {
                seenFiles.Add(folder);
            }

            var allFiles = Directory.EnumerateFiles(folder);

            foreach (var thisFilePath in allFiles)
            {
                Console.WriteLine($"  File = {thisFilePath}");
                string thisFile = Path.GetFileName(thisFilePath);
                Match match = Regex.Match(thisFile, "api-([\\w]{8}-[\\w]{4}-[\\w]{4}-[\\w]{4}-[\\w]{12}).transaction.log");

                if (match.Success)
                {
                    var md5sum = CalculateMD5Sum(thisFilePath);

                    apiLogFilePath = thisFilePath;
                    api_id = match.Groups[1].ToString();
                }

                if ("api-unknown.transaction.log".Equals(thisFile))
                {
                    unknownLogFilePath = thisFilePath;
                }
            }

            // If we're missing either file here we need to quit out 
            if (null == apiLogFilePath)
                return null;
                
            if (null == unknownLogFilePath)
                return null;

            return new { ApiID = api_id, ApiLogFilePath = apiLogFilePath, UnknownLogFilePath = unknownLogFilePath };
        }

        static void ProcessInitialState()
        {
            // Pull this out into a separate method
            List<string> guardianInstanceFolders = null;

            // Check the logs folder exists and exit if not
            if (!Directory.Exists(environment["logs_folder"]))
            {
                throw new DirectoryNotFoundException("Logs source directory does not exist or is not accessible");
            }
            else
            {
                guardianInstanceFolders = new List<string>(Directory.EnumerateDirectories(environment["logs_folder"]));
            }

            // Check the state folder exists, create it if not 
            if (!Directory.Exists(environment["state_folder"]))
            {
                Directory.CreateDirectory(environment["state_folder"]);
            }

            // Create the empty state files
            foreach (var thisFolder in guardianInstanceFolders)
            {
                // TODO : We should check the 'seenFiles' list and skip the folder if we've already seen it ?

                Console.WriteLine($"Folder = {thisFolder}");
                var instance_name = Path.GetFileName(thisFolder);

                var logFiles = ExamineFolder(thisFolder);

                if (null == logFiles)
                {
                    Console.WriteLine($"Skipping folder {thisFolder} due to no logs found");
                    continue;
                }

                // Get the new state based on the log files
                var newGs = new GuardianState();
                var api_gfs = newGs;
                newGs.API_ID = logFiles.ApiID;
                newGs.Instance_Name = instance_name;
                newGs.API_Log_File = GetFileState(logFiles.ApiLogFilePath);
                newGs.Unknown_Log_File = GetFileState(logFiles.UnknownLogFilePath);

                // Get the existing state file if it exists
                string existingStateFilePath = Path.ChangeExtension(Path.Combine(environment["state_folder"], instance_name), ".json");

                if (!File.Exists(existingStateFilePath))
                {
                    Console.WriteLine("State file doesn't exist so writing it now ...");

                    // Here we need to set the lines written to zero or we'll never send the file
                    newGs.API_Log_File.LastLineSent = 0;
                    newGs.Unknown_Log_File.LastLineSent = 0;

                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(newGs, Formatting.Indented);
                    File.WriteAllText(existingStateFilePath, json);
                }
                else
                {
                    Console.WriteLine("State file exists so reading it now ...");

                    // TODO : Why do we read this if we don't use it ?
                    var json = File.ReadAllText(existingStateFilePath);
                    GuardianState response = JsonConvert.DeserializeObject<GuardianState>(json);

                    Console.WriteLine("Done");
                }

                guardianStateFiles.Add(existingStateFilePath);
            }
        }
        private static void ProcessTimeEvent(Object o)
        {
            if (Interlocked.Exchange(ref inTimer, 1) == 0)
            {
                // Display the date/time when this method got called.

                 Console.WriteLine("Sentinel publisher OK at tick " + currentTick);

                // This is designed to rollover - ignore warnings
                currentTick = (ushort)(currentTick + 1);

                ProcessAllLogFiles(guardianStateFiles);

                // Force a garbage collection to occur for this demo.
                //GC.Collect();

                Interlocked.Exchange(ref inTimer, 0);
            }
            else

                    Console.WriteLine("... skipping timer tick");

        }
        private static void ProcessLogFile(string stateFile, GuardianLogType logType = GuardianLogType.API_Log)
        {
            // Get the current state file
            if (!File.Exists(stateFile))
                throw new FileNotFoundException($"State file not found -> {stateFile}");

            GuardianState savedState = JsonConvert.DeserializeObject<GuardianState>(File.ReadAllText(stateFile));

            string logname = logType == GuardianLogType.API_Log ? savedState.API_Log_File.FileName : savedState.Unknown_Log_File.FileName;
            GuardianFileState gfs = logType == GuardianLogType.API_Log ? savedState.API_Log_File : savedState.Unknown_Log_File;

            // Process the log file
            string thisLogFile = Path.Combine(environment["logs_folder"], savedState.Instance_Name, logname);

            if (!File.Exists(thisLogFile))
                throw new FileNotFoundException($"Log file not found -> {thisLogFile}");

            // Process the file if the size has changed or the lines sent is zero
            if (gfs.FileSize != ((new System.IO.FileInfo(thisLogFile)).Length) || gfs.LastLineSent == 0)
            {
                // Read the full file 
                Console.WriteLine($"Processiong log file -> {thisLogFile}");

                var logLines = File.ReadAllLines(thisLogFile);

                //  The weakness here is we have to read the entire file in one go - might have to change this
                for (int i = gfs.LastLineSent; i < logLines.Count(); i++)
                {
                    Console.WriteLine($"  process {i}");
                    var log = JObject.Parse(logLines[i]);

                    // Now parse out all the data and post it
                    GuardianEntity ge = new GuardianEntity(
                                            logType,
                                            log["uuid"].Value<string>(),
                                            log["pod"]["instance_name"].Value<string>(),
                                            null,
                                            log["date_epoch"].Value<long>(),
                                            log["api"].Value<string>(),
                                            log["api_name"].Value<string>(),
                                            log["non_blocking_mode"].Value<string>(),
                                            log["source_ip"].Value<string>(),
                                            log["source_port"].Value<uint>(),
                                            log["destination_ip"].Value<string>(),
                                            log["destination_port"].Value<uint>(),
                                            log["protocol"].Value<string>(),
                                            log["hostname"].Value<string>(),
                                            log["uri_path"].Value<string>(),
                                            log["method"].Value<string>(),
                                            log["status"].Value<uint>(),
                                            log["query"].Value<string>(),
                                            log["params"]["request_header"].ToString(),
                                            log["params"]["response_header"].ToString(),
                                            log["errors"].ToString(),
                                            null);

                    Console.WriteLine(ge);

                    // Send the event now
                    WriteEvent(ge);
                }

                // Save the new state
                var newFs = GetFileState(thisLogFile);

                if (logType == GuardianLogType.API_Log)
                    savedState.API_Log_File = newFs;
                else
                    savedState.Unknown_Log_File = newFs;

                File.WriteAllText(stateFile, Newtonsoft.Json.JsonConvert.SerializeObject(savedState, Formatting.Indented));
            }
        }

        private static void ShowUsage()
        {
            Console.WriteLine("\n------------------------------------------------------------------------------------------------------------");
            Console.WriteLine("\n42Crunch Logs Analytics publisher - Ingests 42Crunch APIFirewall log files and pushes to Azure Log Analytics");
            Console.WriteLine("\nUsage via Docker:");
            Console.WriteLine("  docker run -d --rm -v ?:? guardian2LA");
            Console.WriteLine("\nEnvironment variables:");
            Console.WriteLine("  FW2LA_WORKSPACE_ID : Auzre Log Analytics workspace ID");
            Console.WriteLine("  FW2LA_WORKSPACE_KEY : Auzre Log Analytics primary/secondary key");
            Console.WriteLine("  FW2LA_LOGS_FOLDER : Guardian log file location (via a -v mount)");
            Console.WriteLine("  FW2LA_STATE_FOLDER : State file location (defaults to ./.state)");
            Console.WriteLine("  FW2LA_TABLENAME : Azure Log Analytics table name (defaults to guardian_log)");
            Console.WriteLine("  FW2LA_TICK_INTERVAL : File processing interval in seconds (defaults to 10 seconds)");
            Console.WriteLine();

        }
        private static void ProcessAllLogFiles(List<string> stateFiles)
        {
            foreach (var stateFile in stateFiles)
            {
                Console.WriteLine($"Processing state file -> {stateFile}");
                ProcessLogFile(stateFile, GuardianLogType.API_Log);
                ProcessLogFile(stateFile, GuardianLogType.Unknown_Log);
            }
        }

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("API Firewall to LogsAnalytics publisher starting");

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("NonHostConsoleApp.Program", LogLevel.Warning)
                    .AddConsole();
            });

            logger = loggerFactory.CreateLogger<Program>();

            if (args.Contains("--help"))
            {
                ShowUsage();
                return 1;
            }

            // Get all the environment variables - this may throw an exception 
            ProcessEvironment();
            // Allocate a client
            laclient = new LogAnalyticsClient(
                                workspaceId: environment["workspaceId"],
                                sharedKey: environment["workspaceKey"]);

            // Hook the shutdown events so we can shutdown gracefully
            var tcs = new TaskCompletionSource<bool>();
            var sigintReceived = false;

            AppDomain.CurrentDomain.ProcessExit += (_, ea) =>
            {
                if (!sigintReceived)
                {
                    Console.WriteLine("Received SIGTERM");
                    tcs.SetResult(true);
                }
                else
                {
                    Console.WriteLine("Received SIGTERM, ignoring it because already processed SIGINT");
                }
            };

            Console.CancelKeyPress += (_, ea) =>
            {
                // Tell .NET to not terminate the process
                ea.Cancel = true;

                Console.WriteLine("Received SIGINT (Ctrl+C)");
                sigintReceived = true;
                tcs.SetResult(true);
            };

            ProcessInitialState();

            // Now we can start the time now
            Timer timer = new Timer(ProcessTimeEvent, null, 0, tickInterval * 1000);

            // Wait here forever or until the process is shut down
            await tcs.Task;
            timer.Dispose();

            Console.WriteLine("42logs2sentinel program stopping");
            return 0;
        }

        private static string GetMaskedToken(string inputToken, uint showCount = 8, char maskValue = '*')
        {
            StringBuilder opStr = new StringBuilder();
            foreach (var c in inputToken.ToCharArray().Select((value, index) => new { value, index }))
            {
                if (c.index < (inputToken.Length - showCount) && c.value != '-')
                {
                    opStr.Append(maskValue);
                }
                else
                {
                    opStr.Append(c.value);
                }
            }

            return opStr.ToString();
        }
        private static string CalculateMD5Sum(string inputFile)
        {
            // Use the method found here:
            // https://stackoverflow.com/questions/16380477/net-md5-doesnt-equal-md5sum-exe-on-unix
            byte[] hash;
            var inputText = File.ReadAllText(inputFile);

            using (MD5 md5 = MD5.Create())
            {
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes(inputText));
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }

            return sb.ToString();
        }

        private static GuardianFileState GetFileState(string logFile, bool onlyFileSize = false)
        {
            var gfs = new GuardianFileState();

            gfs.FileName = Path.GetFileName(logFile);
            gfs.FileSize = ((new System.IO.FileInfo(logFile)).Length);

            if (onlyFileSize == false)
            {
                gfs.MD5Sum = CalculateMD5Sum(logFile);
                gfs.LastLineSent = File.ReadLines(logFile).Count();
            }

            return gfs;
        }

        private class GuardianState
        {
            public string API_ID { get; set; }
            public string Instance_Name { get; set; }
            public GuardianFileState API_Log_File { get; set; }
            public GuardianFileState Unknown_Log_File { get; set; }
        }
        private class GuardianFileState
        {
            public string FileName { get; set; }
            public long FileSize { get; set; }
            public string MD5Sum { get; set; }
            public int LastLineSent { get; set; }
        }
    }
}
