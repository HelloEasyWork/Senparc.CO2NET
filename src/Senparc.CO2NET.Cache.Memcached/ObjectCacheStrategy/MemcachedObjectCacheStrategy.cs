﻿/*----------------------------------------------------------------
    Copyright (C) 2018 Senparc

    文件名：MemcachedObjectCacheStrategy.cs
    文件功能描述：本地锁


    创建标识：Senparc - 20161025

    修改标识：Senparc - 20170205
    修改描述：v0.2.0 重构分布式锁

    修改标识：Senparc - 20170205
    修改描述：v1.3.0 core下，MemcachedObjectCacheStrategy.GetMemcachedClientConfiguration()方法添加注入参数
----------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using Senparc.CO2NET.Cache;

#if NET45 || NET461

#else
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
#endif

namespace Senparc.CO2NET.Cache.Memcached
{
    public class MemcachedObjectCacheStrategy : BaseCacheStrategy, IBaseObjectCacheStrategy
    {
        public MemcachedClient Cache { get; set; }
        private MemcachedClientConfiguration _config;
        private static Dictionary<string, int> _serverlist;// = SiteConfig.MemcachedAddresss; TODO:全局注册配置

        /// <summary>
        /// 注册列表
        /// </summary>
        /// <param name="serverlist">Key：服务器地址（通常为IP），Value：端口</param>
        public static void RegisterServerList(Dictionary<string, int> serverlist)
        {
            _serverlist = serverlist;
        }


        #region 单例

        /// <summary>
        /// LocalCacheStrategy的构造函数
        /// </summary>
        public MemcachedObjectCacheStrategy(/*ILoggerFactory loggerFactory, IOptions<MemcachedClientOptions> optionsAccessor*/)
        {
            _config = GetMemcachedClientConfiguration();
#if NET45 || NET461
            Cache = new MemcachedClient(_config);
#else
            Cache = new MemcachedClient(null, _config);
#endif
        }


        //静态LocalCacheStrategy
        public static IBaseObjectCacheStrategy Instance
        {
            get
            {
                return Nested.instance;//返回Nested类中的静态成员instance
            }
        }

        class Nested
        {
            static Nested()
            {
            }
            //将instance设为一个初始化的LocalCacheStrategy新实例
            internal static readonly MemcachedObjectCacheStrategy instance = new MemcachedObjectCacheStrategy();
        }

        #endregion

        #region 配置

#if NET45 || NET461
        private static MemcachedClientConfiguration GetMemcachedClientConfiguration()
#else
        private static MemcachedClientConfiguration GetMemcachedClientConfiguration(/*ILoggerFactory loggerFactory, IOptions<MemcachedClientOptions> optionsAccessor*/)
