﻿#region Apache License Version 2.0
/*----------------------------------------------------------------

Copyright 2018 Suzhou Senparc Network Technology Co.,Ltd.

Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
except in compliance with the License. You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software distributed under the
License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
either express or implied. See the License for the specific language governing permissions
and limitations under the License.

Detail: https://github.com/JeffreySu/WeiXinMPSDK/blob/master/license.md

----------------------------------------------------------------*/
#endregion Apache License Version 2.0

/*----------------------------------------------------------------
    Copyright (C) 2018 Senparc
    
    文件名：WeixinContext.cs
    文件功能描述：微信消息上下文（全局）
    
    
    创建标识：Senparc - 20150211
    
    修改标识：Senparc - 20150303
    修改描述：整理接口
    
    修改标识：Senparc - 20181023
    修改描述：修改 timeSpan 获取判断逻辑（firstMessageContext.LastActiveTime 已改为 DateTime? 类型）

    修改标识：Senparc - 20190914
    修改描述：（V3.0）v0.8.0 提供支持分布式缓存的消息上下文（MessageContext）

----------------------------------------------------------------*/

/*
 * V3.2
 * V4.0 添加异步方法
 * v5.0 支持分布式缓存
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Senparc.CO2NET.Cache;
using Senparc.CO2NET.Extensions;
using Senparc.NeuChar.Entities;

namespace Senparc.NeuChar.Context
{
    /// <summary>
    /// 消息上下文全局设置
    /// </summary>
    public static class MessageContextGlobalConfig//TODO:所有设置可以整合到一起
    {
        ///// <summary>
        ///// 上下文操作使用的同步锁
        ///// </summary>
        //public static object Lock = new object();//TODO:转为同步锁
        public const string MESSAGE_CONTENT_ITEM_LOCK_NAME = "MESSAGE_CONTENT_ITEM_LOCK_NAME";

        ///// <summary>
        ///// 去重专用锁
        ///// </summary>
        //public static object OmitRepeatLock = new object();//TODO:转为同步锁
        public const string MESSAGE_CONTENT_OMIT_REPEAT_LOCK_NAME = "MESSAGE_CONTENT_OMIT_REPEAT_LOCK_NAME";

        /// <summary>
        /// 缓存键前缀
        /// </summary>
        public const string CACHE_KEY_PREFIX = "MessageContext:";

        /// <summary>
        /// 是否开启上下文记录
        /// </summary>
        public static bool UseMessageContext { get; set; } = true;



        /// <summary>
        /// 每一个MessageContext过期时间（分钟）
        /// </summary>
        public static Double ExpireMinutes { get; set; }

        /// <summary>
        /// 最大储存上下文数量（分别针对请求和响应信息）
        /// </summary>
        public static int MaxRecordCount { get; set; }
    }


    /// <summary>
    /// 微信消息上下文操作对象（全局）
    /// 默认过期时间：90分钟
    /// </summary>
    public class GlobalMessageContext<TMC, TRequest, TResponse>
        where TMC : class, IMessageContext<TRequest, TResponse>, new() //TODO:TRequest, TResponse直接写明基类类型
        where TRequest : class, IRequestMessageBase
        where TResponse : class, IResponseMessageBase
    {

        ///// <summary>
        ///// 所有MessageContext集合，不要直接操作此对象
        ///// </summary>
        //public Dictionary<string, TM> MessageCollection { get; set; }
        //TODO:换成整个数据库访问

        ///// <summary>
        ///// MessageContext队列（LastActiveTime升序排列）,不要直接操作此对象
        ///// </summary>
        //public MessageQueue<TM, TRequest, TResponse> MessageQueue
        //{
        //    get
        //    {
        //        if (_messageQueue == null)
        //        {
        //            //载入
        //            var cache = CacheStrategyFactory.GetObjectCacheStrategyInstance();
        //            //TODO:获取命名空间下的所有对象
        //            var messageQueue = (MessageQueue<TM, TRequest, TResponse>)null;
        //            return messageQueue;
        //        }
        //        return null;
        //    }
        //}




        public GlobalMessageContext()
        {
            //Restore();
        }

        private string GetCacheKey(string userName)
        {
            return $"{MessageContextGlobalConfig.CACHE_KEY_PREFIX}{userName}";
        }

        /// <summary>
        /// 重置所有上下文参数，所有记录将被清空（如果缓存数据比较多，性能开销将会比较大，请谨慎操作）
        /// </summary>
        public void Restore()
        {
            var cache = CacheStrategyFactory.GetObjectCacheStrategyInstance();

            //删除所有键
            var finalKeyPrefix = cache.GetFinalKey(GetCacheKey(""));
            var allObjects = cache.GetAll();
            var messageContextObjects = allObjects.Where(z => z.Key.StartsWith(finalKeyPrefix, StringComparison.Ordinal)).ToList();
            foreach (var item in messageContextObjects)
            {
                Console.WriteLine($"{item.Key}");
                cache.RemoveFromCache(item.Key, true);//移除
            }

            MessageContextGlobalConfig.ExpireMinutes = 90;
        }

        /// <summary>
        /// 获取过期时间 TimeSpan 对象
        /// </summary>
        /// <param name="expireMinutes"></param>
        /// <returns></returns>
        private TimeSpan? GetExpireTimeSpan(double? expireMinutes = null)
        {
            expireMinutes = expireMinutes ?? MessageContextGlobalConfig.ExpireMinutes;
            TimeSpan? expireTimeSpan = expireMinutes > 0 ? TimeSpan.FromMinutes(expireMinutes.Value) : (TimeSpan?)null;
            return expireTimeSpan;
        }

        /// <summary>
        /// 获取MessageContext，如果不存在，返回null
        /// 这个方法的更重要意义在于操作TM队列，及时移除过期信息，并将最新活动的对象移到尾部
        /// </summary>
        /// <param name="userName">用户名（OpenId）</param>
        /// <returns></returns>
        private TMC GetMessageContext(string userName)
        {
            //检查并移除过期记录，为了尽量节约资源，这里暂不使用独立线程轮询



            //while (MessageQueue.Count > 0)
            //{
            //    var firstMessageContext = MessageQueue[0];
            //    var timeSpan = SystemTime.Now - (firstMessageContext.LastActiveTime.HasValue ? firstMessageContext.LastActiveTime.Value : SystemTime.Now);
            //    //确定对话过期时间
            //    var expireMinutes = firstMessageContext.ExpireMinutes.HasValue
            //        ? firstMessageContext.ExpireMinutes.Value //队列自定义事件
            //        : this.ExpireMinutes;//全局统一默认时间

            //    //TODO:这里假设按照队列顺序过期，实际再加入了自定义过期时间之后，可能不遵循这个规律   —— Jeffrey Su 2018.1.23
            //    if (timeSpan.TotalMinutes >= expireMinutes)
            //    {
            //        MessageQueue.RemoveAt(0);//从队列中移除过期对象
            //        MessageCollection.Remove(firstMessageContext.UserName);//从集合中删除过期对象

            //        //添加事件回调
            //        firstMessageContext.OnRemoved();//TODO:此处异步处理，或用户在自己操作的时候异步处理需要耗费时间比较长的操作。
            //    }
            //    else
            //    {
            //        break;
            //    }
            //}


            //TODO:使用缓存过期后，OnRemoved() 将失效

            /* 
                * 全局只有在这里用到MessageCollection.ContainsKey
                * 充分分离MessageCollection内部操作，
                * 为以后变化或扩展MessageCollection留余地
                */


            //if (!MessageCollection.ContainsKey(userName))
            //{
            //    return null;
            //}

            //return MessageCollection[userName];


            //以下为新版本代码
            var cache = CacheStrategyFactory.GetObjectCacheStrategyInstance();
            var cacheKey = this.GetCacheKey(userName);

            //注意：这里日过直接反序列化成 TMC，将无法保存类型，需要使用JsonConverter

            if (cache.CheckExisted(cacheKey))
            {
                var jsonStr = cache.Get(cacheKey) as Newtonsoft.Json.Linq.JObject;//类型：Newtonsoft.Json.Linq.JObject

                if (jsonStr == null)
                {
                    return null;
                }
                
                var result = JsonConvert.DeserializeObject<TMC>(jsonStr.ToString(), new MessageContextJsonConverter<TMC, TRequest, TResponse>());
                Console.WriteLine("从缓存读取result："+result.ToJson());
                return result;
            }
            else
            {

                return null;
            }

            //return cache.Get<TMC>(cacheKey);
        }

        /// <summary>
        /// 获取MessageContext
        /// </summary>
        /// <param name="userName">用户名（OpenId）</param>
        /// <param name="createIfNotExists">true：如果用户不存在，则创建一个实例，并返回这个最新的实例
        /// false：如用户不存在，则返回null</param>
        /// <returns></returns>
        private TMC GetMessageContext(string userName, bool createIfNotExists)
        {
            var messageContext = GetMessageContext(userName);

            if (messageContext == null)
            {
                if (createIfNotExists)
                {
                    //全局只在这一个地方使用写入单用户上下文的原始对象
                    var newMessageContext = new TMC()
                    {
                        UserName = userName,
                        MaxRecordCount = MessageContextGlobalConfig.MaxRecordCount
                    };

                    var cache = CacheStrategyFactory.GetObjectCacheStrategyInstance();
                    var cacheKey = this.GetCacheKey(userName);
                    var expireTime = GetExpireTimeSpan();
                    cache.Set(cacheKey, newMessageContext, expireTime);//插入单用户上下文的原始缓存对象
                    //messageContext = GetMessageContext(userName);//注意！！这里如果使用Redis等分布式缓存立即从缓存读取，可能会因为还没有存入，发生为null的情况
                    messageContext = newMessageContext;
                }
                else
                {
                    return null;
                }
            }
            return messageContext;
        }

        /// <summary>
        /// 获取MessageContext，如果不存在，使用requestMessage信息初始化一个，并返回原始实例
        /// </summary>
        /// <returns></returns>
        public TMC GetMessageContext(TRequest requestMessage)
        {
            var cache = CacheStrategyFactory.GetObjectCacheStrategyInstance();
            using (cache.BeginCacheLock(MessageContextGlobalConfig.MESSAGE_CONTENT_ITEM_LOCK_NAME, $"GetMessageContext-{requestMessage.FromUserName}"))
            {
                return GetMessageContext(requestMessage.FromUserName, true);
            }
        }

        /// <summary>
        /// 获取MessageContext，如果不存在，使用responseMessage信息初始化一个，并返回原始实例
        /// </summary>
        /// <returns></returns>
        public TMC GetMessageContext(TResponse responseMessage)
        {
            var cache = CacheStrategyFactory.GetObjectCacheStrategyInstance();
            using (cache.BeginCacheLock(MessageContextGlobalConfig.MESSAGE_CONTENT_ITEM_LOCK_NAME, $"GetMessageContext-{responseMessage.ToUserName}"))
            {
                return GetMessageContext(responseMessage.ToUserName, true);
            }
        }

        /// <summary>
        /// 记录请求信息
        /// </summary>
        /// <param name="requestMessage">请求信息</param>
        public void InsertMessage(TRequest requestMessage)
        {
            var userName = requestMessage.FromUserName;
            var cache = CacheStrategyFactory.GetObjectCacheStrategyInstance();
            using (cache.BeginCacheLock(MessageContextGlobalConfig.MESSAGE_CONTENT_ITEM_LOCK_NAME, $"InsertMessage-{userName}"))
            {
                var messageContext = GetMessageContext(userName, true);
                //if (messageContext.RequestMessages.Count > 0)
                //{
                //    //如果不是新建的对象，把当前对象移到队列尾部（新对象已经在底部）
                //    var messageContextInQueue =
                //        MessageQueue.FindIndex(z => z.UserName == userName);

                //    if (messageContextInQueue >= 0)
                //    {
                //        MessageQueue.RemoveAt(messageContextInQueue); //移除当前对象
                //        MessageQueue.Add(messageContext); //插入到末尾
                //    }
                //}

                messageContext.LastActiveTime = messageContext.ThisActiveTime;//记录上一次请求时间
                messageContext.ThisActiveTime = SystemTime.Now;//记录本次请求时间
                messageContext.RequestMessages.Add(requestMessage);//录入消息

                var cacheKey = GetCacheKey(userName);
                var expireTime = GetExpireTimeSpan();
                cache.Set(cacheKey, messageContext, expireTime);
            }
        }

        /// <summary>
        /// 记录响应信息
        /// </summary>
        /// <param name="responseMessage">响应信息</param>
        public void InsertMessage(TResponse responseMessage)
        {
            var userName = responseMessage.ToUserName;
            var cache = CacheStrategyFactory.GetObjectCacheStrategyInstance();
            using (cache.BeginCacheLock(MessageContextGlobalConfig.MESSAGE_CONTENT_ITEM_LOCK_NAME, $"InsertMessage-{userName}"))
            {
                var messageContext = GetMessageContext(userName, true);
                messageContext.ResponseMessages.Add(responseMessage);

                var cacheKey = GetCacheKey(userName);
                var expireTime = GetExpireTimeSpan();
                cache.Set(cacheKey, messageContext, expireTime);
            }
        }

        /// <summary>
        /// 获取最新一条请求数据，如果不存在，则返回null
        /// </summary>
        /// <param name="userName">用户名（OpenId）</param>
        /// <returns></returns>
        public TRequest GetLastRequestMessage(string userName)
        {
            var cache = CacheStrategyFactory.GetObjectCacheStrategyInstance();
            using (cache.BeginCacheLock(MessageContextGlobalConfig.MESSAGE_CONTENT_ITEM_LOCK_NAME, $"GetMessageContext-{userName}"))
            {
                var messageContext = GetMessageContext(userName, true);
                return messageContext.RequestMessages.LastOrDefault();
            }
        }

        /// <summary>
        /// 获取最新一条响应数据，如果不存在，则返回null
        /// </summary>
        /// <param name="userName">用户名（OpenId）</param>
        /// <returns></returns>
        public TResponse GetLastResponseMessage(string userName)
        {
            var cache = CacheStrategyFactory.GetObjectCacheStrategyInstance();
            using (cache.BeginCacheLock(MessageContextGlobalConfig.MESSAGE_CONTENT_ITEM_LOCK_NAME, $"GetMessageContext-{userName}"))
            {
                var messageContext = GetMessageContext(userName, true);
                return messageContext.ResponseMessages.LastOrDefault();
            }
        }
    }
}
