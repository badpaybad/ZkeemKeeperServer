using System;

namespace ZkeemKeeperServer.HostConsole
{
    public static class ExceptionExtenstions
    {
        public static string ListAllMessage(this Exception ex)
        {
            string s = string.Empty;

            while (ex != null)
            {
                s += $"{ex.Message}\r\n";
                ex = ex.InnerException;
            }

            return s;
        }
    }

}
