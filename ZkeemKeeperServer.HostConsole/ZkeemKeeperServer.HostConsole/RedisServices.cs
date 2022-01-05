using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ZkeemKeeperServer.HostConsole
{
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
