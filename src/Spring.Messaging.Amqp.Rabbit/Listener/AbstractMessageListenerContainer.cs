// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AbstractMessageListenerContainer.cs" company="The original author or authors.">
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
using System;
using System.Threading;
using Common.Logging;
using RabbitMQ.Client;
using Spring.Context;
using Spring.Messaging.Amqp.Core;
using Spring.Messaging.Amqp.Rabbit.Connection;
using Spring.Messaging.Amqp.Rabbit.Core;
using Spring.Objects.Factory;
using Spring.Transaction.Support;
using Spring.Util;
#endregion

namespace Spring.Messaging.Amqp.Rabbit.Listener
{
    /// <summary>
    ///  An abstract message listener container.
    /// </summary>
    /// <author>Mark Pollack</author>
    public abstract class AbstractMessageListenerContainer : RabbitAccessor, IDisposable, IObjectNameAware, ILifecycle, IInitializingObject
    {
        /// <summary>
        /// Logger available to subclasses.
        /// </summary>
        protected new static readonly ILog Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The object name.
        /// </summary>
        private volatile string objectName;

        /// <summary>
        /// Flag for auto startup.
        /// </summary>
        private volatile bool autoStartup = true;

        /// <summary>
        /// The phase.
        /// </summary>
        private int phase = int.MaxValue;

        /// <summary>
        /// Flag for active.
        /// </summary>
        private volatile bool active;

        /// <summary>
        /// Flag for running.
        /// </summary>
        private volatile bool isRunning;

        /// <summary>
        /// Flag for lifecycle monitor.
        /// </summary>
        private readonly object lifecycleMonitor = new object();

        /// <summary>
        /// The queues.
        /// </summary>
        private volatile string[] queueNames;

        /// <summary>
        /// The error handler.
        /// </summary>
        private IErrorHandler errorHandler;

        /// <summary>
        /// Flag for expose listener channel.
        /// </summary>
        private bool exposeListenerChannel = true;

        /// <summary>
        /// The message listener.
        /// </summary>
        private volatile object messageListener;

        /// <summary>
        /// The acknowledge mode.
        /// </summary>
        private volatile AcknowledgeModeUtils.AcknowledgeMode acknowledgeMode = AcknowledgeModeUtils.AcknowledgeMode.Auto;

        /// <summary>
        /// Flag for initialized.
        /// </summary>
        private bool initialized;

        #region Properties

        /// <summary>
        /// <para>
        /// Gets or sets AcknowledgeMode. 
        /// Flag controlling the behaviour of the container with respect to message acknowledgement. The most common usage is
        /// to let the container handle the acknowledgements (so the listener doesn't need to know about the channel or the
        /// message).
        /// </para>
        /// <para>
        /// Set to {@link AcknowledgeMode#Manual} if the listener will send the acknowledgements itself using
        /// {@link Channel#basicAck(long, boolean)}. Manual acks are consistent with either a transactional or
        /// non-transactional channel, but if you are doing no other work on the channel at the same other than receiving a
        /// single message then the transaction is probably unnecessary.
        /// </para>
        /// <para>
        /// Set to {@link AcknowledgeMode#None} to tell the broker not to expect any acknowledgements, and it will assume all
        /// messages are acknowledged as soon as they are sent (this is "autoack" in native Rabbit broker terms). If
        /// {@link AcknowledgeMode#None} then the channel cannot be transactional (so the container will fail on start up if
        /// that flag is accidentally set).
        /// </para>
        /// <para> 
        /// @param acknowledgeMode the acknowledge mode to set. Defaults to {@link AcknowledgeMode#Auto}
        /// @see AcknowledgeMode
        /// </para>
        /// </summary>
        public AcknowledgeModeUtils.AcknowledgeMode AcknowledgeMode { get { return this.acknowledgeMode; } set { this.acknowledgeMode = value; } }

        /// <summary>
        /// Gets or sets the name of the queues to receive messages from.
        /// </summary>
        /// <value>The name of the queues. Can not be null.</value>
        public string[] QueueNames { get { return this.queueNames; } set { this.queueNames = value; } }

        /// <summary>
        /// Sets the queues.
        /// </summary>
        /// <value>The queues.</value>
        public Queue[] Queues
        {
            set
            {
                var queueNames = new string[value.Length];

                for (var i = 0; i < value.Length; i++)
                {
                    AssertUtils.ArgumentNotNull(value[i], "Queue must not be null.");
                    queueNames[i] = value[i].Name;
                }

                this.queueNames = queueNames;
            }
        }

