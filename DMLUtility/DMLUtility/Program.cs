using D3.DeluxeMediaLib.DMLBusinessLogic;
using D3.DeluxeMediaLib.DMLBusinessLogic.Common;
using D3.DeluxeMediaLib.DMLBusinessLogic.Publishers;
using D3.DeluxeMediaLib.DMLInfoComponents;
using log4net;
using System;
using System.Configuration;
using System.IO;
using System.ServiceProcess;
using System.Xml.Linq;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace DMLUtility
{
    /// <summary>
    /// Startup class for the DMLUtility executable.
    /// </summary>
    class Program
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(Program));

        #region Methods

        static void Main(string[] args)
        {
            DMLUtilityServiceHost service = new DMLUtilityServiceHost();

            // If command-line args are supplied, we assume that the utility is running
            // as an executable.  Otherwise, if we're running in the IDE we want to execute the
            // Start() method.  The final case is that the service is running in the service manager.

            try
            {
                if (args != null && args.Length > 0)
                    ExecuteCommandLine(new UtilityOptions(args));
                else if (Environment.UserInteractive)
                    service.Start();
                else
                    ServiceBase.Run(service);
            }
            catch (Exception e)
            {
                _log.Fatal(e);
            }
            finally
            {
                if (service != null) { service.Dispose(); service = null; }
            }
        }

        private static void ExecuteCommandLine(UtilityOptions options)
        {
            DateTime startTime = DateTime.Now;
            DMLUserInfo userInfo = null;
            LibraryEntityOptions libOptions = null;
            LibraryServerLocations serverLocations = null;
            XElement libraryXml = null;
            IDMLLibraryEntity libEntity = null;
            PublishDmlEvents eventPublisher = null;

            try
            {
                _log.InfoFormat("DML Utility was initiated from the command-line. {0}", options.ToString());

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
                libraryXml = BuildResponse(libEntity, libOptions, options);

                //Generate the output based on the configuration and command line options.
                PerformOutput(libraryXml, options, libOptions);
            }
            catch (Exception ex)
            {
                _log.Fatal(ex);
            }
            finally
            {
                try
                {
                    //Publish the complete event for dml.
                    eventPublisher.PublishDmlCompleted(options);
                    eventPublisher.Dispose();
                }
                catch { }
                _log.InfoFormat("DML Utility command-line execution completed in {0}. {1}",
                    DateTime.Now.Subtract(startTime).ToString(), options.ToString());
            }
        }

        internal static XElement BuildResponse(IDMLLibraryEntity libEntity, LibraryEntityOptions libOptions, UtilityOptions options)
        {
            XElement libraryXml = null;

            if (options.ProcessAll)
                return options.CompatFeed ? libEntity.GetAllCompatFeedEntries() : libEntity.GetAllLibraryEntries(libOptions.UseXmlFileDataRepo);

            if (options.CompatFeed)
            {
                if (!String.IsNullOrWhiteSpace(options.TitleId))
                    libraryXml = libEntity.GetCompatFeedEntries(options.TitleId);
                else
                    libraryXml = libEntity.GetCompatFeedEntries(options.StartTitle, options.TitleCount);
            }
            else
            {
                if (!String.IsNullOrWhiteSpace(options.TitleId))
                    libraryXml = libEntity.GetLibraryEntries(options.TitleId, libOptions.UseXmlFileDataRepo);
                else if (options.DeltaUpdate)
                    libraryXml = ((DMLLibraryEntityV2)libEntity).UpdateChangedLibraryEntries(libOptions.UseXmlFileDataRepo);
                else
                    libraryXml = libEntity.GetLibraryEntries(options.StartTitle, options.TitleCount, libOptions.UseXmlFileDataRepo);
            }

            return libraryXml;
        }

        /// <summary>
        /// Helper function which determines where the output should go and sends it there
        /// </summary>
        /// <param name="libraryXml"></param>
        /// <param name="options"></param>
        /// <param name="libOptions"></param>
        internal static void PerformOutput(XElement libraryXml, UtilityOptions options, LibraryEntityOptions libOptions)
        {
            string backupDate;
            string backupFilename;
            string ext;
            FileInfo outputFile;

            // Do nothing if no output file was set.
            if (String.IsNullOrWhiteSpace(options.OutputFile))
                return;

            // If we are not writing an xml file, simply exit.
            if (!libOptions.UseXmlFileDataRepo)
                return;

            // Save the XElement to a file.
            libraryXml.Save(options.OutputFile);

            // Determine whether we need to create a backup file.
            if (options.CreateFileBackup)
            {
                // Addition to base filename for backup
                backupDate = DateTime.Now.ToString("_yyyyMMdd-Hmss");
                backupFilename = options.OutputFile;
                ext = Path.GetExtension(options.OutputFile);

                if (!String.IsNullOrWhiteSpace(ext))
                {
                    // File has extension, backup will look like "originalFile_yyyymmdd-hhmmss.ext"
                    backupFilename = backupFilename.Substring(0, backupFilename.LastIndexOf(ext)) + backupDate + ext;
                }
                else
                {
                    // File doesn't have an extension, backup will look like "originalFile_yyyymmdd"
                    backupFilename += backupDate;
                }

                // Copy the new output file to the backup location.
                outputFile = new FileInfo(options.OutputFile);
                outputFile.CopyTo(backupFilename, true);
            }

        }

        #endregion Methods
    }
}
