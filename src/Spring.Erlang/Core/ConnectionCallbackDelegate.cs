// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConnectionCallbackDelegate.cs" company="The original author or authors.">
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
using Spring.Erlang.Connection;
#endregion

namespace Spring.Erlang.Core
{
    /// <summary>Execute any number of operations against the supplied OTP connection, possibly returning a result.</summary>
    /// <typeparam name="T">Type T.</typeparam>
    /// <param name="channel">The channel.</param>
    /// <returns>Object of Type T.</returns>
    public delegate T ConnectionCallbackDelegate<T>(IConnection channel);
}
