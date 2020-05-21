using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Timers;

namespace AVPN
{
    public partial class AVPN : ServiceBase
    {
        System.Timers.Timer Timer = new System.Timers.Timer();
        System.Timers.Timer TimerNetsrv = new System.Timers.Timer();
        int Interval = 0; // 10000 ms = 10 second  
        string CheckHostAddr = string.Empty; //ping this address to check client location (insite or outsite)
        string VPNConnectionName = string.Empty;
        string VPNUser = string.Empty;
        string VPNPass = string.Empty;
        string ConfigURL = string.Empty;
        bool ReadConfigURL = false;
        string ConfigStr = string.Empty;
        bool timertic = false;
        string logpath = AppDomain.CurrentDomain.BaseDirectory;
        int logsize = 99999999;
        static string APipeName = "AVPNpipe";
        static int numThreads = 4;
        Thread[] PipeServers = new Thread[numThreads];
        bool asNetsrv = false;

        public static bool VPNConnectStatus(string vpnname)
        {
            bool rt = false;
            NetworkInterface[] networkCards = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface ncard in networkCards)
            {
                if (ncard.Description == vpnname)
                {
                    rt = true;
                }
            }
            return rt;
        }

        public static string ReadFromURL(string fileURL)
        {
            try
            {
                var client = new WebClient();
                var stream = client.OpenRead(fileURL);
                var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                return content;
            }
            catch
            {
                return null;
            }
        }

        public void RunProcess(string app, string param, bool netsrv = false)
        {
            string tmp = "Run";
            if (netsrv)
            {
                tmp += " (as NetworkService)";
            }
            WriteLog(string.Format("{0}: {1} {2}", tmp, app, param));

            string q = "";
            DateTime startt = DateTime.Now;

            if (netsrv)
            {
                WriteLog("Try connect to net service");
                try
                {
                    NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", APipeName, PipeDirection.InOut);
                    pipeClient.Connect(2000);
                    WriteLog("Success connect to net service");

                    StreamReader sreader = new StreamReader(pipeClient);
                    StreamWriter swriter = new StreamWriter(pipeClient);

                    swriter.WriteLine(app + " " + param);
                    swriter.Flush();

                    startt = DateTime.Now;

                    q += sreader.ReadToEnd();
                }
                catch (Exception ex)
                {
                    WriteLog(string.Format("Connect to net service error: {0}", ex.Message));
                }
            }
            else
            {
                System.Diagnostics.Process tmpproc = new System.Diagnostics.Process();
                tmpproc.StartInfo = new System.Diagnostics.ProcessStartInfo(app, param);
                tmpproc.StartInfo.RedirectStandardOutput = true;
                tmpproc.StartInfo.UseShellExecute = false;
                tmpproc.StartInfo.StandardOutputEncoding = Encoding.GetEncoding(866);
                tmpproc.Start();
                startt = DateTime.Now;
                while ((!tmpproc.HasExited) && ((DateTime.Now - startt).TotalSeconds < 5))
                {
                    q += tmpproc.StandardOutput.ReadToEnd();
                }

            }

            WriteLog(q);
        }

        public void ApplyRoutes(string routes, bool delete = false)
        {
            if (routes != null)
            {
                string[] rtmass = routes.Split('\n');
                foreach (string rt in rtmass)
                {
                    if (rt.Contains("[routes]"))
                    {

                    }
                    else
                    {
                        string tmp = rt.Replace("\r", "");
                        string[] rtsplit = tmp.Split(' ');

                        if (rtsplit.Length == 3)
                        {
                            if (delete)
                            {
                                RunProcess("route", string.Format("delete {0}", rtsplit[0]), false);
                            }
                            else
                            {
                                RunProcess("route", string.Format("add {0} mask {1} {2}", rtsplit[0], rtsplit[1], rtsplit[2]), false);
                            }

                        }
                    }
                }
            }
        }

