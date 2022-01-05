using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
                        TcpListenerData();
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

            return new ResponseData { DataInByte = Encoding.UTF8.GetBytes(cmdstring), Body = cmdstring };
        }

        private ResponseData Iclock_devicecmd(RequestData arg)
        {
            var cmdstring = "OK";

            return new ResponseData { DataInByte = Encoding.UTF8.GetBytes(cmdstring), Body = cmdstring };
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

            return new ResponseData { DataInByte = Encoding.UTF8.GetBytes(cmdstring), Body = cmdstring };
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
            ResponseData rd;
            if (arg.HttpMethod.Equals("get", StringComparison.OrdinalIgnoreCase))
            {
                rd = Iclock_cdata_get(arg);
            }
            else
            {
                rd = Iclock_cdata_post(arg);
            }

            return rd;
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
                Body = optionConfig,
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
                DataInByte = Encoding.UTF8.GetBytes("OK"),
                Body = "OK"
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

        private void TcpListenerData()
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

                            Logger.Info("RequestRaw: " + request.RawData);

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

            var header = SendResponseHeader(ref remoteSocket, responseData, requestData);
            SendToClientSocket(ref remoteSocket, responseData.DataInByte);

            Logger.Info("ResponseHeader: " + header);
            Logger.Info("ResponseBody: " + responseData.Body);
        }

        string SendResponseHeader(ref Socket remoteSocket, ResponseData responseData, RequestData requestData, string contentType = "text/plain", string statusCode = "200 OK")
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

            SendToClientSocket(ref remoteSocket, bSendData);

            return sBuffer;
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

}