        /// <summary>
        /// Gets the required queue names.
        /// </summary>
        /// <returns>The required queue names.</returns>
        public string[] GetRequiredQueueNames()
        {
            AssertUtils.ArgumentNotNull(this.queueNames, "Queue");
            AssertUtils.State(this.queueNames.Length > 0, "Queue names must not be empty.");
            return this.queueNames;
        }

        /// <summary>
        /// Gets or sets a value indicating whether ExposeListenerChannel.
        /// Exposes the listener channel to a registered
        /// <see cref="Spring.Messaging.Amqp.Rabbit.Core.IChannelAwareMessageListener"/> as well as to
        /// <see cref="Spring.Messaging.Amqp.Rabbit.Core.RabbitTemplate"/> calls.
        /// Default is true, reusing the listener's <see cref="IModel"/>
        /// </summary>
        /// <value><c>true</c> if expose listener channel; otherwise, <c>false</c>.</value>
        /// <see cref="Spring.Messaging.Amqp.Rabbit.Core.IChannelAwareMessageListener"/>
        public bool ExposeListenerChannel { get { return this.exposeListenerChannel; } set { this.exposeListenerChannel = value; } }

        /// <summary>
        /// Gets or sets the message listener to register with the container.  This
        /// can be either a Spring <see cref="IMessageListener"/> object or
        /// a Spring <see cref="IChannelAwareMessageListener"/> object.
        /// </summary>
        /// <value>The message listener.</value>
        /// <exception cref="ArgumentException">If the supplied listener</exception> is not a <see cref="IMessageListener"/> or <see cref="IChannelAwareMessageListener"/> <see cref="IMessageListener"/>
        public object MessageListener
        {
            get { return this.messageListener; }

            set
            {
                this.CheckMessageListener(value);
                this.messageListener = value;
            }
        }

        /// <summary>Checks the message listener, throwing an exception
        /// if it does not correspond to a supported listener type.
        /// By default, only a <see cref="IMessageListener"/> object or a
        /// Spring <see cref="IChannelAwareMessageListener"/> object will be accepted.</summary>
        /// <param name="messageListener">The message listener.</param>
        protected virtual void CheckMessageListener(object messageListener)
        {
            AssertUtils.ArgumentNotNull(messageListener, "IMessage Listener can not be null");
            if (!(messageListener is IMessageListener || messageListener is IChannelAwareMessageListener || messageListener is Action<Message>))
            {
                throw new ArgumentException("messageListener needs to be of type [" + typeof(IMessageListener).FullName + "] or [" + typeof(IChannelAwareMessageListener).FullName + "] or [" + typeof(Action<Message>) + "]");
            }
        }

        /// <summary>
        /// Sets an ErrorHandler to be invoked in case of any uncaught exceptions thrown
        /// while processing a Message. By default there will be no ErrorHandler
        /// so that error-level logging is the only result.
        /// </summary>
        /// <value>The error handler.</value>
        public IErrorHandler ErrorHandler { set { this.errorHandler = value; } }

        /// <summary>
        /// Gets or sets a value indicating whether AutoStartup.
        /// </summary>
        public bool AutoStartup { get { return this.autoStartup; } set { this.autoStartup = value; } }

        /// <summary>
        /// Gets or sets Phase.
        /// </summary>
        public int Phase { get { return this.phase; } set { this.phase = value; } }

        /// <summary>
        /// Gets or sets ObjectName.
        /// </summary>
        public string ObjectName { get { return this.objectName; } set { this.objectName = value; } }
        #endregion

        /// <summary>
        /// Delegates to {@link #validateConfiguration()} and {@link #initialize()}.
        /// </summary>
        public override void AfterPropertiesSet()
        {
            base.AfterPropertiesSet();
            AssertUtils.State(
                this.exposeListenerChannel || !this.AcknowledgeMode.IsManual(), 
                "You cannot acknowledge messages manually if the channel is not exposed to the listener " + "(please check your configuration and set exposeListenerChannel=true or acknowledgeMode!=Manual)");
            AssertUtils.State(
                !(this.AcknowledgeMode.IsAutoAck() && this.ChannelTransacted), 
                "The acknowledgeMode is None (autoack in Rabbit terms) which is not consistent with having a " + "transactional channel. Either use a different AcknowledgeMode or make sure channelTransacted=false");
            this.ValidateConfiguration();
            this.Initialize();
        }