        public static bool PingHost(string hostaddr)
        {
            bool pingresult = false;

            if (hostaddr == "") { return false; }

            IPAddress[] ipaddrs = null;
            try
            {
                ipaddrs = Dns.GetHostAddresses(hostaddr);
            }
            catch { }

            if (ipaddrs != null)
            {
                foreach (IPAddress ip in ipaddrs)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            Ping myPing = new Ping();
                            PingReply reply = myPing.Send(ip.ToString(), 4000);
                            if (reply != null)
                            {
                                if (reply.Status == IPStatus.Success)
                                {
                                    pingresult = true;
                                    break;
                                }
                            }
                        }
                        catch
                        {

                        }
                    }
                    if (pingresult) { break; }
                }
            }

            return pingresult;
        }

        public void WriteLog(string logMessage, bool addTimeStamp = true)
        {
            var path = logpath;
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);


            var filePath = String.Format("{0}\\{1}_{2}.txt", path, "log", ServiceName);

            if (addTimeStamp)
            {
                logMessage = String.Format("[{0}] - {1}\r\n", DateTime.Now.ToString("yyyyMMdd-HH:mm:ss", CultureInfo.CurrentCulture), logMessage);
            }

            if (File.Exists(filePath))
            {

                var FileSize = Convert.ToDecimal((new System.IO.FileInfo(filePath)).Length);
                if (FileSize > logsize)
                {
                    var file = File.ReadAllLines(filePath).ToList();
                    var AmountToCull = (int)(file.Count * 0.5); //trim 50% of file
                    string[] trimmed = file.Skip(AmountToCull).ToArray();// ToList().ToArray();
                    File.WriteAllLines(filePath, trimmed);
                }
            }

            File.AppendAllText(filePath, logMessage);
        }

        public AVPN()
        {
            InitializeComponent();
            this.ServiceName = "AVPN";
        }

        protected override void OnStart(string[] args)
        {
            string[] tmpArgs = Environment.GetCommandLineArgs();

            if (tmpArgs.Length > 1)
            {
                if (tmpArgs[1] == "netsrv")
                {
                    //Run as net service
                    TimerNetsrv.Elapsed += new ElapsedEventHandler(OnElapsedTimeNetsrv);
                    TimerNetsrv.Interval = 1000;
                    TimerNetsrv.Enabled = true;
                }
                else
                {
                    //normal run
                    if ((tmpArgs.Length - 1) % 2 == 0)
                    {
                        //parse first command line
                        for (int i = 1; i < tmpArgs.Length; i = i + 2)
                        {
                            switch (tmpArgs[i])
                            {
                                case "-logpath":
                                    logpath = tmpArgs[i + 1];
                                    break;
                            }
                        }

                        //start write log only after set log location
                        WriteLog("Service has been started");

                        //pars other command line
                        for (int i = 1; i < tmpArgs.Length; i = i + 2)
                        {
                            switch (tmpArgs[i])
                            {
                                case "-vpnname":
                                    VPNConnectionName = tmpArgs[i + 1];
                                    WriteLog("VPN connection name set to: " + VPNConnectionName);
                                    break;
                                case "-checkhost":
                                    CheckHostAddr = tmpArgs[i + 1];
                                    WriteLog("CheckHost address set to: " + CheckHostAddr);
                                    break;
                                case "-cfgurl":
                                    ConfigURL = tmpArgs[i + 1];
                                    WriteLog("Configuration file URL set to: " + ConfigURL);
                                    break;
                                case "-interval":
                                    if (int.TryParse(tmpArgs[i + 1], out Interval))
                                    {
                                        Interval *= 1000;
                                        WriteLog("Check interval set to (ms): " + Interval);
                                    }
                                    else
                                    {
                                        WriteLog("Check interval incorrect");
                                    }
                                    break;
                                case "-user":
                                    VPNUser = tmpArgs[i + 1];
                                    break;
                                case "-pass":
                                    VPNPass = tmpArgs[i + 1];
                                    break;
                                case "-logsize":
                                    if (int.TryParse(tmpArgs[i + 1], out logsize))
                                    {
                                        logsize *= 1000;
                                        WriteLog("Maximum log size set to (byte): " + logsize);
                                    }
                                    else
                                    {
                                        WriteLog("log size incorrect");
                                    }
                                    break;
                                case "-netsrv": //create and run net service
                                    WriteLog("NETWORK SERVICE mode Enabled");
                                    asNetsrv = true;
                                    string exelocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                                    string otherparam = "netsrv";
                                    string app = "SC";
                                    string param = string.Format("create {0} DisplayName= \"{0}\" binpath= \"{1} {2}\" start= auto obj= \"NT Authority\\NetworkService\"", Program.AVPNnetName, exelocation, otherparam);
                                    WriteLog("Create service AVPNnet");
                                    string q = Program.RunProcess(app, param);
                                    WriteLog(q);

                                    WriteLog("Run service AVPNnet");
                                    param = string.Format("start {0}", Program.AVPNnetName);
                                    q = Program.RunProcess(app, param);
                                    WriteLog(q);

                                    break;
                            }
                        }
                    }
                    else
                    {
                        WriteLog("ERROR: incorrect commandline arguments");
                    }
                    if (Interval == 0)
                    {
                        Interval = 10000;
                        WriteLog("Check interval not set. Use default value (ms): " + Interval);
                    }

                    if (logsize == 99999999)
                    {
                        logsize = 2000000;
                        WriteLog("Maximum log size not set. Use to default value (byte): " + logsize);
                    }

                    if (CheckHostAddr == "")
                    {
                        var serverName = System.Environment.MachineName; //host name sans domain
                        var fqhn = System.Net.Dns.GetHostEntry(serverName).HostName; //fully qualified hostname

                        serverName = serverName.ToLower();
                        fqhn = fqhn.ToLower();

                        var domain = fqhn.Replace(serverName + ".", "");

                        if (domain == fqhn)
                        {
                            Console.WriteLine("Computer not joined to domain");
                            WriteLog("CheckHost address not set! Computer not joined to domain. Set checkhost using parametr -checkhost");
                        }
                        else
                        {
                            CheckHostAddr = domain;
                            WriteLog("CheckHost address not set. Use domain name: " + CheckHostAddr);
                        }
                    }

                    Timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
                    Timer.Interval = Interval;
                    Timer.Enabled = true;
                }
            }


        }

        protected override void OnStop()
        {
            string[] tmpArgs = Environment.GetCommandLineArgs();
            if (tmpArgs.Length > 1)
            {
                //run as netsrv
                if (tmpArgs[1] == "netsrv")
                {
                    TimerNetsrv.Stop();
                    for (int j = 0; j < numThreads; j++)
                    {
                        if (PipeServers[j] != null)
                        {
                            PipeServers[j].Abort();
                            if (PipeServers[j].Join(10))
                            {
                                PipeServers[j] = null;
                            }
                        }
                    }
                    for (int j = 0; j < numThreads; j++)
                    {
                        if (PipeServers[j] != null)
                        {
                            NamedPipeClientStream clt = new NamedPipeClientStream(".", AVPN.APipeName);
                            clt.Connect(200);
                            clt.Close();
                        }
                    }
                    for (int j = 0; j < numThreads; j++)
                    {
                        if (PipeServers[j] != null)
                        {
                            if (PipeServers[j].Join(10000))
                            {
                                PipeServers[j] = null;
                            }
                        }
                    }
                }
                else
                {
                    //run normal
                    Timer.Stop();
                    if (ConfigStr != null)
                    {
                        ApplyRoutes(ConfigStr, true);
                        ConfigStr = null;
                    }

                    //if netsrv installed, stop it
                    if (asNetsrv)
                    {
                        string app = "SC";
                        string param = string.Format("stop {0}", Program.AVPNnetName);
                        WriteLog("Stop service AVPNnet");
                        string q = Program.RunProcess(app, param);
                        WriteLog(q);
                    }

                    WriteLog("Service has been stopped.");
                }
            }


        }

        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            if (!timertic)
            {
                timertic = true;
            }
            else
            {
                return;
            };
            WriteLog(String.Format("{0} ms elapsed.", Interval));

            WriteLog("Check VPN connection status \"" + VPNConnectionName + "\"");
            bool vpnstatus = VPNConnectStatus(VPNConnectionName);
            if (vpnstatus)
            {
                WriteLog("VPN Connection \"" + VPNConnectionName + "\" status: UP");
                //read routes config
                if (!ReadConfigURL)
                {
                    ReadConfigURL = true;
                    WriteLog("Try read config from URL \"" + ConfigURL + "\"");
                    ConfigStr = ReadFromURL(ConfigURL);
                    if (ConfigStr != null)
                    {
                        WriteLog("Read config from URL \"" + ConfigURL + "\": Success");
                        ApplyRoutes(ConfigStr, false);
                    }
                    else
                    {
                        WriteLog("Read config from URL \"" + ConfigURL + "\": Fail");
                    }
                }
            }
            else
            {
                WriteLog("VPN Connection \"" + VPNConnectionName + "\" status: DOWN");

                ReadConfigURL = false;
                if (ConfigStr != null)
                {
                    ApplyRoutes(ConfigStr, true);
                    ConfigStr = null;
                }

                bool pingr = PingHost(CheckHostAddr);
                if (pingr)
                {
                    WriteLog("Ping host \"" + CheckHostAddr + "\" success");
                }
                else
                {
                    WriteLog("Ping host \"" + CheckHostAddr + "\" fail");

                    WriteLog("try to connect VPN: " + VPNConnectionName);

                    RunProcess("rasdial.exe", VPNConnectionName + " " + VPNUser + " " + VPNPass + " /phonebook:C:\\ProgramData\\Microsoft\\Network\\Connections\\Pbk\\rasphone.pbk", asNetsrv);
                }
            }

            timertic = false;
        }

        private void OnElapsedTimeNetsrv(object source, ElapsedEventArgs e)
        {
            if (!timertic)
            {
                timertic = true;
            }
            else
            {
                return;
            };
            for (int j = 0; j < numThreads; j++)
            {
                if (PipeServers[j] != null)
                {
                    if (PipeServers[j].Join(50))
                    {
                        PipeServers[j] = null;
                    }
                }
                else
                {
                    PipeServers[j] = new Thread(PipeServerThread);
                    PipeServers[j].Start();
                }
            }
            timertic = false;
        }

        static void PipeServerThread(object data)
        {
            NamedPipeServerStream pipeServer = new NamedPipeServerStream(APipeName, PipeDirection.InOut, numThreads);
            try
            {
                // Wait for a client to connect
                pipeServer.WaitForConnection();

                StreamReader sr = new StreamReader(pipeServer);
                StreamWriter sw = new StreamWriter(pipeServer);

                string tempread = sr.ReadLine();

                if (tempread.Contains("rasdial"))
                {
                    string param = tempread.Replace("rasdial.exe ", "");
                    string runout = Program.RunProcess("rasdial", param);
                    sw.WriteLine(runout);
                    sw.Flush();
                }
                else
                {
                    sw.WriteLine("error: no valid command");
                    sw.Flush();
                }
            }
            catch
            {

            }
            finally
            {
                pipeServer.Close();
            }

        }
    }
}
