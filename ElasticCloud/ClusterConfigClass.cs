using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Timers;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Resource_Manager
{
    public class ClusterConfigurationClass
    {
        private double _loadH = 50;
        private double _loadL = 20;
        private double averageCPULoad { get; set; }
        string[] jokes = { "Hear about the new restaurant called Karma?\n There’s no menu: You get what you deserve.",
                           "Why couldn't the bicycle stand on its own?\n It was two tired.",
                           "What is an astronaut's favorite part on a computer.\n The space bar!",
                           "Why did the turkey cross the road twice?\n To prove it wasn't a chicken!",
                           "A neutron walks into a bar and asks the barman \"how much is it for a drink?\".\n The barman says \"for you no charge\". ",
                           "Why did the invisible man turn down the offer?\n Because he can't see himself doing it.",
                           "My wife told me to take the spider out instead of killing it.\n We went out, had some drinks. Nice guy.\n He's a web designer.",
                           "Why did Shakespeare only write in pen?\n Pencils confused him. 2B or not 2B?"        
                         };

        CloudInfoClass ci = new CloudInfoClass();


        #region Main Functions
        public Tuple<double, bool> MONITOR_CLUSTER(CloudInfoClass ci, ElapsedEventArgs e)
        {   
            Console.WriteLine($"\n~ Event raised on: { e.SignalTime } [Event: Monitoring]\n");

            double load = 0;
            double temp = 0;
            double i = 0;

            foreach (var s in ci._serverIPs)
            {
                if (!s.Equals(ci._LoadBalancerIP))
                {
                    Environment.CurrentDirectory = "/home/user";
                    AuthenticationMethod passwd = new PrivateKeyAuthenticationMethod("user", new PrivateKeyFile[]{
                    new PrivateKeyFile("/home/user/.ssh/id_rsa", "")});
                    ConnectionInfo connection = new ConnectionInfo(s, "user", passwd);

                    var client = new SshClient(connection);
                    if (!client.IsConnected)
                    {
                        //Console.WriteLine("\nConnecting now to client " + connection.Host + "...");
                        
                        // Accept Host Key
                        client.HostKeyReceived += delegate (object sender, HostKeyEventArgs e)
                        {
                            e.CanTrust = true;
                        };

                        client.Connect();
                    }            

                    var c = client.CreateCommand("mpstat -P ALL 2 1| grep all | awk '{print$13}'");

                    c.Execute();
                    
                    if (!c.Result.Equals(null))
                    {
                        var result = c.Result.Split("\n", StringSplitOptions.RemoveEmptyEntries);
                        
                        temp = 100d - Convert.ToDouble(result[0]);
                        load += temp;
                        i++;
                    }


                    Console.WriteLine($"\tServer [{connection.Host}] CPU Load: {temp}%");

                    client.Disconnect();
                    //Console.WriteLine("\nDisconnected from client " + connection.Host + "\n");

               }

            }
            
            averageCPULoad = load / i;
            
            Console.WriteLine("\n\tAverage CPU Load: " + averageCPULoad + "%\n");

            return new Tuple<double, bool>(averageCPULoad, true);
        }
    
        public bool TAKE_DECISION(CloudInfoClass ci, double averageCPULoad, ElapsedEventArgs e)
        {
            Console.WriteLine($"\n~ Event raised on: { e.SignalTime } [Event: Decision]\n");

            ci._projectInfo.TryGetValue("cyclades.vm:", out string value);
            ci._project2Info.TryGetValue("cyclades.vm:", out string value2);

            int maxsize = Convert.ToInt32(value) + Convert.ToInt32(value2);

            // If average CPU Load above 50%
            if (averageCPULoad >= _loadH)
            {
                // Check first available resources
                if (ci._clusterSize >= 2 && ci._clusterSize < maxsize)
                    ADD_NODE(ci);
                else
                    Console.WriteLine("\t(413) REQUEST ENTITY TOO LARGE overLimit (Resource Limit Exceeded for your account.)"
                    + "\n\t|  Limit for resource 'Virtual Machine' exceeded for your account. Available: 0, Requested: 1"
                    + "\n\n\tCancelling <ADD_NODE> Task...[Done]\n");
            }
            // If average CPU Load below 20%
            else if (averageCPULoad <= _loadL)
            {
                // Check first if servers are 2 or more (<= available resources)
                if (ci._clusterSize > 2 && ci._clusterSize <= maxsize)
                    REMOVE_NODE(ci);
                else
                    Console.WriteLine("\t(ACTION NOT ALLOWED) REQUEST ENTITY belowLimit (Resource Low Limit Exceeded"
                    + " for your account.)\n\t|   Low Limit for resource 'Virtual Machine' exceeded for your account. Available: 0, Requested: 1"
                    + "\n\n\tCancelling <REMOVE_NODE> Task...[Done]\n");
            }
            else
                Console.WriteLine("\n\t[Decision]: Everything looks good!!! No need for further action.");


            return true;
        }

        private void ADD_NODE(CloudInfoClass ci)
        {
            ADD_VM(ci);            
        }
        
        private void REMOVE_NODE(CloudInfoClass ci)
        {
            string id = "";
            string ip = "";

            foreach (var s in ci._serverIDs)
            {
                if (!s.Equals(ci._LoadBalancerID) && !s.Equals(ci._MainServerID))
                {
                    id = s;
                    break;
                }
            }

            string[] result = { };

            string cmd = $"kamaki server info {id} | grep 'ipv4: 192.168.1.'";

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \" " + cmd + " \"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            result = process.StandardOutput.ReadToEnd().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            process.WaitForExit();

            if (result.Length > 1)
                ip = result[1].TrimEnd();


            CONFIGURE_CLUSTER_REMOVE(ci, ip);
            REMOVE_VM(ci, id, ip);
        }

        #endregion


        #region Subfunctions
            
        private void ADD_VM(CloudInfoClass ci)
        {
            ci._imageIDs.TryGetValue("Ubuntu Server LTS", out string ivalue);
            ci._projectInfo.TryGetValue("cyclades.vm:", out string value);
            ci._project2Info.TryGetValue("cyclades.vm:", out string value2);


            string cmd = "";
            bool k1 = false;
            bool k2 = false;

            Console.WriteLine("\n");

            // Check which project has available space left and add vm to it
            if (ci._assignedVMsToProject.Count < Convert.ToInt32(value))
            {
                cmd = $"kamaki server create --name=\"Server\" --network={ci._networkID} --flavor-id=260 --image-id={ivalue} --project-id={ci._projectID} "
                    + "-p /home/user/.ssh/id_rsa.pub,/user/.ssh/authorized_keys,user,user,0777, -w"; 
                    //+ "--key-name \"\"My Uploaded Key\"\" -w";
                k1 = true;
            }             
            else if (ci._assignedVMsToProject2.Count < Convert.ToInt32(value2))
            {
                cmd = $"kamaki server create --name=\"Server\" --network={ci._networkID} --flavor-id=260 --image-id={ivalue} --project-id={ci._project2ID} "
                     + "-p /home/user/.ssh/id_rsa.pub,/home/user/.ssh/authorized_keys,user,user,0777 -w";  
                     //+ "--key-name \"\"My Uploaded Key\"\" -w ";
                k2 = true;
            }

            string[] result = { };
            string id = "";
            string pass = "";

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \" " + cmd + " \"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            result = process.StandardOutput.ReadToEnd().Split("\n", StringSplitOptions.RemoveEmptyEntries);
            process.WaitForExit();


            foreach (var s in result)
            {
                if (s.Contains("adminPass:"))
                {
                    string[] s1 = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    pass = s1[1].TrimEnd();
                }

                if (s.StartsWith("id:"))
                {
                    string[] s1 = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    id = s1[1].TrimEnd();
                }                
            }


            if (k1 == true)
                ci._assignedVMsToProject.Add(id);
            else if (k2 == true)
                ci._assignedVMsToProject2.Add(id);


            ci.GetCloudInfo($"kamaki server wait { id } --until ACTIVE", id);
            
            
            string[] result1 = {};
            string cmd1 = $"kamaki server info { id } | grep 'ipv4: 192.168.1.'";

            var process1 = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \" " + cmd1 + " \"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process1.Start();
            result1 = process1.StandardOutput.ReadToEnd().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            process1.WaitForExit();

            string ip = result1[1].TrimEnd();

            ci._serverPass.Add(ip, pass);

            // Update corresponding properties
            ci.GetCloudInfo("kamaki server list", "");

            
            Console.WriteLine("\nGetting things ready...\n");

            // !!!
            bool q = false;
            bool w = false;
            Parallel.Invoke( delegate () { System.Threading.Thread.Sleep(25000); q = true; },
                             
                             delegate () 
                             {
                                Random rn = new Random();

                                int num = rn.Next(0, jokes.Length);
                                Console.WriteLine("\"" + jokes[num] + "\" \n\t");
                             },

                             delegate () {
                                    while(true)
                                    {
                                        for (int dots = 0; dots < 3; ++dots)
                                        {
                                            Console.Write('.');
                                            System.Threading.Thread.Sleep(1000);
                                            if (dots == 2)
                                            {
                                                if (q == true)
                                                {
                                                    w = true;
                                                    break;
                                                }

                                                Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
                                                dots = -1;
                                                System.Threading.Thread.Sleep(1000);
                                            }
                                        }

                                        if (w == true) 
                                            break;
                                    }
                             }
                             
            );  

             
            Console.WriteLine("\n");

            
            CONFIGURE_VM(ci, id, ip, pass);
        } 

        private void CONFIGURE_VM(CloudInfoClass ci, string id, string ip, string pass)
        {
            bool b = false;

            // string scriptText = "#!/bin/bash \n\n# Program for Fibonacci Series \n# Static input N " +
            //                     "\nN=400 \n# First Number \na=0 \n# Second Number \nb=1" +
            //                     "\n\nfor (( i =0; i<N; i++ )) \ndo \n\tfn=\\$((a+b)) " + 
            //                     "\n\ta=\\$b\n\tb=\\$fn \ndone";

            string loadScript = "fulload() { dd if=/dev/zero of=/dev/null & }; fulload; sleep 10; killall dd";
   

            Environment.CurrentDirectory = "/home/user";
            AuthenticationMethod passwd = new PrivateKeyAuthenticationMethod("user", new PrivateKeyFile[]{
                new PrivateKeyFile("/home/user/.ssh/id_rsa", "")});
            AuthenticationMethod method = new PasswordAuthenticationMethod("user", pass);
            ConnectionInfo connection = new ConnectionInfo(ip, "user", method);

            var client = new SshClient(connection);
            if (!client.IsConnected)
            {
                Console.WriteLine("\nConnecting now to client " + connection.Host + "...");
                
                // Accept Host Key
                client.HostKeyReceived += delegate (object sender, HostKeyEventArgs e)
                {
                    e.CanTrust = true;
                };

                client.Connect();
            }

            Console.WriteLine("\n> Downloading and installing essential packages and libraries...\n\t");

            // - install nginx, nginx-extras, sysstat
            var c = client.CreateCommand($"echo {pass} | sudo -S apt-get update && " + 
                                          "sudo -S apt-get install -y nginx nginx-extras sysstat");

            var c1 = client.CreateCommand( $"echo {pass} " + @"| sudo -S sed -i '0,/ =404;/!b;//a\" +
                                           "content_by_lua_block { " + 
                                           "os.execute(\"/bin/loadScript\") " +
                                           "ngx.header[\"Content-type\"] = \"text/html\" " + 
                                           "ngx.say('\"'\"'<H4>Success!</H4>'\"'\"') " + 
                                           "ngx.say('\"'\"'<H7>[ServerID:" + id +"]</H7>'\"'\"'); " + 
                                           "}' /etc/nginx/sites-available/default");


            var c2 = client.CreateCommand($"echo { pass } | sudo -S sed -i 's/worker_connections 768;/worker_connections 10000;/g' /etc/nginx/nginx.conf");
            var c3 = client.CreateCommand($"echo { pass } | sudo -S sed -i 's/keepalive_timeout 65;/keepalive_timeout 300;/g' /etc/nginx/nginx.conf");
            var c4 = client.CreateCommand($"echo { pass } | sudo -S sed -i \"/keepalive_timeout 300;/a\\ \tkeepalive_requests 1000000;\" /etc/nginx/nginx.conf");
            var c5 = client.CreateCommand($"echo { pass } | sudo -S service nginx reload; chmod 700 .ssh; chmod 640 .ssh/authorized_keys");
            var c6 = client.CreateCommand($"echo { pass } | sudo -S touch /bin/loadScript ; sudo -S chmod 777 /bin/loadScript && sudo -S echo \"{ loadScript }\" > /bin/loadScript");


            bool q = false;
            bool w = false;
            Parallel.Invoke( delegate () {
                                    
                                    while(true)
                                    {
                                        for (int dots = 0; dots < 3; ++dots)
                                        {
                                            Console.Write('.');
                                            System.Threading.Thread.Sleep(1000);
                                            if (dots == 2)
                                            {
                                                if (q == true)
                                                {
                                                    w = true;
                                                    break;
                                                }

                                                Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
                                                dots = -1;
                                                System.Threading.Thread.Sleep(1000);
                                            }
                                        }

                                        if (w == true) 
                                            break;
                                    }
                             },

                             delegate () { c.Execute();  q = true; }
            );
            

            //Console.WriteLine(c.Result);

            if (c.Result.Length > 100)
                Console.WriteLine("\n\n> ...Done\n");
            else
            {
                b = true;

                Console.WriteLine("\n\n> Error on downloading packages and libraries. Trying again...\n");

                c.Execute();

                if (c.Result.Length > 100)
                {
                    Console.WriteLine("> ...Done\n");
                    b = false;
                }
                else
                {
                    Console.WriteLine("> Error on downloading packages and libraries...Aborting Task...\n");

                    REMOVE_VM(ci, id, ip);
                    return;
                }
            }

            if (b == false)
                Parallel.Invoke( () => c1.Execute(),
                                 () => c2.Execute(),
                                 () => c3.Execute()
                               );   

            c4.Execute();
            c5.Execute();
            c6.Execute();
            
            
            client.Disconnect();
            Console.WriteLine("\nDisconnected from client " + connection.Host + "\n");
          


            CONFIGURE_CLUSTER_ADD(ci, ip);
        }

        private void CONFIGURE_CLUSTER_ADD(CloudInfoClass ci, string ip)
        {
            string result = "";
        
            string cmd = "sudo sed -i '/upstream web_cluster {/a\\ \tserver " + ip + ":80;' /etc/nginx/sites-available/default";

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \" " + cmd + " \"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.HasExited)
                Console.WriteLine($"Server [{ip}] successfully added to the cluster.");



            // Update corresponding properties
            if (!ci._serverIPs.Contains(ip))
                ci._serverIPs.Add(ip);

            ci.GetCloudInfo("kamaki server list", "");
            ci.GetCloudInfo("sudo service nginx reload", "add");
            
        }

        private void CONFIGURE_CLUSTER_REMOVE(CloudInfoClass ci, string ip)
        {
            string result = "";
        
            string cmd = $"sudo sed -i '/server {ip}:80;/d' /etc/nginx/sites-available/default";

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \" " + cmd + " \"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.HasExited)
                Console.WriteLine($"\n\tServer [{ip}] successfully removed from the cluster.");


            // Update corresponding service
            ci.GetCloudInfo("sudo service nginx reload", "");
        }

        private void REMOVE_VM(CloudInfoClass ci, string id, string ip)
        {
            bool q = false;
            bool q1 = false;

            // Update _assignedVMsToProject and _assignedVMsToProject2 lists
            foreach (var s in ci._assignedVMsToProject)
            {
                if (s.Equals(id))
                {
                    q = true;
                    break;
                } 
            }
            
            if (q.Equals(true))
                ci._assignedVMsToProject.Remove(id);
            else
            {
                foreach (var s in ci._assignedVMsToProject2)
                {
                    if (s.Equals(id))
                    {
                        q1 = true;
                        break;
                    }
                }
            }

            if (q1.Equals(true))
                ci._assignedVMsToProject2.Remove(id);


            // Update _serverIDs list
            foreach (var s in ci._serverIDs)
            {
                if (s.Equals(id))
                {
                    ci._serverIDs.Remove(id);
                    break;
                } 
            }

            // Update _serverIPs list
            foreach (var s in ci._serverIPs)
            {
                if (s.Equals(ip))
                {
                    ci._serverIPs.Remove(ip);
                    break;
                } 
            }

            // Update _clusterSize property
            ci._clusterSize = ci._serverIDs.Count;       


            // Delete server
            ci.GetCloudInfo($"kamaki server delete { id }", id);           
        } 

        #endregion


    }

}