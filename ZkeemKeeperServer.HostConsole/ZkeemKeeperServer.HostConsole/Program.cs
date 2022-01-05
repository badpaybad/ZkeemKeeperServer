using Newtonsoft.Json;
using System;
using System.Configuration;

namespace ZkeemKeeperServer.HostConsole
{
    class Program
    {
        static ZkeemKeeperServer server;
        static void Main(string[] args)
        {
            string hostIp = ConfigurationManager.AppSettings["ZkeemKeeper_host"];
            int port = int.Parse(ConfigurationManager.AppSettings["ZkeemKeeper_port"]);
            int numberOfThread = int.Parse(ConfigurationManager.AppSettings["ZkeemKeeper_num_of_thread"]);
       
            server = new ZkeemKeeperServer(hostIp, port, numberOfThread);
            server.OnClientAttendance += Server_OnClientAttendance; 
            server.Start();

            var cmd = Console.ReadLine();
            if (cmd == "quit")
            {
                server.Dispose();
                Environment.Exit(0);
            }
        }

        private static void Server_OnClientAttendance(RequestData arg1, System.Collections.Generic.List<AttLogInfo> arg2)
        {           
            //var redisHost = ConfigurationManager.AppSettings["ATTENDANCE_RedisHost"];
            //var redisPort = int.Parse(ConfigurationManager.AppSettings["ATTENDANCE_RedisPort"]);
            //var redisPwd = ConfigurationManager.AppSettings["ATTENDANCE_RedisPwd"];
            //var redisNotiDb = int.Parse(ConfigurationManager.AppSettings["ATTENDANCE_RedisDb"]);

            //string queueName = ConfigurationManager.AppSettings["ATTENDANCE_RedisQueue"];
            //RedisServices redis = new RedisServices().Init(redisHost, redisPort, redisPwd, redisNotiDb);

            //redis.TryEnqueue(queueName, JsonConvert.SerializeObject(new
            //{
            //    request = arg1,
            //    data = arg2
            //}));
        }      

    }
}
