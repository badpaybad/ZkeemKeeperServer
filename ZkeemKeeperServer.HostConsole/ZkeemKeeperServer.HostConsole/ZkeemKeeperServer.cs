using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ZkeemKeeperServer.HostConsole
{
    public class ZkeemKeeperServer : IDisposable
    {
        public const string CMD_ID1_REBOOT = "C:ID1:REBOOT\n";
        public const string CMD_ID106_SENDBELL = "C:ID106:SENDBELL\n";
        public const string CMD_ID10_SHELL_NEED_ARGS = "C:ID10:SHELL ";
        public const string CMD_ID20_CHECK_LOCK = "C:ID20:CHECK\n";
        public const string CMD_ID30_INFO_LS = "C:ID30:INFO ls\n";
        public const string CMD_ID40_SET_OPTION_NEED_ARGS = "C:ID40:SET OPTION ";
        public const string CMD_ID50_CLEAR_NEED_ARGS = "C:ID50:";
        public const string CMD_ID60_UPDATE_USER_INFO_NEED_ARGS = "C:ID60:";
        public const string CMD_ID70_SMS_USER_NEED_ARGS = "C:ID70:";
        public const string CMD_ID75_SMS_PERSONAL_NEED_ARGS = "C:ID75:";
        public const string CMD_ID80_CHECK_UNLOCK = "C:ID80:CHECK\n";
        public const string CMD_ID65_DELETE_USER_NEED_ARGS = "C:ID65:";
        public const string CMD_ID100_UPLOAD_INFO_NEED_ARGS = "C:ID100:";
        public const string CMD_ID101_DELETE_ATTLOG_NEED_ARGS = "C:ID101:";
        public const string CMD_ID102_DELETE_PHOTO_NEED_ARGS = "C:ID102:";
        public const string CMD_ID103_USER_MEAL_NEED_ARGS = "C:ID103:";
        public const string CMD_ID104_USER_LEGALITY_NEED_ARGS = "C:ID104:";
        public const string CMD_ID108_USER_ENROLL_MF_NEED_ARGS = "C:ID108:";

        public const string CMD_ID110_USER_LIST_ALL = "C:ID110:DATA QUERY USERINFO PIN=\n";
        private const char _splitNewLine = '\n';
        bool _isDispose;
        public void Dispose()
        {
            _isStop = true;

            _isDispose = true;
        }

        TcpListener _tcp;
        List<Thread> _tcpListenerThreads = new List<Thread>();
        string _hostIp;
        int _tcpPort;
        bool _isInitDone;
        bool _isStop;

        ConcurrentDictionary<string, Func<RequestData, ResponseData>> _routingMapHandle = new ConcurrentDictionary<string, Func<RequestData, ResponseData>>();

        ConcurrentDictionary<string, ConcurrentQueue<string>> _deviceCommands = new ConcurrentDictionary<string, ConcurrentQueue<string>>();

        ConcurrentDictionary<string, string> _deviceSn = new ConcurrentDictionary<string, string>();

        public event Action<RequestData, List<AttLogInfo>> OnClientAttendance;
        public event Action<RequestData> OnClientRequest;

        public ZkeemKeeperServer(string hostIp, int port, int numberOfThread = 3)
        {
            _hostIp = hostIp;
            _tcpPort = port;

            _routingMapHandle["/iclock/cdata"] = Iclock_cdata;
            _routingMapHandle["/iclock/getrequest"] = Iclock_getrequest;
            _routingMapHandle["/iclock/devicecmd"] = Iclock_devicecmd;
            _routingMapHandle["/iclock/fdata"] = Iclock_fdata;

            // InitTcpListener();
            for (var i = 0; i < numberOfThread; i++)
            {
                _tcpListenerThreads.Add(new Thread(() =>
                {
                    while (!_isDispose)
                    {
                        ListenerData();
                    }
                }));
            }

            foreach (var t in _tcpListenerThreads)
            {
                t.Start();
            }
        }

        #region monitoring
        public static List<string> GetHostIPs()
        {
            var hostName = Dns.GetHostName();
            IPHostEntry ipHost = Dns.GetHostEntry(hostName);
            var ipAddrs = ipHost.AddressList;
            return ipAddrs.Select(i => i.ToString()).ToList();
        }

        public List<string> List_DeviceKey_Online()
        {
            lock (_deviceSn)
            {
                return _deviceSn.Keys.ToList();
            }
        }

        public List<string> List_DeviceSN_Online()
        {
            lock (_deviceSn)
            {
                return _deviceSn.Values.ToList();
            }
        }
        #endregion

        #region command send to device
        public void CmdReboot(string toDeviceSN)
        {
            SendCommand(toDeviceSN, CMD_ID1_REBOOT);
        }

        public void CmdGetListUser(string toDeviceSN)
        {
            SendCommand(toDeviceSN, CMD_ID110_USER_LIST_ALL);
        }

        public void CmdUpdateUserInfo(string toDeviceSN, params UserInfo[] users)
        {
            //var cmds = string.Empty;
            //foreach (var u in users)
            //{
            //    cmds = cmds + $"{CMD_ID60_UPDATE_USER_INFO_NEED_ARGS}DATA UPDATE USERINFO PIN={u.PIN}" +
            //        $"\tName={u.Username}\tPri={u.Priority}\tPasswd={u.Password}\tCard=[{u.CardNumber.ToString("X").PadLeft(10, '0')}]\tGrp={u.Group}\tTZ={u.TimeZone}\n";
            //}

            //SendCommand(toDeviceSN, cmds);
        }

        public void SendCommand(string toDeviceSN, string cmd, string args = "")
        {
            var cmdAndArgs = $"{cmd}";
            if (string.IsNullOrEmpty(args) == false)
            {
                cmdAndArgs = cmdAndArgs.Trim('\n') + " " + args + "\n";
            }
            if (cmdAndArgs.IndexOf("\n") < 0) { cmdAndArgs = cmdAndArgs + "\n"; }

            if (_deviceCommands.TryGetValue(toDeviceSN, out ConcurrentQueue<string> cmds) && cmds != null)
            {
                lock (cmds)
                {
                    cmds.Enqueue(cmdAndArgs);
                }
            }
        }
        #endregion

        #region url routing handle 
        private ResponseData Iclock_fdata(RequestData arg)
        {
            //var bReceive = arg.DataInByte;
            //int imgrecindex = 0;
            //for (int i = 0; i < bReceive.Length; i++)
            //{
            //    imgrecindex = i;
            //    if ((int)bReceive[i] == 0)
            //        break;
            //}
            //imgrecindex += 1;
            //// Save Photo
            //byte[] imgReceive = new byte[bReceive.Length - imgrecindex];
            //Array.Copy(bReceive, imgrecindex, imgReceive, 0, bReceive.Length - imgrecindex);
            //string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
            //if (Directory.Exists(path) == false)
            //{
            //    Directory.CreateDirectory(path);
            //}
            //path += "\\" + strImageNumber.Replace("PIN=", "");
            //System.IO.File.WriteAllBytes(path, imgReceive);

            var cmdstring = "OK";

            return new ResponseData { DataInByte = Encoding.UTF8.GetBytes(cmdstring) };
        }

        private ResponseData Iclock_devicecmd(RequestData arg)
        {
            var cmdstring = "OK";

            return new ResponseData { DataInByte = Encoding.UTF8.GetBytes(cmdstring) };
        }

        /// <summary>
        /// after get request, device will call result to Iclock_devicecmd
        /// </summary>
        /// <param name="remoteSocket"></param>
        /// <param name="arg"></param>
        /// <returns></returns>
        private ResponseData Iclock_getrequest(RequestData arg)
        {
            if (arg.HttpMethod.Equals("get", StringComparison.OrdinalIgnoreCase))
            {
                return Iclock_getrequest_get(arg);
            }
            else
            {
                return Iclock_getrequest_post(arg);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteSocket"></param>
        /// <param name="arg"></param>
        /// <returns></returns>
        private ResponseData Iclock_getrequest_post(RequestData arg)
        {
            var sBuffer = arg.RawData;
            // Only PUSH SDK Ver 2.0.1
            //if (sBuffer.IndexOf("Stamp", 1) > 0 && sBuffer.IndexOf("ATTPHOTO", 1) > 0 && sBuffer.IndexOf("OPLOG", 1) < 0) // Upload AttLog

            // FIX
            // Compatible with PUSH SDK Ver 1.0 and Ver 2.0.1
            // PUSH SDK Ver 1.0: "PhotoStamp"
            // PUSH SDK Ver 2.0.1: "ATTPHOTO&Stamp"
            if (sBuffer.IndexOf("Stamp", 1) > 0 && (sBuffer.IndexOf("Photo", 1) > 0 || sBuffer.IndexOf("ATTPHOTO", 1) > 0) && sBuffer.IndexOf("OPLOG", 1) < 0) // Upload AttLog
            {
                // attpholog(sBuffer);
            }

            var cmdstring = "OK";

            if (sBuffer.IndexOf("INFO") > 0)
            {
                cmdstring = "OK";
            }
            else if (sBuffer.IndexOf("options") > 0)
            {
                cmdstring = "OK";
            }

            return new ResponseData { DataInByte = Encoding.UTF8.GetBytes(cmdstring) };
        }

        private ResponseData Iclock_getrequest_get(RequestData arg)
        {
            var sBuffer = arg.RawData;
            var cmdstring = "OK";

            if (sBuffer.IndexOf("INFO") > 0)
            {
                cmdstring = "OK";
            }
            else if (sBuffer.IndexOf("options") > 0)
            {
                cmdstring = "OK";
            }
            else
            {
                if (_deviceCommands.TryGetValue(arg.DeviceSN.ToLower(), out ConcurrentQueue<string> cmds) && cmds != null)
                {
                    if (cmds.TryDequeue(out string val) && !string.IsNullOrEmpty(val))
                    {
                        cmdstring = val;
                    }
                }
            }
            return new ResponseData { DataInByte = Encoding.UTF8.GetBytes(cmdstring), Body = cmdstring };
        }

        private ResponseData Iclock_cdata(RequestData arg)
        {
            if (arg.HttpMethod.Equals("get", StringComparison.OrdinalIgnoreCase))
            {
                return Iclock_cdata_get(arg);
            }
            else
            {
                return Iclock_cdata_post(arg);
            }
        }

        private ResponseData Iclock_cdata_get(RequestData arg)
        {
            string SN = "", Stamp = "", OpStamp = "", PhotoStamp = ""
                , ErrorDelay = "15", Delay = "15", TransTimes = ""
                , TransInterval = "", TransFlag = "1111101100"
                , Realtime = "", Encrypt = "", TimeZoneclock = "";

            SN = arg.DeviceSN;

            //Stamp = "1"; // If we loose connection during the process, we get again all records from the beginning (we can change that to get only the last missed records)

            //Realtime = "1";
            //TransTimes = "00:00;14:05";
            //Encrypt = "0";
            //TimeZoneclock = "1";
            //TransInterval = "1";


            //Stamp = "1"; // If we loose connection during the process, we get again all records from the beginning (we can change that to get only the last missed records)

            //Realtime = "1";
            //TransTimes = "00:00;14:05";
            //Encrypt = "0";
            //TimeZoneclock = "1";
            //TransInterval = "1";


            Stamp = "1"; // If we loose connection during the process, we get again all records from the beginning (we can change that to get only the last missed records)

            Realtime = "1";
            TransTimes = "00:00;14:05";
            Encrypt = "0";
            TimeZoneclock = "7";
            TransInterval = "1";

            // European Time Zone (Set the Time on the Device when we restart the Device)
            // Time Zone --> In China devices, Time Zone = 8, in European devices, Time Zone = 1
            string Time_Zone = "7";

            string optionConfig = $"GET OPTION FROM:{SN}\r\nStamp={Stamp}\r\nOpStamp={OpStamp}\r\nPhotoStamp={PhotoStamp}" +
                $"\r\nErrorDelay={ErrorDelay}\r\nDelay={Delay}\r\nTransTimes={TransTimes}" +
                $"\r\nTransInterval={TransInterval}\r\nTransFlag={TransFlag}\r\nRealtime={Realtime}" +
                $"\r\nEncrypt={Encrypt}\r\nTimeZoneclock={TimeZoneclock}\r\nTimeZone={Time_Zone}\r\n";

            byte[] bOption = Encoding.UTF8.GetBytes(optionConfig);

            return new ResponseData
            {
                DataInByte = bOption
            };
        }

        private ResponseData Iclock_cdata_post(RequestData arg)
        {
            if (arg.RawData.IndexOf("ATTLOG", StringComparison.OrdinalIgnoreCase) > 0)
            {
                List<AttLogInfo> attLogs = new List<AttLogInfo>();
                try
                {
                    var lines = arg.HttpBody.Trim(_splitNewLine).Split(_splitNewLine);

                    foreach (var l in lines)
                    {
                        if (string.IsNullOrEmpty(l)) continue;

                        attLogs.Add(new AttLogInfo(arg.DeviceSN).Parse(l));
                    }
                }
                catch
                {
                    Logger.Error(arg.RawData);
                }

                OnClientAttendance(arg, attLogs);
            }
            else
            {
                Logger.Info(arg.RawData);
            }

            //// Only PUSH SDK Ver 2.0.1 (In Version 1.0 String for AttLog have diferent format, example: CHECK LOG: stamp=392232960 1       2012-03-14 17:39:00     0       0       0       1)
            //if (sBuffer.IndexOf("Stamp", 1) > 0 && sBuffer.IndexOf("OPERLOG", 1) < 0 && sBuffer.IndexOf("ATTLOG", 1) > 0 && sBuffer.IndexOf("OPLOG", 1) < 0) // Upload AttLog
            //{
            //    attlog(sBuffer);
            //}

            //// Only PUSH SDK Ver 2.0.1
            //if (sBuffer.IndexOf("Stamp", 1) > 0 && sBuffer.IndexOf("OPERLOG", 1) > 0 && sBuffer.IndexOf("OPLOG", 1) > 0) // Upload OpLog

            //// BETA: FIX (Not completly Fix)
            //// Compatible with PUSH SDK Ver 1.0 and Ver 2.0.1
            //// PUSH SDK Ver 1.0: "OpStamp"
            //// PUSH SDK Ver 2.0.1: "OPERLOG"
            ////if (sBuffer.IndexOf("Stamp", 1) > 0 && (sBuffer.IndexOf("OPERLOG", 1) > 0 || sBuffer.IndexOf("OpStamp", 1) > 0) 
            ////    && (sBuffer.IndexOf("OPLOG", 1) > 0 || sBuffer.IndexOf("OpStamp", 1) > 0))
            //{
            //    oplog(sBuffer);
            //}

            //// Only PUSH SDK Ver 2.0.1
            //if (sBuffer.IndexOf("Stamp", 1) > 0 && sBuffer.IndexOf("OPERLOG", 1) > 0 && sBuffer.IndexOf("OPLOG 6", 1) > 0) // Upload Enroll FP
            //{
            //    enfplog(sBuffer);
            //}

            //// Only PUSH SDK Ver 2.0.1
            //if (sBuffer.IndexOf("Stamp", 1) > 0 && sBuffer.IndexOf("USERINFO", 1) > 0) // Upload user Info
            //{
            //    usinlog(sBuffer);
            //}

            return new ResponseData
            {
                DataInByte = Encoding.UTF8.GetBytes("OK")
            };
        }
        #endregion

        private void InitTcpListener()
        {
            if (string.IsNullOrEmpty(_hostIp))
            {
                _tcp = new TcpListener(IPAddress.Any, _tcpPort);
            }
            else
            {
                _tcp = new TcpListener(IPAddress.Parse(_hostIp), _tcpPort);
            }
            _isInitDone = true;
            Logger.Info("Init Tcp Done");
            ShowInfo();
        }

        void ShowInfo()
        {
            Logger.Info($"{_hostIp}:{_tcpPort}");
            Logger.Info($"{_tcp.LocalEndpoint.AddressFamily}:{_tcpPort}");
        }

        public void Start()
        {
            if (_tcp == null || _tcp.Server == null)
            {
                InitTcpListener();
            }
            else
            {
                _isStop = true;
                Thread.Sleep(1000);
                try { _tcp.Stop(); } catch { }
            }

            _isStop = false;
            _tcp.Start();

            ShowInfo();
        }

        public void Stop()
        {
            _isStop = true;
        }

        //public void Disconnect()
        //{
        //    _isStop = true;

        //    if (_tcp != null)
        //    {
        //        _tcp.Stop();
        //    }

        //    ShowInfo();
        //}

        private void ListenerData()
        {
            try
            {
                while (!_isStop)
                {
                    if (!_isInitDone)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    // ThreadPool.QueueUserWorkItem(new WaitCallback((o) =>
                    // {
                    try
                    {
                        Socket socketAccepted = _tcp.AcceptSocket();

                        Thread.Sleep(300);

                        ShowInfo();

                        try
                        {
                            byte[] bReceive = new byte[1024 * 1024 * 2];

                            int i = socketAccepted.Receive(bReceive);

                            byte[] requestData = new byte[i];

                            for (int j = 0; j < i; j++)
                            {
                                requestData[j] = bReceive[j];
                            }

                            var request = new RequestData(requestData);

                            if (OnClientRequest != null)
                            {
                                OnClientRequest(request);
                            }

                            Logger.Info(request.RawData);

                            _deviceSn.GetOrAdd(request.DeviceSN.ToLower(), request.DeviceSN);
                            _deviceCommands.GetOrAdd(request.DeviceSN.ToLower(), new ConcurrentQueue<string>());

                            if (_routingMapHandle.TryGetValue(request.RequestPath, out Func<RequestData, ResponseData> handle)
                                && handle != null)
                            {
                                var response = handle(request);

                                if (response == null)
                                {
                                    response = new ResponseData
                                    {
                                        DataInByte = Encoding.UTF8.GetBytes("OK"),
                                        Body = "OK"
                                    };
                                }

                                SendResponse(ref socketAccepted, response, request);
                            }
                            else
                            {
                                SendResponse(ref socketAccepted, new ResponseData
                                {
                                    DataInByte = Encoding.UTF8.GetBytes("OK"),
                                    Body = "OK"
                                }, request);
                            }
                        }
                        catch (Exception skEx)
                        {
                            Logger.Error(skEx.Message, skEx);
                        }
                        finally
                        {
                            socketAccepted.Shutdown(SocketShutdown.Both);
                            socketAccepted.Close();
                        }

                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex.Message, ex);
                    }
                    // }));

                    Thread.Sleep(1);
                }

                this._tcp.Stop();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message, ex);
            }
        }

        public void SendResponse(ref Socket remoteSocket, ResponseData responseData, RequestData requestData)
        {
            Logger.Info(requestData.HttpMethod+ ":Url: " + requestData.RequestUrl + " "+ requestData.DeviceSN);

            SendResponseHeader(ref remoteSocket, responseData, requestData);
            SendToClientSocket(ref remoteSocket, responseData.DataInByte);

            Logger.Info("Body: "+  responseData.Body);
        }

        void SendResponseHeader(ref Socket remoteSocket, ResponseData responseData, RequestData requestData, string contentType = "text/plain", string statusCode = "200 OK")
        {
            string sBuffer = null;
            var contentLength = responseData.DataInByte.Length;

            if (string.IsNullOrEmpty(responseData.ContentType))
            {
                contentType = "text/plain"; // text/html
            }
            if (string.IsNullOrEmpty(responseData.StatusCode))
            {
                statusCode = "200 OK"; // text/html
            }

            else { contentType = responseData.ContentType; }

            sBuffer = sBuffer + requestData.HttpVersion + " " + statusCode + "\r\n";
            sBuffer = sBuffer + "Server: cx1193719-b\r\n";
            sBuffer = sBuffer + "Content-Type: " + contentType + "\r\n";
            sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";

            // Date, Format: Thu, 19 Feb 2008 15:52:10 GMT
            // Get UTC Time
            // string WeekDay = DateTime.UtcNow.DayOfWeek.ToString().Remove(3); // English
            DateTime dateNow = DateTime.Now;
            string WeekDay = dateNow.DayOfWeek.ToString().Remove(3);
            // string Month = DateTime.UtcNow.Month.ToString();
            string Month = dateNow.Month.ToString();
            if (Month == "1") Month = "Jan"; // English
            if (Month == "2") Month = "Feb";
            if (Month == "3") Month = "Mar";
            if (Month == "4") Month = "Apr";
            if (Month == "5") Month = "May";
            if (Month == "6") Month = "Jun";
            if (Month == "7") Month = "Jul";
            if (Month == "8") Month = "Aug";
            if (Month == "9") Month = "Sep";
            if (Month == "10") Month = "Oct";
            if (Month == "11") Month = "Nov";
            if (Month == "12") Month = "Dec";

            //  string Hour = DateTime.UtcNow.Hour.ToString();
            //  string Minute = DateTime.UtcNow.Minute.ToString();
            //  string Second = DateTime.UtcNow.Second.ToString();
            string Hour = dateNow.Hour.ToString();
            string Minute = dateNow.Minute.ToString();
            string Second = dateNow.Second.ToString();
            if (Hour.Length == 1) Hour = "0" + Hour;
            if (Minute.Length == 1) Minute = "0" + Minute;
            if (Second.Length == 1) Second = "0" + Second;

            //  sBuffer = sBuffer + "Date: " + WeekDay + ", " + DateTime.UtcNow.Day + " " + Month + " " + DateTime.UtcNow.Year + " "
            //                    + Hour + ":" + Minute + ":" + Second + " GMT\r\n";
            sBuffer = sBuffer + "Date: " + WeekDay + ", " + dateNow.Day + " " + Month + " " + dateNow.Year + " "
                             + Hour + ":" + Minute + ":" + Second + " GMT\r\n";
            sBuffer = sBuffer + "Content-Length: " + contentLength + "\r\n\r\n";

            Byte[] bSendData = Encoding.UTF8.GetBytes(sBuffer);

            Logger.Info("SendResponseHeader:" + sBuffer);

            SendToClientSocket(ref remoteSocket, bSendData);
        }
        void SendToClientSocket(ref Socket remoteSocket, Byte[] dataToSend)
        {
            int numBytes = 0;

            try
            {
                if (remoteSocket.Connected)
                {
                    if ((numBytes = remoteSocket.Send(dataToSend, dataToSend.Length, 0)) == -1)
                    {
                        Logger.Error("Socket Error: Cannot Send Packet");
                    }
                    else
                    {
                    }
                }
                else
                {
                    Logger.Error("Link Failed...");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message, ex);
            }

        }

    }
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
                        RequestParams.Add(arrVal[0].ToLower(), arrVal[1]);
                    }
                    else
                    {
                        RequestParams.Add(arrVal[0].ToLower(), string.Empty);
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
                    HttpHeaders.Add(l.Substring(0, kIndex).ToLower(), l.Substring(kIndex + 1));
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
        public readonly Dictionary<string, string> HttpHeaders = new Dictionary<string, string>();

        public string HttpBody;

        public readonly byte[] DataInByte;

        public readonly string RawData;

        public readonly string RequestUrl;

        public readonly string RequestPath;

        public readonly Dictionary<string, string> RequestParams = new Dictionary<string, string>();

        public string DeviceSN
        {
            get
            {
                if (RequestParams.ContainsKey("sn"))
                    return RequestParams["sn"];

                return string.Empty;
            }
        }
        public string DeviceTable
        {
            get
            {
                if (RequestParams.ContainsKey("table"))
                    return RequestParams["table"];

                return string.Empty;
            }
        }
        public string DeviceStamp
        {
            get
            {
                if (RequestParams.ContainsKey("stamp"))
                    return RequestParams["stamp"];

                return string.Empty;
            }
        }
    }
    public class ResponseData
    {
        public string Body { get; set; }
        public byte[] DataInByte { get; set; }

        public string StatusCode { get; set; } = "200 OK";
        public string ContentType { get; set; } = "text/plain";
    }

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

    public class Logger : IDisposable
    {
        static ConcurrentQueue<LogInfo> _logQueue = new ConcurrentQueue<LogInfo>();
        static string _dirLog = "Logs";
        static Thread _thread;
        static bool _isDispose = false;
        static Logger()
        {
            var rootDir = AppDomain.CurrentDomain.BaseDirectory;
            _dirLog = Path.Combine(rootDir, "Logs");

            if (Directory.Exists(_dirLog) == false)
            {
                Directory.CreateDirectory(_dirLog);
            }

            _thread = new Thread(() =>
            {
                while (!_isDispose)
                {
                    try
                    {
                        var fileLog = Path.Combine(_dirLog, DateTime.Now.ToString("yyyyMMddHH") + ".csv");
                        var fileLogError = Path.Combine(_dirLog, DateTime.Now.ToString("yyyyMMddHH") + "_error.csv");

                        List<LogInfo> temp = new List<LogInfo>();
                        for (var i = 0; i < 100; i++)
                        {
                            if (_logQueue.TryDequeue(out LogInfo log) && log != null)
                            {
                                temp.Add(log);
                            }
                        }

                        string slog = string.Empty;
                        var serror = string.Empty;
                        foreach (var log in temp)
                        {
                            if (log.Type == LogType.Error)
                            {
                                serror += BuildLineLog(log) + "\r\n";
                            }
                            else
                            {
                                slog += BuildLineLog(log) + "\r\n";
                            }
                        }

                        if (!string.IsNullOrEmpty(slog))
                        {
                            try
                            {
                                using (var sw = new StreamWriter(fileLog, true, System.Text.Encoding.UTF8))
                                {
                                    sw.WriteLine(slog);
                                    sw.Flush();
                                }
                            }
                            catch { }
                        }
                        if (!string.IsNullOrEmpty(serror))
                        {
                            try
                            {
                                using (var sw = new StreamWriter(fileLogError, true, System.Text.Encoding.UTF8))
                                {
                                    sw.WriteLine(serror);
                                    sw.Flush();
                                }
                            }
                            catch { }
                        }
                    }
                    catch (Exception)
                    {

                    }
                    finally
                    {
                        Thread.Sleep(1000);
                    }
                }
            });

            _thread.Start();
        }

        public static string BuildLineLog(LogInfo info)
        {
            string infoEx = string.Empty;
            if (info.ErrorException != null)
            {
                infoEx = JsonConvert.SerializeObject(info.ErrorException);
            }

            return $"{info.Type.ToString()}, {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}, {info.DateUtc}, {RemoveCommasKeys(info.Message)}, {RemoveCommasKeys(infoEx)}";
        }

        static string RemoveCommasKeys(string src)
        {
            src = src.Replace(",", ";;; ");
            src = src.Replace("\t", ";t; ");
            src = src.Replace("\n", ";n; ");
            src = src.Replace("\r", ";r; ");

            return src;
        }

        public static void LogObject(object obj, LogType type, bool showInConsole = false)
        {
            string msg = string.Empty;

            try
            {
                msg = JsonConvert.SerializeObject(obj);

                LogInfo item = new LogInfo()
                {
                    DateUtc = DateTime.Now,
                    Message = msg,
                    ErrorException = null,
                    Type = type
                };

                _logQueue.Enqueue(item);

                if (showInConsole)
                {
                    Console.WriteLine(msg);
                }
            }
            catch (Exception ex)
            {
                msg = JsonConvert.SerializeObject(ex);

                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        public static void Error(string msg, Exception ex = null)
        {
            try
            {
                _logQueue.Enqueue(new LogInfo()
                {
                    DateUtc = DateTime.Now,
                    Message = msg,
                    ErrorException = ex,
                    Type = LogType.Error
                });

                Console.WriteLine("Error: " + msg);

                if (ex != null)
                    Console.WriteLine(ex.ListAllMessage());
            }
            catch
            {

            }

        }

        public static void Info(string msg, Exception ex = null)
        {
            try
            {
                _logQueue.Enqueue(new LogInfo()
                {
                    DateUtc = DateTime.Now,
                    Message = msg,
                    ErrorException = ex,
                    Type = LogType.Info
                });
                Console.WriteLine(msg);
            }
            catch
            {

            }

        }


        public void Dispose()
        {
            _isDispose = true;
        }

        public class LogInfo
        {
            public DateTime DateUtc;
            public string Message;
            public Exception ErrorException;
            public LogType Type;
        }

        public enum LogType
        {
            Info,
            Error,
        }
    }

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
    public class RedisServices : IDisposable
    {
        //static RedisServices _currentInstance;

        //public static RedisServices CurrentInstance
        //{
        //    get
        //    {
        //        if (_currentInstance == null) throw new Exception("Have to call Init to create instance");

        //        return _currentInstance;
        //    }
        //}

        //IServer _server;
        SocketManager _socketManager;
        IConnectionMultiplexer _connectionMultiplexer;
        ConfigurationOptions _options = null;

        public bool IsEnable { get; private set; }

        public RedisServices()
        {
        }

        public IConnectionMultiplexer RedisConnectionMultiplexer
        {
            get
            {
                if (_connectionMultiplexer != null && _connectionMultiplexer.IsConnected)
                    return _connectionMultiplexer;

                if (_connectionMultiplexer != null && !_connectionMultiplexer.IsConnected)
                {
                    _connectionMultiplexer.Dispose();
                }

                _connectionMultiplexer = GetConnection();
                if (!_connectionMultiplexer.IsConnected)
                {
                    var exception = new Exception("Can not connect to redis");
                    Console.WriteLine(exception);
                    throw exception;
                }
                return _connectionMultiplexer;
            }
        }

        public IDatabase RedisDatabase
        {
            get
            {
                var redisDatabase = RedisConnectionMultiplexer.GetDatabase();

                return redisDatabase;
            }
        }

        ISubscriber RedisSubscriber
        {
            get
            {
                var redisSubscriber = RedisConnectionMultiplexer.GetSubscriber();

                return redisSubscriber;
            }
        }

        public RedisServices Init(string endPoint, int? port, string pwd, int? db = null)
        {
            IsEnable = !string.IsNullOrEmpty(endPoint);

            var soketName = endPoint ?? "127.0.0.1";
            _socketManager = new SocketManager(soketName);

            Console.WriteLine($"Redis {endPoint}:{port}");

            port = port ?? 6379;

            _options = new ConfigurationOptions()
            {
                EndPoints =
                {
                    {endPoint, port.Value}
                },
                Password = pwd,
                AllowAdmin = false,
                SyncTimeout = 5 * 1000,
                SocketManager = _socketManager,
                AbortOnConnectFail = false,
                ConnectTimeout = 6 * 1000,
                DefaultDatabase = db,
                //HighPrioritySocketThreads = true,
                //ConnectRetry = 3,
            };

            // _currentInstance = this;

            return this;
        }

        //public RedisServices GetCurrentInstance()
        //{
        //    return CurrentInstance;
        //}

        public TimeSpan Ping()
        {
            try
            {
                return RedisDatabase.Ping();
            }
            catch (Exception ex)
            {
                Logger.Error($"Ping error: {ex.ListAllMessage()}", ex);
                return default(TimeSpan);
            }

        }

        public ConnectionMultiplexer GetConnection()
        {
            if (_options == null) throw new Exception($"Must call {nameof(RedisServices.Init)}");

            var multiplexConnection = ConnectionMultiplexer.Connect(_options);

            Logger.Info($"Redis get ConnectionMultiplexer: {multiplexConnection.ClientName}");

            return multiplexConnection;
        }


        public void Subscribe(string channel, Action<string> handleMessage)
        {
            try
            {
                RedisSubscriber.Subscribe(channel).OnMessage((msg) =>
                {
                    //Console.WriteLine(msg.Channel);
                    //Console.WriteLine(msg.SubscriptionChannel);

                    handleMessage(msg.Message);
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"Subscribe {channel} error: {ex.ListAllMessage()}", ex);
            }

        }

        public void UnSubscribe(string channel)
        {
            try
            {
                RedisSubscriber.Unsubscribe(channel);
            }
            catch (Exception ex)
            {
                Logger.Error($"UnSubscribe {channel} error: {ex.ListAllMessage()}", ex);
            }

        }

        public void Publish(string channel, string message)
        {
            try
            {
                RedisSubscriber.Publish(channel, message);
            }
            catch (Exception ex)
            {
                Logger.Error($" Publish {channel} error: {ex.ListAllMessage()}", ex);
            }

        }

        public bool KeyExisted(string key)
        {
            try
            {
                return RedisDatabase.KeyExists(key);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return false;
            }

        }
        public T Get<T>(string key)
        {
            try
            {
                if (!IsEnable)
                {
                    return default(T);
                }

                //if (RedisDatabase.KeyExists(key) == false) return default(T);

                var val = RedisDatabase.StringGet(key);
                if (val.HasValue == false) return default(T);

                return JsonConvert.DeserializeObject<T>(val);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return default(T);
            }

        }

        public void Set<T>(string key, T val, TimeSpan? expireAfter = null)
        {

            try
            {
                if (!IsEnable)
                {
                    return;
                }

                RedisDatabase.StringSet(key, JsonConvert.SerializeObject(val), expireAfter);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return;
            }
        }

        public string Get(string key)
        {
            try
            {
                if (!IsEnable)
                {
                    return null;
                }
                //if (RedisDatabase.KeyExists(key) == false) return string.Empty;
                var val = RedisDatabase.StringGet(key);
                if (val.HasValue == false) return string.Empty;

                return val;
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return string.Empty;
            }

        }

        public void Set(string key, string val, TimeSpan? expireAfter = null)
        {


            try
            {
                if (!IsEnable)
                {
                    return;
                }

                RedisDatabase.StringSet(key, val, expireAfter);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return;
            }
        }
        public void HashSet(string key, string field, string value)
        {

            try
            {
                if (!IsEnable)
                {
                    throw new PlatformNotSupportedException("No Redis enable");
                }

                RedisDatabase.HashSet(key, field, value);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
            }
        }
        public void HashSet(string key, KeyValuePair<string, string> val)
        {
            try
            {
                if (!IsEnable)
                {
                    throw new PlatformNotSupportedException("No Redis enable");
                }

                RedisDatabase.HashSet(key, val.Key, val.Value);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
            }

        }

        public bool HashExisted(string key, string field)
        {
            try
            {
                return RedisDatabase.HashExists(key, field);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return false;
            }

        }

        public string HashGet(string key, string fieldName)
        {
            try
            {
                if (!IsEnable)
                {
                    throw new PlatformNotSupportedException("No Redis enable");
                }
                //if (RedisDatabase.KeyExists(key) == false) return string.Empty;
                return RedisDatabase.HashGet(key, fieldName);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return string.Empty;
            }

        }

        public void HashDelete(string key, string fieldName)
        {
            try
            {
                if (!IsEnable)
                {
                    throw new PlatformNotSupportedException("No Redis enable");
                }
                RedisDatabase.HashDelete(key, fieldName);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return;
            }

        }

        public Dictionary<string, string> HashGetAll(string key)
        {
            try
            {
                if (!IsEnable)
                {
                    throw new PlatformNotSupportedException("No Redis enable");
                }

                //if (RedisDatabase.KeyExists(key) == false) return new Dictionary<string, string>();

                var data = RedisDatabase.HashGetAll(key);

                Dictionary<string, string> temp = new Dictionary<string, string>();
                foreach (var d in data)
                {
                    temp.Add(d.Name, d.Value);
                }
                return temp;
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return new Dictionary<string, string>();
            }

        }

        public bool QueueHasValue(string key)
        {
            try
            {
                if (!IsEnable)
                {
                    throw new PlatformNotSupportedException("No Redis enable");
                }

                //if (RedisDatabase.KeyExists(key) == false) return false;

                return RedisDatabase.ListLength(key) > 0;

                //return RedisDatabase.KeyExists(key);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return false;
            }

        }

        public long QueueLength(string key)
        {
            try
            {
                if (RedisDatabase.KeyExists(key) == false) return 0;

                return RedisDatabase.ListLength(key);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return default(long);
            }

        }

        public bool TryEnqueue(string key, params string[] values)
        {
            try
            {
                foreach (var p in values)
                {
                    var temp = RedisDatabase.ListLeftPush(key, p);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return false;
            }

        }

        public bool TryDequeue(string key, out string val)
        {
            val = string.Empty;
            try
            {

                var temp = RedisDatabase.ListRightPop(key);

                if (temp.HasValue == false) return false;
                val = temp;

                return true;

            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return false;
            }

        }


        public bool TryPop(string key, out string val)
        {
            val = string.Empty;

            try
            {
                if (!RedisDatabase.KeyExists(key)) return false;

                var temp = RedisDatabase.ListLeftPop(key);
                if (temp.HasValue == false) return false;

                val = temp;

                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        public void KeyDelete(string key)
        {
            try
            {
                RedisDatabase.KeyDelete(key);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return;
            }

        }

        public List<T> GetOrSet<T>(string key, Func<List<T>> buildIfNotExist)
        {

            try
            {
                var val = Get<List<T>>(key);
                if (val != null && val.Count != 0)
                {
                    return val;
                }
                val = buildIfNotExist();
                Set(key, val);
                return val;
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return new List<T>();
            }
        }

        public string GetInfo()
        {
            try
            {
                string s = string.Empty;

                foreach (var ep in _options.EndPoints)
                {
                    s += ep.ToString();
                }

                return $"{_socketManager.Name} ep:{s} db:{_options.DefaultDatabase}";
            }
            catch (Exception ex)
            {
                Logger.Error($"{_socketManager.Name} {ex.ListAllMessage()}", ex);
                return string.Empty;
            }

        }

        public bool LockTake(string key, TimeSpan? timeout = null)
        {
            try
            {
                if (timeout == null)
                {
                    timeout = new TimeSpan(0, 0, 30);
                }
                return RedisDatabase.LockTake(key, key, timeout.Value);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return false;
            }

        }

        public bool LockRelease(string key)
        {
            try
            {
                return RedisDatabase.LockRelease(key, key);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return false;
            }

        }

        public bool LockExtend(string key, TimeSpan? timeout = null)
        {

            try
            {
                if (timeout == null)
                {
                    timeout = new TimeSpan(0, 0, 30);
                }
                return RedisDatabase.LockExtend(key, key, timeout.Value);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return false;
            }
        }

        public string[] GetAllDataInList(string key)
        {
            try
            {
                var length = RedisDatabase.ListLength(key);
                var value = RedisDatabase.ListRange(key, 0, length).ToStringArray();
                return value;
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return new string[0];
            }
        }


        /**
         * parameter: key: (example: *key*,key*,*)
         * parameter: db_redis
         * 
         * **/
        public IEnumerable<StackExchange.Redis.RedisKey> GetAllListInFolderRedis(string key, int db_redis)
        {
            try
            {
                return RedisConnectionMultiplexer.GetServer(_options.EndPoints.FirstOrDefault()).Keys(db_redis, key);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return null;
            }

        }


        public long ListAdd(string key, string val)
        {
            try
            {

                return RedisDatabase.ListRightPush(key, val);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
                return -1;
            }
        }

        public List<string> ListRange(string key, long from = 0, long to = -1)
        {
            try
            {
                RedisValue[] result = RedisDatabase.ListRange(key, from, to);
                if (result.Length > 0)
                {
                    return result.Where(i => i.HasValue).Select(i => (string)i).ToList();
                }
                return new List<string>();
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);

                return new List<string>();
            }


        }

        public void KeyExpireAfter(string key, TimeSpan? expireAfter)
        {
            try
            {
                RedisDatabase.KeyExpire(key, expireAfter);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
            }

        }

        public void KeyExpireAt(string key, DateTime? expireAt)
        {
            try
            {
                RedisDatabase.KeyExpire(key, expireAt);
            }
            catch (Exception ex)
            {
                Logger.Error($"{key} {ex.ListAllMessage()}", ex);
            }

        }

        public void Dispose()
        {

        }
    }

}
