using D3.DeluxeMediaLib.DMLBusinessLogic;
using D3.DeluxeMediaLib.DMLBusinessLogic.Common;
using D3.DeluxeMediaLib.DMLBusinessLogic.Publishers;
using D3.DeluxeMediaLib.DMLInfoComponents;
using log4net;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DMLUtility
{
    /// <summary>
    /// Implements the IDMLUtitlityService API.
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)] 
    public class DMLUtilityService : IDMLUtilityService
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(DMLUtilityService));
        private static Dictionary<string, UtilityOptions> _jobs = new Dictionary<string, UtilityOptions>();
        private Object _jobsLock = new object();

        #region Methods

        /// <summary>
        /// Handles the actual generation of the DML file.
        /// </summary>
        /// <param name="options"></param>
        private void generateDmlFile(string jobID)
        {
            DateTime startTime = DateTime.Now;
            DMLUserInfo userInfo = null;
            LibraryEntityOptions libOptions = null;
            LibraryServerLocations serverLocations = null;
            XElement libraryXml = null;
            IDMLLibraryEntity libEntity = null;
            PublishDmlEvents eventPublisher = null;
            UtilityOptions options = null;

            try
            {
                _log.InfoFormat("DML Utility was initiated from the service api. {0}", options.ToString());

                options = _jobs[jobID];

                //Get user object, but bypass normal authentication
                userInfo = DMLUserEntity.SimulateUserAuthentication(options.CustomerId);

                // Load the library options class that will be passed to the business logic.
                try
                {
                    serverLocations = new LibraryServerLocations();
                    libOptions = new LibraryEntityOptions(ConfigurationManager.AppSettings, options, serverLocations);
                }
                catch (Exception ex)
                {
                    _log.Warn(String.Format("Problem while determining library options: {0}", ex));
                }

                eventPublisher = new PublishDmlEvents(new PublisherFactory(), libOptions.PublishEvents);

                libEntity = DMLLibraryEntityFactory.GetLibraryEntity(options.ApiVersion, userInfo, eventPublisher, libOptions);

                //Publish the start event for dml.
                eventPublisher.PublishDmlStarted(options);

                //Build the response.
                libraryXml = Program.BuildResponse(libEntity, libOptions, options);

                //Generate the output based on the configuration and command line options.
                Program.PerformOutput(libraryXml, options, libOptions);
            }
            finally
            {
                try
                {
                    eventPublisher.PublishDmlCompleted(options);
                    eventPublisher.Dispose();
                }
                catch { }

                lock (_jobsLock)
                {
                    if (_jobs.ContainsKey(jobID))
                        _jobs.Remove(jobID);
                }
                
                _log.InfoFormat("DML Utility service api execution completed in {0}. {1}",
                    DateTime.Now.Subtract(startTime).ToString(),options.ToString());
            }
        }

        /// <summary>
        /// Generate a new DML synchronously.
        /// </summary>
        /// <param name="options">Provides information required for DML generation such as the customer id and the output path.</param>
        /// <returns>The job id of the DML generation process that was just initiated.  If nothing was initiated then the job id will be empty.</returns> 
        public string GenerateDml(UtilityOptions options)
        {
            string jobID = string.Empty;
            string customerID = options.CustomerId.ToLower();
            bool found = false;

            if (string.IsNullOrEmpty(options.OutputFile))
                throw new FaultException("OutputFile cannot be null for the GenerateDml method.");

            // If a job is already running, simply return an empty string.
            found = (from j in _jobs.Values
                     where j.CustomerId.ToLower() == customerID
                     select true).FirstOrDefault();

            if (found)
                return string.Empty;

            // Generate a new job id.
            jobID = Guid.NewGuid().ToString();
            _jobs.Add(jobID, options);

            // Kick off the DML generation in a separate thread.
            Task.Run(new Action(delegate() { generateDmlFile(jobID); }));

            // Return the job id.
            return jobID;
        }

        /// <summary>
        /// Check the status of a specific job.
        /// </summary>
        /// <param name="jobID">The job id returned by the GenerateDml call.</param>
        /// <returns>True if the job is currently running.</returns>
        public bool IsJobRunning(string jobID)
        {
            return _jobs.ContainsKey(jobID);
        }

        /// <summary>
        /// Returns the specific start-up options of a currently running job for a specific customer.
        /// </summary>
        /// <param name="customerID">The customer being queried.</param>
        /// <returns>The startup options for the customer's job.  Null if no job is currently running for this customer.</returns>
        public UtilityOptions GetJobByCustomer(string customerID)
        {
            return (from j in _jobs.Values
                    where j.CustomerId.Equals(customerID, StringComparison.OrdinalIgnoreCase)
                    select j).FirstOrDefault();
        }

        /// <summary>
        /// Returns the number of jobs which are currently running within this service.
        /// </summary>
        /// <returns>Count of currently running jobs.</returns>
        public int GetRunningJobsCount()
        {
            return _jobs.Count();
        }

        /// <summary>
        /// Returns true.  Used to easily test whether the endpoint is listening.
        /// </summary>
        /// <returns>True.</returns>
        public bool IsAlive()
        {
            return true;
        }

        #endregion Methods
    }
}