        /// <summary>
        /// Validate the configuration of this container. The default implementation is empty. To be overridden in subclasses.
        /// </summary>
        protected virtual void ValidateConfiguration() { }

        /// <summary>
        /// Calls {@link #shutdown()} when the ObjectFactory destroys the container instance.
        /// </summary>
        public void Dispose() { this.Shutdown(); }

        #region Lifecycle Methods For Starting and Stopping the Container

        /// <summary>
        /// Initialize this container.
        /// </summary>
        public void Initialize()
        {
            try
            {
                lock (this.lifecycleMonitor)
                {
                    Monitor.PulseAll(this.lifecycleMonitor);
                }

                this.DoInitialize();
            }
            catch (Exception ex)
            {
                throw this.ConvertRabbitAccessException(ex);
            }
        }

        /// <summary>
        /// Stop the shared Connection, call {@link #doShutdown()}, and close this container.
        /// </summary>
        public void Shutdown()
        {
            Logger.Debug(m => m("Shutting down Rabbit listener container"));
            lock (this.lifecycleMonitor)
            {
                this.active = false;
                Monitor.PulseAll(this.lifecycleMonitor);
            }

            // Shut down the invokers.
            try
            {
                this.DoShutdown();
            }
            catch (Exception ex)
            {
                throw this.ConvertRabbitAccessException(ex);
            }
            finally
            {
                lock (this.lifecycleMonitor)
                {
                    this.isRunning = false;
                    Monitor.PulseAll(this.lifecycleMonitor);
                }
            }
        }

        /// <summary>
        /// Register any invokers within this container. Subclasses need to implement this method for their specific invoker management process.
        /// </summary>
        protected abstract void DoInitialize();

        /// <summary>
        /// Close the registered invokers. Subclasses need to implement this method for their specific invoker management process. A shared Rabbit Connection, if any, will automatically be closed <i>afterwards</i>.
        /// </summary>
        protected abstract void DoShutdown();

        /// <summary>
        /// Gets a value indicating whether IsActive.
        /// </summary>
        public bool IsActive
        {
            get
            {
                lock (this.lifecycleMonitor)
                {
                    return this.active;
                }
            }
        }

        #region ILifecycle Implementation

        /// <summary>
        /// Start this container.
        /// </summary>
        public void Start()
        {
            if (!this.initialized)
            {
                lock (this.lifecycleMonitor)
                {
                    if (!this.initialized)
                    {
                        this.AfterPropertiesSet();
                        this.initialized = true;
                    }
                }
            }

            try
            {
                Logger.Debug(m => m("Starting Rabbit listener container."));

                this.DoStart();
            }
            catch (Exception ex)
            {
                throw this.ConvertRabbitAccessException(ex);
            }
        }

        /// <summary>
        /// Start this container, and notify all invoker tasks.
        /// </summary>
        protected virtual void DoStart()
        {
            // Reschedule paused tasks, if any.
            lock (this.lifecycleMonitor)
            {
                this.active = true;
                this.isRunning = true;
                Monitor.PulseAll(this.lifecycleMonitor);
            }
        }

        /// <summary>
        /// Stop this container.
        /// </summary>
        /// <exception cref="SystemException">
        /// </exception>
        public void Stop()
        {
            try
            {
                this.DoStop();
            }
            catch (Exception ex)
            {
                throw this.ConvertRabbitAccessException(ex);
            }
            finally
            {
                lock (this.lifecycleMonitor)
                {
                    this.isRunning = false;
                    Monitor.PulseAll(this.lifecycleMonitor);
                }
            }
        }

        /// <summary>Stop this container.</summary>
        /// <param name="callback">The callback.</param>
        public void Stop(Action callback)
        {
            this.Stop();
            callback.Invoke();
        }

        /// <summary>
        /// This method is invoked when the container is stopping. The default implementation does nothing, but subclasses may override.
        /// </summary>
        protected virtual void DoStop() { }

        /// <summary>
        /// Determine whether this container is currently running, that is, whether it has been started and not stopped yet.
        /// </summary>
        /// <value><c>true</c> if this component is running; otherwise, <c>false</c>.</value>
        public bool IsRunning
        {
            get
            {
                lock (this.lifecycleMonitor)
                {
                    return this.isRunning;
                }
            }
        }
        #endregion

        /// <summary>Invoke the registered ErrorHandler, if any. Log at error level otherwise.</summary>
        /// <param name="ex">The ex.</param>
        protected void InvokeErrorHandler(Exception ex)
        {
            if (this.errorHandler != null)
            {
                this.errorHandler.HandleError(ex);
            }
            else
            {
                Logger.Warn(m => m("Execution of Rabbit message listener failed, and no ErrorHandler has been set."), ex);
            }
        }

