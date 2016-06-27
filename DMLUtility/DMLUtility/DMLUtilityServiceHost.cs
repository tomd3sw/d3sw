// <copyright file="DMLUtilityServiceHost.cs" company="Deluxe Digital Distribution, Inc.">
// DMLUtilityServiceHost.cs
// Copyright (c) 2014 - 2008, Deluxe Digital Distribution, Inc.  All rights reserved.
// </copyright>

namespace DMLUtility
{
    using D3.DeluxeMediaLib.DMLBusinessLogic.Processes;
    using log4net;
    using System;
    using System.Configuration;
    using System.ServiceModel;
    using System.ServiceProcess;

    /// <summary>
    /// Class which can be started and stopped from the windows service manager.
    /// </summary>
    partial class DMLUtilityServiceHost : ServiceBase
    {
        /// <summary>
        /// Logger to be used from this class.
        /// </summary>
        private static ILog _log = LogManager.GetLogger(typeof(DMLUtilityServiceHost).Name);
        /// <summary>
        /// Host process for the dml utility wcf endpoint.
        /// </summary>
        private static ServiceHost _serviceHost = null;

        /// <summary>
        /// Initilizes the class with the default state.
        /// </summary>
        public DMLUtilityServiceHost()
        {
            InitializeComponent();
        }

        private int ProcessLocalQueueThreads
        {
            get
            {
                string setting = ConfigurationManager.AppSettings["ProcessLocalQueueThreads"];
                int threads;

                return (setting != null && int.TryParse(setting, out threads))
                    ? threads
                    : 1;
            }
        }
        /// <summary>
        /// Loads the WCF service and then starts all other background processes.
        /// </summary>
        internal void Start()
        {
            try
            {
                if (_serviceHost != null)
                    _serviceHost.Close();

                _log.Info("Starting the DML Utility service.");

                _serviceHost = new ServiceHost(typeof(DMLUtilityService));
                _serviceHost.Open();

                TransferEventsToLocalQueue.Start();

                ProcessLocalQueue.ThreadCount = ProcessLocalQueueThreads;
                ProcessLocalQueue.Start();
            }
            catch (Exception ex)
            {
                _log.Fatal("Failed to start the DML Utility windows service.", ex);
            }
        }

        /// <summary>
        /// Unloads the WCF service and sends a stop signal to all other background processes.
        /// </summary>
        internal new void Stop()
        {
            try
            {
                _log.Info("Stopping the DML Utility service.");

                if (_serviceHost != null)
                {
                    _serviceHost.Close();
                    _serviceHost = null;
                }

                TransferEventsToLocalQueue.Stop();
                ProcessLocalQueue.Stop();
            }
            catch (Exception ex)
            {
                _log.Fatal("An unhandled exception was encountered while stopping the DML Utility windows service.", ex);
            }
        }

        /// <summary>
        /// Event triggered by the service manager when the service is started.
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            Start();
        }

        /// <summary>
        /// Event triggered by the service manager when the service is stopped.
        /// </summary>
        protected override void OnStop()
        {
            Stop();
        }
    }
}
