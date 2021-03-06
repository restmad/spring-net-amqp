﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IReturnCallback.cs" company="The original author or authors.">
//   Copyright 2002-2012 the original author or authors.
//   
//   Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
//   the License. You may obtain a copy of the License at
//   
//   http://www.apache.org/licenses/LICENSE-2.0
//   
//   Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
//   an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the
//   specific language governing permissions and limitations under the License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

#region Using Directives
using Spring.Messaging.Amqp.Core;
#endregion

namespace Spring.Messaging.Amqp.Rabbit.Core
{
    /// <summary>
    /// Return callback interface.
    /// </summary>
    public interface IReturnCallback
    {
        /// <summary>The returned message.</summary>
        /// <param name="message">The message.</param>
        /// <param name="replyCode">The reply code.</param>
        /// <param name="replyText">The reply text.</param>
        /// <param name="exchange">The exchange.</param>
        /// <param name="routingKey">The routing key.</param>
        void ReturnedMessage(Message message, int replyCode, string replyText, string exchange, string routingKey);
    }
}