        #endregion

        #region Template methods for listener execution

        /// <summary>Executes the specified listener,
        /// committing or rolling back the transaction afterwards (if necessary).</summary>
        /// <param name="channel">The channel.</param>
        /// <param name="message">The received message.</param>
        /// <see cref="InvokeListener"/><see cref="CommitIfNecessary"/><see cref="RollbackOnExceptionIfNecessary"/><see cref="HandleListenerException"/>
        protected virtual void ExecuteListener(IModel channel, Message message)
        {
            if (!this.IsRunning)
            {
                Logger.Warn(m => m("Rejecting received message because the listener container has been stopped: {0}", message));

                throw new MessageRejectedWhileStoppingException();
            }

            try
            {
                this.InvokeListener(channel, message);
            }
            catch (Exception ex)
            {
                this.HandleListenerException(ex);
                throw ex;
            }
        }

        /// <summary>Invokes the specified listener</summary>
        /// <param name="channel">The channel to operate on.</param>
        /// <param name="message">The received message.</param>
        /// <see cref="MessageListener"/>
        public virtual void InvokeListener(IModel channel, Message message)
        {
            var listener = this.MessageListener;
            if (listener is IChannelAwareMessageListener)
            {
                this.DoInvokeListener((IChannelAwareMessageListener)listener, channel, message);
            }
            else if (listener is IMessageListener || listener is Action<Message>)
            {
                var bindChannel = this.ExposeListenerChannel && this.IsChannelLocallyTransacted(channel);
                if (bindChannel)
                {
                    var resourceHolder = new RabbitResourceHolder(channel, false);
                    resourceHolder.SynchronizedWithTransaction = true;
                    TransactionSynchronizationManager.BindResource(this.ConnectionFactory, resourceHolder);
                }

                try
                {
                    if (listener is IMessageListener)
                    {
                        this.DoInvokeListener((IMessageListener)listener, message);
                    }
                    else if (listener is Action<Message>)
                    {
                        this.DoInvokeListener((Action<Message>)listener, message);
                    }
                }
                finally
                {
                    if (bindChannel)
                    {
                        // unbind if we bound
                        TransactionSynchronizationManager.UnbindResource(this.ConnectionFactory);
                    }
                }
            }
            else if (listener != null)
            {
                throw new ArgumentException("Only MessageListener and SessionAwareMessageListener supported: " + listener);
            }
            else
            {
                throw new InvalidOperationException("No message listener specified - see property MessageListener");
            }
        }

        /// <summary>Invoke the specified listener as Spring SessionAwareMessageListener,
        /// exposing a new Rabbit Channel (potentially with its own transaction)
        /// to the listener if demanded.</summary>
        /// <param name="listener">The Spring ISessionAwareMessageListener to invoke.</param>
        /// <param name="channel">The channel to operate on.</param>
        /// <param name="message">The received message.</param>
        /// <see cref="IChannelAwareMessageListener"/><see cref="ExposeListenerChannel"/>
        protected virtual void DoInvokeListener(IChannelAwareMessageListener listener, IModel channel, Message message)
        {
            RabbitResourceHolder resourceHolder = null;

            var channelToUse = channel;
            var boundHere = false;

            try
            {
                if (!this.ExposeListenerChannel)
                {
                    // We need to expose a separate Channel.
                    resourceHolder = this.GetTransactionalResourceHolder();
                    channelToUse = resourceHolder.Channel;

                    if (this.IsChannelLocallyTransacted(channelToUse) &&
                        !TransactionSynchronizationManager.ActualTransactionActive)
                    {
                        resourceHolder.SynchronizedWithTransaction = true;
                        TransactionSynchronizationManager.BindResource(
                            this.ConnectionFactory, 
                            resourceHolder);
                        boundHere = true;
                    }
                }
                else
                {
                    // if locally transacted, bind the current channel to make it available to RabbitTemplate
                    if (this.IsChannelLocallyTransacted(channel))
                    {
                        var localResourceHolder = new RabbitResourceHolder(channelToUse, false);
                        localResourceHolder.SynchronizedWithTransaction = true;
                        TransactionSynchronizationManager.BindResource(this.ConnectionFactory, localResourceHolder);
                        boundHere = true;
                    }
                }

                // Actually invoke the message listener
                try
                {
                    listener.OnMessage(message, channelToUse);
                }
                catch (Exception e)
                {
                    throw this.WrapToListenerExecutionFailedExceptionIfNeeded(e);
                }
            }
            finally
            {
                if (resourceHolder != null && boundHere)
                {
                    // so the channel exposed (because exposeListenerChannel is false) will be closed
                    resourceHolder.SynchronizedWithTransaction = false;
                }

                ConnectionFactoryUtils.ReleaseResources(resourceHolder);
                if (boundHere)
                {
                    // unbind if we bound
                    TransactionSynchronizationManager.UnbindResource(this.ConnectionFactory);
                    if (!this.ExposeListenerChannel && this.IsChannelLocallyTransacted(channelToUse))
                    {
                        /*
                         *  commit the temporary channel we exposed; the consumer's channel
                         *  will be committed later. Note that when exposing a different channel
                         *  when there's no transaction manager, the exposed channel is committed
                         *  on each message, and not based on txSize.
                         */
                        RabbitUtils.CommitIfNecessary(channelToUse);
                    }
                }
            }
        }

