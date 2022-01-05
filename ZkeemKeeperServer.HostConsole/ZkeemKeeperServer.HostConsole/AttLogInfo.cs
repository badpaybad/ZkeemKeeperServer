using System;

namespace ZkeemKeeperServer.HostConsole
{
    public class AttLogInfo
    {
        static char _splitTab = '\t';

        public string DeviceSN;
        public string PIN;
        public DateTime Datetime;

        /// <summary>
        /// 0——Clock in 1—— Clock out 2—— Out 3—— Return from an out 4——Clock in for overtime 5—— Clock out for overtime 8——Meal start 9—— Meal end 
        /// </summary>
        public string AttendanceStatus;

        /// <summary>
        /// 0 ——Password 1 —— Fingerprint 2 —— Card 9 ——Others 
        /// </summary>
        public string VerifyType;

        public string VerifyText
        {
            get
            {
                switch (VerifyType)
                {
                    case "0": return "Password";
                    case "1": return "Fingerprint";
                    case "2": return "Card";
                    case "9": return "Others";
                    default: return "Other";
                }
            }
        }

        public string WorkCode;

        public string Reserved1;

        public string Reserved2;

        public AttLogInfo(string deviceSN)
        {
            DeviceSN = deviceSN;
        }

        public AttLogInfo Parse(string lineData)
        {
            var arr = lineData.Split(_splitTab);

            if (arr.Length > 0) PIN = arr[0];
            if (arr.Length > 1) Datetime = DateTime.Parse(arr[1]);
            if (arr.Length > 2) AttendanceStatus = arr[2];
            if (arr.Length > 3) VerifyType = arr[3];
            if (arr.Length > 4) WorkCode = arr[4];
            if (arr.Length > 5) Reserved1 = arr[5];
            if (arr.Length > 6) Reserved2 = arr[6];

            return this;
        }

        public int UserPin { get { return int.Parse(PIN); } }

        public string AttendanceImage { get; set; }
    }

}
