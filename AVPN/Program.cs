using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace AVPN
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>

        public static string AVPNName = "AVPN";
        public static string AVPNnetName = "AVPNnet";

        public static string RunProcess(string app, string param)
        {
            string q = string.Empty;
            System.Diagnostics.Process tmpproc = new System.Diagnostics.Process();
            tmpproc.StartInfo = new System.Diagnostics.ProcessStartInfo(app, param);
            tmpproc.StartInfo.RedirectStandardOutput = true;
            tmpproc.StartInfo.UseShellExecute = false;
            tmpproc.StartInfo.StandardOutputEncoding = Encoding.GetEncoding(866);
            tmpproc.Start();

            while (!tmpproc.HasExited)
            {
                q += tmpproc.StandardOutput.ReadToEnd();
            }

            return q;
        }


        public static bool GetService(string srvname, bool onlyRun = false)
        {
            bool find = false;
            ServiceController[] scServices;
            scServices = ServiceController.GetServices();
            
            foreach (ServiceController scTemp in scServices)
            {                
                if (scTemp.ServiceName == srvname)
                {
                    if (onlyRun)
                    {
                        if (scTemp.Status == ServiceControllerStatus.Running)
                        {
                            find = true;
                        }
                    }
                    else
                    {
                        find = true;
                    }
                }
            }
            return find;
        }

        static void Main(string[] args)
        {
            if (System.Environment.UserInteractive)
            {
                string q = string.Empty;
                string app = string.Empty;
                string param = string.Empty;
                string exelocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

                string[] tmpArgs = Environment.GetCommandLineArgs();

                if (tmpArgs.Length > 1)
                {
                    if (tmpArgs[1] == "install")
                    {
                        string otherparam = "";
                        for (int i = 2; i < tmpArgs.Length; i++)
                        {
                            otherparam += tmpArgs[i];
                            if (i + 1 < tmpArgs.Length)
                            {
                                otherparam += " ";
                            }
                        }

                        //install main service
                        app = "SC";
                        param = string.Format("create {0} DisplayName= \"{0}\" binpath= \"{1} {2}\" start= auto", AVPNName, exelocation, otherparam);                                                
                        Console.WriteLine("Run command: {0} {1}:", app, param);
                        q = RunProcess(app, param);
                        Console.WriteLine(q);
                        
                        Thread.Sleep(2000);

                        //run main service
                        app = "SC";
                        param = "start " + AVPNName;
                        Console.WriteLine("Run command: {0} {1}:", app, param);
                        q = RunProcess(app, param);
                        Console.WriteLine(q);

                    }
                    if (tmpArgs[1] == "remove")
                    {
                        //if main service state "running", first stop it
                        if (GetService(AVPNName, true))
                        {
                            app = "SC";
                            param = "stop " + AVPNName;
                            Console.WriteLine("Run command: {0} {1}:", app, param);
                            q = RunProcess(app, param);
                            Console.WriteLine(q);
                            Thread.Sleep(2000);
                        }
                        
                        //remove main service
                        app = "SC";
                        param = "delete " + AVPNName;
                        Console.WriteLine("Run command: {0} {1}:", app, param);
                        q = RunProcess(app, param);
                        Console.WriteLine(q);

                        //if netsrv is installed, remove it too
                        if (GetService(AVPNnetName))
                        {
                            //if netsrv state "running", first stop it
                            if (GetService(AVPNnetName, true))
                            {
                                app = "SC";
                                param = "stop " + AVPNnetName;
                                Console.WriteLine("Run command: {0} {1}:", app, param);
                                q = RunProcess(app, param);
                                Console.WriteLine(q);
                                Thread.Sleep(2000);
                            }

                            app = "SC";
                            param = "delete " + AVPNnetName;
                            Console.WriteLine("Run command: {0} {1}:", app, param);
                            q = RunProcess(app, param);                            
                            Console.WriteLine(q);
                        }
                    }
                }
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                new AVPN()
                };
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}