        /// <summary>Invoke the specified listener a Spring Rabbit MessageListener.</summary>
        /// <remarks>Default implementation performs a plain invocation of the
        /// <code>OnMessage</code>
        /// methods</remarks>
        /// <param name="listener">The listener to invoke.</param>
        /// <param name="message">The received message.</param>
        protected virtual void DoInvokeListener(IMessageListener listener, Message message)
        {
            try
            {
                listener.OnMessage(message);
            }
            catch (Exception e)
            {
                throw this.WrapToListenerExecutionFailedExceptionIfNeeded(e);
            }
        }

        /// <summary>The do invoke listener.</summary>
        /// <param name="listener">The listener.</param>
        /// <param name="message">The message.</param>
        /// <exception cref="Exception"></exception>
        protected virtual void DoInvokeListener(Action<Message> listener, Message message)
        {
            try
            {
                listener.Invoke(message);
            }
            catch (Exception e)
            {
                throw this.WrapToListenerExecutionFailedExceptionIfNeeded(e);
            }
        }

        /// <summary>Determines whether the given Channel is locally transacted, that is, whether
        /// its transaction is managed by this listener container's Channel handling
        /// and not by an external transaction coordinator.</summary>
        /// <remarks>This method is about finding out whether the Channel's transaction
        /// is local or externally coordinated.</remarks>
        /// <param name="channel">The channel to check.</param>
        /// <returns><c>true</c> if the is channel locally transacted; otherwise, <c>false</c>.</returns>
        /// <see cref="RabbitAccessor.ChannelTransacted"/>
        protected virtual bool IsChannelLocallyTransacted(IModel channel) { return this.ChannelTransacted; }

        /// <summary>Handle the given exception that arose during listener execution.</summary>
        /// <remarks>The default implementation logs the exception at error level,
        /// not propagating it to the Rabbit provider - assuming that all handling of
        /// acknowledgement and/or transactions is done by this listener container.
        /// This can be overridden in subclasses.</remarks>
        /// <param name="ex">The exception to handle</param>
        protected virtual void HandleListenerException(Exception ex)
        {
            if (this.IsActive)
            {
                // Regular case: failed while active.
                // Invoke ErrorHandler if available.
                this.InvokeErrorHandler(ex);
            }
            else
            {
                // Rare case: listener thread failed after container shutdown.
                // Log at debug level, to avoid spamming the shutdown log.
                Logger.Debug(m => m("Listener exception after container shutdown"), ex);
            }
        }

        /// <summary>Wrap listener execution failed exception if needed.</summary>
        /// <param name="e">The e.</param>
        /// <returns>The exception.</returns>
        protected Exception WrapToListenerExecutionFailedExceptionIfNeeded(Exception e)
        {
            if (!(e is ListenerExecutionFailedException))
            {
                // Wrap exception to ListenerExecutionFailedException.
                return new ListenerExecutionFailedException("Listener threw exception", e);
            }

            return e;
        }

        #endregion
    }

    /// <summary>
    /// Exception that indicates that the initial setup of this container's
    /// shared Connection failed. This is indicating to invokers that they need
    /// to establish the shared Connection themselves on first access.
    /// </summary>
    public class SharedConnectionNotInitializedException : SystemException
    {
        /// <summary>Initializes a new instance of the <see cref="SharedConnectionNotInitializedException"/> class.</summary>
        /// <param name="message">The message.</param>
        public SharedConnectionNotInitializedException(string message) : base(message) { }
    }
}
