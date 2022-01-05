using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZkeemKeeperServer.HostConsole
{
    public class RequestData
    {
        static char _splitCr = '\r';
        static char _splitNewLine = '\n';
        static char _splitQuery = '?';
        static char _splitAnd = '&';
        static char _splitEqual = '=';
        static char _splitSpeace = ' ';
        static char _splitSlash = '/';
        static char _splitColon = ':';
        public RequestData(byte[] requestDataInByte)
        {
            DataInByte = requestDataInByte;

            string data = Encoding.UTF8.GetString(requestDataInByte);
            data = data.Replace(_splitCr.ToString(), string.Empty);
            RawData = data;

            List<string> allLine = RawData.Split(_splitNewLine).ToList();

            var urlRequest = allLine[0].Split(_splitSpeace);
            HttpMethod = urlRequest[0];
            RequestUrl = urlRequest[1];

            string[] urlParam = RequestUrl.Split(_splitQuery);

            RequestPath = urlParam[0].ToLower();
            if (urlParam.Length > 1)
            {
                var arrParam = urlParam[1].Split(_splitAnd);

                foreach (var p in arrParam)
                {
                    var arrVal = p.Split(_splitEqual);
                    if (arrVal.Length > 1)
                    {
                        RequestParams.Add(new KeyValuePair<string, string>(arrVal[0].ToLower(), arrVal[1]));
                    }
                    else
                    {
                        RequestParams.Add(new KeyValuePair<string, string>(arrVal[0].ToLower(), string.Empty));
                    }
                }
            }

            HttpVersion = urlRequest[2];//.Split(_splitSlash)[1];

            var idxLineSplitBody = 0;

            for (var i = 1; i < allLine.Count; i++)
            {
                var l = allLine[i].Trim(new char[] { _splitCr, _splitNewLine, _splitSpeace });
                int kIndex = l.IndexOf(_splitColon);
                if (kIndex > 0)
                {
                    HttpHeaders.Add(new KeyValuePair<string, string>(l.Substring(0, kIndex).ToLower(), l.Substring(kIndex + 1)));
                }
                if (string.IsNullOrEmpty(l))
                {
                    idxLineSplitBody = i;
                    break;
                }
            }

            if (allLine.Count > idxLineSplitBody)
            {
                var body = string.Empty;
                for (var i = idxLineSplitBody; i < allLine.Count; i++)
                {
                    var l = allLine[i].Trim(new char[] { _splitSpeace, _splitCr, _splitNewLine });
                    if (!string.IsNullOrEmpty(l))
                    {
                        body = body + l + "\n";
                    }
                }
                HttpBody = body;
            }
        }

        public readonly string HttpVersion;
        public readonly string MimeType = "text/plain";
        public readonly string HttpMethod;
        public readonly List<KeyValuePair<string, string>> HttpHeaders = new List<KeyValuePair<string, string>>();

        public string HttpBody;

        public readonly byte[] DataInByte;

        public readonly string RawData;

        public readonly string RequestUrl;

        public readonly string RequestPath;

        public readonly List<KeyValuePair<string, string>> RequestParams = new List<KeyValuePair<string, string>>();

        public string DeviceSN
        {
            get
            {
                return RequestParams.Where(x => x.Key == "sn").Select(i => i.Value).FirstOrDefault();
            }
        }
        public string DeviceTable
        {
            get
            {
                return RequestParams.Where(x => x.Key == "table").Select(i => i.Value).FirstOrDefault();

            }
        }
        public string DeviceStamp
        {
            get
            {
                return RequestParams.Where(x => x.Key == "stamp").Select(i => i.Value).FirstOrDefault();
            }
        }
    }

}
