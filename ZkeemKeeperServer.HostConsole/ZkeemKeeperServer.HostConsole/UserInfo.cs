namespace ZkeemKeeperServer.HostConsole
{
    public class UserInfo
    {
        public string DeviceSN;
        public string PIN;
        public string Username;
        public string Password;
        public string Priority = "0";
        public int CardNumber;
        public string Group = "1";
        public string TimeZone = "7";

        public UserInfo(string deviceSN)
        {
            DeviceSN = deviceSN;
        }
    }

}
