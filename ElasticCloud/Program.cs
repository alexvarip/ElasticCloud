using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace Resource_Manager
{
    class Program
    {  
        private static Timer _timer;
        private static int i = 0;
        public static bool ready { get; set; } = true;
        public static bool ready1 { get; set; } = true;
        private static IList<string> _clusterLoad = new List<string>(); 
        private static CloudInfoClass ci = new CloudInfoClass();
        private static ClusterConfigurationClass cl = new ClusterConfigurationClass();

        static void Main(string[] args)
        {

            #region Cloud Information

            Console.WriteLine("Initializing ~Okeanos Knossos Cloud Resource Manager...\n");
            Console.Write("Gathering Cloud Information");
            Console.Write(".");
            Console.Write(".");
            ci.GetCloudInfo("kamaki project list | grep 'cloudcomputing.cs.uth.gr'", "");
            Console.Write(".");
            Console.Write(".");
            ci.GetCloudInfo("kamaki project list | grep 'bigdata.dsml.ece.ntua.gr'", "");
            Console.Write(".");
            Console.Write(".");
            ci.GetCloudInfo($"kamaki project info { ci._projectID } | grep -A 25 'resources'", "");
            Console.Write(".");
            Console.Write(".");
            ci.GetCloudInfo($"kamaki project info { ci._project2ID } | grep -A 25 'resources'", "");
            Console.Write(".");
            Console.Write(".");
            ci.GetCloudInfo("kamaki server list", "");
            Console.Write(".");
            Console.Write(".");
            foreach (var s in ci._serverIDs)
            {   
                ci.GetCloudInfo($"kamaki server info { s } | grep 'ipv4: 192.168.1.'", "");
                ci.GetCloudInfo($"kamaki server info { s } | grep 'tenant_id:'", s);
            }
            ci.GetCloudInfo("kamaki image list", "");
            Console.Write(".");
            Console.Write(".");
            ci.GetCloudInfo("kamaki flavor list", "");
            Console.Write(".");
            Console.Write(".");
            ci.GetCloudInfo("kamaki network list | grep -A 1 'My_Network (bigdata'", "");

            Console.Write("[Done]");

            Console.Write("\n\nStarting Monitor Cluster Process");
            Console.Write(".");
            Console.Write(".");
            Console.Write(".");
            Console.Write(".");
            Console.Write("[Running]\n");

            #endregion


            SetTimer(); 
            Console.WriteLine("\n*************************************************************");
            Console.WriteLine($"# Process started on {DateTime.Now:F}  #");
            Console.WriteLine("*************************************************************\n");
            Console.ReadLine();

            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
            }

        }
    

        static void SetTimer()
        {
            _timer = new Timer(60000);
            _timer.Elapsed += TimerOnElapsed;
            _timer.AutoReset = true;
            _timer.Enabled = true;
        }

        private static void TimerOnElapsed(object sender, ElapsedEventArgs e)
        {

            if (ready is true && ready1 is true)
            {
                ready = false;
                ready1 = false;

                var tuple = cl.MONITOR_CLUSTER(ci, e);
                ready = tuple.Item2;

                _clusterLoad.Add($"{tuple.Item1.ToString()}% at {e.SignalTime}");

                i++;

                if (i is 5)
                {
                    i = 0;

                    Console.WriteLine("\tLast (5) cluster CPU Loads monitored:");

                    var last = _clusterLoad.TakeLast(5);

                    foreach (var l in last)
                    {
                        Console.WriteLine($"\t\t{l}");
                    }
                }
                

                var b = cl.TAKE_DECISION(ci, tuple.Item1, e);
                ready1 = b;
            }
        }


    }
}