#endif
        {
            //每次都要新建

#if NET45 || NET461
            var config = new MemcachedClientConfiguration();
            foreach (var server in _serverlist)
            {
                config.Servers.Add(new IPEndPoint(IPAddress.Parse(server.Key), server.Value));
            }
            config.Protocol = MemcachedProtocol.Binary;

#else
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            ILoggerFactory loggerFactory = provider.GetService<ILoggerFactory>();
            IOptions<MemcachedClientOptions> optionsAccessor = provider.GetService<IOptions<MemcachedClientOptions>>();

            var config = new MemcachedClientConfiguration(loggerFactory, optionsAccessor);
#endif
            return config;
        }

        static MemcachedObjectCacheStrategy()
        {
            // //初始化memcache服务器池
            //SockIOPool pool = SockIOPool.GetInstance();
            ////设置Memcache池连接点服务器端。
            //pool.SetServers(serverlist);
            ////其他参数根据需要进行配置

            //pool.InitConnections = 3;
            //pool.MinConnections = 3;
            //pool.MaxConnections = 5;

            //pool.SocketConnectTimeout = 1000;
            //pool.SocketTimeout = 3000;

            //pool.MaintenanceSleep = 30;
            //pool.Failover = true;

            //pool.Nagle = false;
            //pool.Initialize();

            //cache = new MemcachedClient();
            //cache.EnableCompression = false;

            #region 内部为测试代码，因为调用RegisterServerList()静态方法前会先执行此静态构造函数，此时_serverlist还没有被初始化，故会出错

            //            try
            //            {
            //                //config.Authentication.Type = typeof(PlainTextAuthenticator);
            //                //config.Authentication.Parameters["userName"] = "username";
            //                //config.Authentication.Parameters["password"] = "password";
            //                //config.Authentication.Parameters["zone"] = "zone";//domain?   ——Jeffrey 2015.10.20
            //                DateTime dt1 = DateTime.Now;
            //                var config = GetMemcachedClientConfiguration();
            //                //var cache = new MemcachedClient(config);'


            //#if NET45 || NET461
            //                var cache = new MemcachedClient(config);
            //#else
            //                var cache = new MemcachedClient(null, config);
            //#endif

            //                var testKey = Guid.NewGuid().ToString();
            //                var testValue = Guid.NewGuid().ToString();
            //                cache.Store(StoreMode.Set, testKey, testValue);
            //                var storeValue = cache.Get(testKey);
            //                if (storeValue as string != testValue)
            //                {
            //                    throw new Exception("MemcachedStrategy失效，没有计入缓存！");
            //                }
            //                cache.Remove(testKey);
            //                DateTime dt2 = DateTime.Now;

            //                WeixinTrace.Log(string.Format("MemcachedStrategy正常启用，启动及测试耗时：{0}ms", (dt2 - dt1).TotalMilliseconds));
            //            }
            //            catch (Exception ex)
            //            {
            //                //TODO:记录是同日志
            //                WeixinTrace.Log(string.Format("MemcachedStrategy静态构造函数异常：{0}", ex.Message));
            //            }

            #endregion
        }

        #endregion

        #region IContainerCacheStrategy 成员

        //public IContainerCacheStrategy ContainerCacheStrategy
        //{
        //    get { return MemcachedContainerStrategy.Instance; }
        //}


        [Obsolete("此方法已过期，请使用 Set(TKey key, TValue value) 方法")]
        public void InsertToCache(string key, object value)
        {
            Set(key, value);
        }


        public void Set(string key, object value)//TODO:添加Timeout参数
        {
            if (string.IsNullOrEmpty(key) || value == null)
            {
                return;
            }

            var cacheKey = GetFinalKey(key);

            //TODO：加了绝对过期时间就会立即失效（再次获取后为null），memcache低版本的bug

            var json = value.SerializeToCache();
            Cache.Store(StoreMode.Set, cacheKey, json, DateTime.Now.AddDays(1));
        }

        public virtual void RemoveFromCache(string key, bool isFullKey = false)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            var cacheKey = GetFinalKey(key, isFullKey);
            Cache.Remove(cacheKey);
        }

        public virtual object Get(string key, bool isFullKey = false)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            var cacheKey = GetFinalKey(key, isFullKey);
            var json = Cache.Get<string>(cacheKey);
            var obj = json.DeserializeFromCache();
            return obj;
        }


        public virtual T Get<T>(string key, bool isFullKey = false)
        {
            if (string.IsNullOrEmpty(key))
            {
                return default(T);
            }

            var cacheKey = GetFinalKey(key, isFullKey);
            var json = Cache.Get<string>(cacheKey);
            var obj = json.DeserializeFromCache<T>();
            return obj;
        }


        public virtual IDictionary<string, object> GetAll()
        {
            throw new NotImplementedException();
        }

        public virtual bool CheckExisted(string key, bool isFullKey = false)
        {
            var cacheKey = GetFinalKey(key, isFullKey);
            object value;
            if (Cache.TryGet(cacheKey, out value))
            {
                return true;
            }
            return false;
        }

        public virtual long GetCount()
        {
            throw new NotImplementedException();//TODO:需要定义二级缓存键，从池中获取
        }

        public virtual void Update(string key, object value, bool isFullKey = false)
        {
            var cacheKey = GetFinalKey(key, isFullKey);
            var json = value.SerializeToCache();
            Cache.Store(StoreMode.Set, cacheKey, json, DateTime.Now.AddDays(1));
        }

        #endregion


        public override ICacheLock BeginCacheLock(string resourceName, string key, int retryCount = 0, TimeSpan retryDelay = new TimeSpan())
        {
            return new MemcachedCacheLock(this, resourceName, key, retryCount, retryDelay);
        }

        /// <summary>
        /// Cache.TryGet(key, out value);
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGet(string key, out object value, bool isFullKey = false)
        {
            var cacheKey = GetFinalKey(key, isFullKey);
            object json;
            if (Cache.TryGet(key, out json)) {
                value = (json as string).DeserializeFromCache();
                return true;
            }

            value = null;
            return false;
        }
    }
}
