using D3.DeluxeMediaLib.DMLInfoComponents;
using System.Collections.Generic;
using System.ServiceModel;

namespace DMLUtility
{
    /// <summary>
    /// WCF interface for the utility service.  The interface allows clients to generate a new DML, check the status of a DML and to get a list
    /// of customers.
    /// </summary>
    [ServiceContract]
    public interface IDMLUtilityService
    {
        /// <summary>
        /// Generate a new DML synchronously.
        /// </summary>
        /// <returns>The job id of the DML generation process that was just initiated.  If nothing was initiated then the job id will be empty.</returns>
        /// <param name="options">Provides information required for DML generation such as the customer id and the output path.</param>
        /// <exception cref="D3.DeluxeMediaLib.DMLInfoComponents.UtilityApiDtos.DmlConcurrencyFaultException">
        /// Thrown if the service has already reached its maximum concurrency.
        /// </exception>
        [OperationContract]
        string GenerateDml(UtilityOptions options);

        /// <summary>
        /// Check the status of a specific job.
        /// </summary>
        /// <param name="jobID">The job id returned by the GenerateDml call.</param>
        /// <returns>True if the job is currently running.</returns>
        [OperationContract]
        bool IsJobRunning(string jobID);

        /// <summary>
        /// Returns the specific start-up options of a currently running job for a specific customer.
        /// </summary>
        /// <param name="customerID">The customer being queried.</param>
        /// <returns>The startup options for the customer's job.  Null if no job is currently running for this customer.</returns>
        [OperationContract]
        UtilityOptions GetJobByCustomer(string customerID);

        /// <summary>
        /// Returns the number of jobs which are currently running within this service.
        /// </summary>
        /// <returns>Count of currently running jobs.</returns>
        [OperationContract]
        int GetRunningJobsCount();
  
        /// <summary>
        /// Returns true.  Used to easily test whether the endpoint is listening.
        /// </summary>
        /// <returns>True.</returns>
        [OperationContract]
        bool IsAlive();
    }
}
