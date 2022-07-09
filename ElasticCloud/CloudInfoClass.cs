using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Resource_Manager
{
    public class CloudInfoClass
    {

        #region Properties

        public string _projectID { get; private set; }
        public string _project2ID { get; private set; }
        public int _clusterSize { get; set; }
        public string _networkID { get; private set; }
        public IList<string> _assignedVMsToProject = new List<string>();
        public IList<string> _assignedVMsToProject2 = new List<string>();
        public IList<string> _serverIDs = new List<string>();
        public IList<string> _serverIPs = new List<string>();
        public IDictionary<string, string> _projectInfo = new Dictionary<string, string>();
        public IDictionary<string, string> _project2Info = new Dictionary<string, string>();
        public IDictionary<string, string> _imageIDs = new Dictionary<string, string>();
        public IDictionary<string, string> _flavorIDs = new Dictionary<string, string>();
        public IDictionary<string, string> _serverPass = new Dictionary<string, string>();
        public string _LoadBalancerIP = "192.168.1.1";
        public string _LoadBalancerID = "14282";
        public string _MainServerIP = "192.168.1.2";
        public string _MainServerID = "14361";

        #endregion

        public void GetCloudInfo(string command, string id)
        {

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \" " + command + " \"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
                
            
            if (command is "kamaki server list")
            {
                string[] result = { };
                string[] result1 = { };

                process.Start();
                result = process.StandardOutput.ReadToEnd().Split("\n", StringSplitOptions.RemoveEmptyEntries);
                process.WaitForExit();

                foreach (var s in result)
                {
                    result1 = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    
                    if (!_serverIDs.Contains(result1[0]))
                    {
                        _serverIDs.Add(result1[0]);
                    }
                }

                _clusterSize = _serverIDs.Count;
            }
        
            if (command.Contains("kamaki server info") && command.Contains("grep 'ipv4: 192.168.1.'"))
            {
                string[] result = { };

                process.Start();
                result = process.StandardOutput.ReadToEnd().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string result1 = result[1].TrimEnd();
                process.WaitForExit();

                if (result.Length > 1)
                {
                    if (!_serverIPs.Contains(result1))
                        _serverIPs.Add(result1);
                }
            }

            if (command.Contains("kamaki server info") && command.Contains("grep 'tenant_id:'"))
            {
                string[] result = { };

                process.Start();
                result = process.StandardOutput.ReadToEnd().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string result1 = result[1].TrimEnd();
                process.WaitForExit();


                if (result.Length > 1)
                {
                    if (result1.Contains(_projectID))
                        _assignedVMsToProject.Add(id);
                    else if (result1.Contains(_project2ID))
                        _assignedVMsToProject2.Add(id);
                }
            }

            if (command is "kamaki project list | grep 'bigdata.dsml.ece.ntua.gr'")
            {
                string[] result = { };

                process.Start();
                result = process.StandardOutput.ReadToEnd().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                process.WaitForExit();

                _projectID = result[0];
            }

            if (command is "kamaki project list | grep 'cloudcomputing.cs.uth.gr'")
            {
                string[] result = { };

                process.Start();
                result = process.StandardOutput.ReadToEnd().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                process.WaitForExit();

                _project2ID = result[0];
            }

            if (command.Equals($"kamaki project info { _projectID } | grep -A 25 'resources'"))
            {
                string[] result = { };
                string[] result1 = { };

                process.Start();
                result = process.StandardOutput.ReadToEnd().Split("\n", StringSplitOptions.RemoveEmptyEntries);
                process.WaitForExit();

                for (int i = 1; i < result.Length - 1; i+=3)
                {
                    if (!_projectInfo.ContainsKey(result[i].TrimStart()))
                    {
                         result1 = result[i+1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        _projectInfo.TryAdd(result[i].TrimStart(), result1[1]);
                    }
                }
            }

            if (command.Equals($"kamaki project info { _project2ID } | grep -A 25 'resources'"))
            {
                string[] result = { };
                string[] result1 = { };

                process.Start();
                result = process.StandardOutput.ReadToEnd().Split("\n", StringSplitOptions.RemoveEmptyEntries);
                process.WaitForExit();

                for (int i = 1; i < result.Length - 1; i+=3)
                {
                    if (!_project2Info.ContainsKey(result[i].TrimStart()))
                    {
                        result1 = result[i+1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        _project2Info.TryAdd(result[i].TrimStart(), result1[1]);
                    }
                }
            }

            if (command is "kamaki image list")
            {
                string[] result = { };
                string[] result1 = { };

                process.Start();
                result = process.StandardOutput.ReadToEnd().Split("\n", StringSplitOptions.RemoveEmptyEntries);
                process.WaitForExit();

                for (int i = 0; i < result.Length; i+=5)
                {
                    result1 = result[i].Split(' ', 2, StringSplitOptions.None);

                    if (!_imageIDs.ContainsKey(result1[1]))
                    {
                        _imageIDs.TryAdd(result1[1], result1[0]);
                    }
                }
            }

            if (command is "kamaki flavor list")
            {
                string[] result = { };
                string[] result1 = { };

                process.Start();
                result = process.StandardOutput.ReadToEnd().Split("\n", StringSplitOptions.RemoveEmptyEntries);
                process.WaitForExit();

                for (int i = 0; i < result.Length; i++)
                {
                    result1 = result[i].Split(' ', 2, StringSplitOptions.None);

                    if (!_flavorIDs.ContainsKey(result1[0]))
                    {
                        _flavorIDs.TryAdd(result1[0], result1[1]);
                    }
                }
            }

            if (command is "kamaki network list | grep -A 1 'My_Network (bigdata'")
            {
                string[] result = { };

                process.Start();
                result = process.StandardOutput.ReadToEnd().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                process.WaitForExit();

                _networkID = result[0];
            }

            if (command is "sudo service nginx reload" && id is "add")
            {
                process.Start();
                Console.WriteLine("\nSuccessfully reloaded nginx.");
                process.WaitForExit();
            }
            else if (command is "sudo service nginx reload" && id is "")
            {
                process.Start();
                Console.WriteLine("\n\tSuccessfully reloaded nginx.");
                process.WaitForExit();
            }

            if (command.Contains($"kamaki server wait"))
            {
                Console.WriteLine($"Waiting for Server [{id}] while status: ACTIVE\n");

                process.Start();
                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
            }

            if (command.Equals($"kamaki server delete { id }"))
            {
                process.Start();
                Console.WriteLine($"\n\tDestroying Server [{id}]...[Done]");
                process.WaitForExit();

                Console.WriteLine($"\tServer [{id}] successfully destroyed.");
            
            }

        }


    }
}