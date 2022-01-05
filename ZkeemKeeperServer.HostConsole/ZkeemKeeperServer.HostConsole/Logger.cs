using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ZkeemKeeperServer.HostConsole
{
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
            Console.WriteLine(msg);
            return;
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

}
