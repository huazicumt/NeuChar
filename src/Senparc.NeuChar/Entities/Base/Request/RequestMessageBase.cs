﻿#region Apache License Version 2.0
/*----------------------------------------------------------------

Copyright 2019 Suzhou Senparc Network Technology Co.,Ltd.

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
    Copyright (C) 2020 Senparc
    
    文件名：RequestMessageBase.cs
    文件功能描述：接收请求消息基类
    
    
    创建标识：Senparc - 20150211
    
    修改标识：Senparc - 20150303
    修改描述：整理接口
----------------------------------------------------------------*/



namespace Senparc.NeuChar.Entities
{
    /// <summary>
    /// 请求消息基础接口
    /// </summary>
    public interface IRequestMessageBase : IMessageBase
    {
        //删除MsgType因为企业号和公众号的MsgType为两个独立的枚举类型
        //RequestMessageType MsgType { get; }
        long MsgId { get; set; }

        RequestMsgType MsgType { get; set; }
        string Encrypt { get; set; }

    }

    /// <summary>
    /// 接收到请求的消息基类
    /// </summary>
    public class RequestMessageBase : MessageBase, IRequestMessageBase
    {
        public RequestMessageBase()
        {

        }

        //public virtual RequestMessageType MsgType
        //{
        //    get { return RequestMessageType.Text; }
        //}

        public long MsgId { get; set; }
        public virtual RequestMsgType MsgType { get; set; }
        public string Encrypt { get; set; }

    }
}
