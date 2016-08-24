// 
//  Copyright (c) Microsoft Corporation. All rights reserved. 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  



namespace Microsoft.PackageManagement.PackageSourceListProvider
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.IO.Compression;
    using System.Threading;
    using Microsoft.PackageManagement.Provider.Utility;
    using Microsoft.PackageManagement.Internal;
    using System.Management.Automation;
    using ErrorCategory = PackageManagement.Internal.ErrorCategory;

    using System.Collections.Generic;

    internal static class  ZipPackageInstaller
    {
        static string SystemEnvironmentKey = @"HKLM:\System\CurrentControlSet\Control\Session Manager\Environment";
        static string UserEnvironmentKey = @"HKCU:\Environment";

        internal static void GetInstalledZipPackage(PackageJson package, PackageSourceListRequest request)
        {
            try
            {
                if (request.AddToPath.Value)
                {
                    request.Verbose(Resources.Messages.AddOrRemovePath, Constants.ProviderName, "AddToPath", "Install-Package");
                }
                if (request.RemoveFromPath.Value)
                {
                    request.Verbose(Resources.Messages.AddOrRemovePath, Constants.ProviderName, "RemoveFromPath", "Uninstall-Package");
                }

                string path = Path.Combine(package.Destination, package.Name, package.Version);
                if (Directory.Exists(path))
                {
                    if (Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Any())
                    {
                        string userSpecifiedProvider = request.GetOptionValue("ProviderName") ?? request.GetOptionValue("Provider");
                        var fp = PackageSourceListRequest.MakeFastPathComplex(package.Source, package.Name, (package.DisplayName ?? ""), package.Version, path, userSpecifiedProvider ?? "");

                        //the directory exists and contain files, we think the package has been installed.
                        request.YieldSoftwareIdentity(fp, package.Name, package.Version, package.VersionScheme, package.Summary, path, package.Name, path, path);
                    }
                }
            }
            catch (Exception e)
            {
                request.Debug(e.StackTrace);
            }
        }
        internal static bool InstallZipPackage(PackageJson package, string fastPath, PackageSourceListRequest request)
        {
            if (request.RemoveFromPath.Value) {
                request.Warning(Resources.Messages.AddOrRemovePath, Constants.ProviderName, "RemoveFromPath", "Uninstall-Package");
            }
            // download the exe package
            var providerType = package.Type.ToLowerInvariant();
            var file = Path.ChangeExtension(Path.GetTempFileName(), providerType);

            if (string.IsNullOrWhiteSpace(package.Destination))
            {
                request.Error(ErrorCategory.InvalidOperation, Constants.ProviderName, Resources.Messages.DestinationRequired);
                return false;
            }            

            WebDownloader.DownloadFile(package.Source, file, request, null);
            if (!File.Exists(file))
            {
                return false;
            }

            // validate the file
            if (!WebDownloader.VerifyHash(file, package,request))
            {          
                file.TryHardToDelete();
                return false;
            }

            if (!request.ShouldContinueWithUntrustedPackageSource(package.Name, package.Source))
            {
                request.Warning(Constants.Messages.UserDeclinedUntrustedPackageInstall, package.Name);
                file.TryHardToDelete();
                return false;
            }

            Timer timer = null;
            object timerLock = new object();
            bool cleanUp = false;

            ProgressTracker tracker = new ProgressTracker(request.StartProgress(0, "Installing Zip Package............"));
            double percent = tracker.StartPercent;

            Action cleanUpAction = () => {
                lock (timerLock)
                {
                    // check whether clean up is already done before or not
                    if (!cleanUp)
                    {
                        try
                        {
                            if (timer != null)
                            {
                                // stop timer
                                timer.Change(Timeout.Infinite, Timeout.Infinite);
                                timer.Dispose();
                                timer = null;
                            }
                        }
                        catch
                        {
                        }

                        cleanUp = true;
                    }
                }
            };

            // extracted folder
            string extractedFolder = string.Concat(file.GenerateTemporaryFilename());
            var versionFolder = "";
            try
            {
                timer = new Timer(_ =>
                {
                    percent += 0.025;
                    var progressPercent = tracker.ConvertPercentToProgress(percent);
                    if (progressPercent < 90)
                    {
                        request.Progress(tracker.ProgressID, (int)progressPercent, string.Format(CultureInfo.CurrentCulture, "Copying files ..."));
                    }
                    if (request.IsCanceled)
                    {
                        cleanUpAction();
                    }
                }, null, 0, 1000);

                //unzip the file
                ZipFile.ExtractToDirectory(file, extractedFolder);
                if (Directory.Exists(extractedFolder))
                {
                    versionFolder = Path.Combine(package.Destination, package.Name, package.Version);
                    // create the directory version folder if not exist
                    if (!Directory.Exists(versionFolder))
                    {
                        Directory.CreateDirectory(versionFolder);
                    }

                    // The package will be installed to destination\packageName\version\
                    // However, a few packages have a package name as its top level folder after zip. 
                    // So the installed folder will look like this:
                    // \destination\foobarPackage\1.0.1\foobarPackage
                    // In this case we directly copy the files to \destination\foobarPackage\1.0.1.

                    
                    var extractedTopLevelFolder = Directory.EnumerateDirectories(extractedFolder, "*", SearchOption.TopDirectoryOnly);

                    while (!Directory.GetFiles(extractedFolder).Any() && extractedTopLevelFolder.Count() == 1) {

                        extractedFolder = extractedTopLevelFolder.FirstOrDefault();

                        //in case the zip contains version folder
                        extractedTopLevelFolder = Directory.EnumerateDirectories(extractedFolder, "*", SearchOption.TopDirectoryOnly);
                    }
                            
                    FileUtility.CopyDirectory(extractedFolder, versionFolder, true);
                    request.YieldFromSwidtag(package, fastPath);
                    request.Verbose(Resources.Messages.SuccessfullyInstalledToDestination, package.Name, package.Destination);

                    AddEnvironmentVariable(request, versionFolder);
                    return true;
                }
                else
                {
                    request.Warning("Failed to download a Zip package {0} from {1}", package.Name, package.Source);
                }
            }
            catch (Exception e)
            {
                request.Debug(e.StackTrace);
                request.WriteError(ErrorCategory.InvalidOperation, package.Name, Resources.Messages.InstallFailed, package.Name, e.Message);                
                if (!(e is UnauthorizedAccessException || e is IOException))
                {
                    // something wrong, delete the version folder
                    versionFolder.TryHardToDelete();
                }
            }
            finally
            {
                cleanUpAction();
                file.TryHardToDelete();
                extractedFolder.TryHardToDelete();
                request.CompleteProgress(tracker.ProgressID, true);
            }            
            return false;
        }

        internal static bool UnInstallZipPackage(PackageJson package, PackageSourceListRequest request, string fastPath)
        {
            string sourceLocation;
            string id;
            string displayName;
            string version;
            string path;
            string providerName;

            if (!request.TryParseFastPathComplex(fastPath: fastPath, regex: PackageSourceListRequest.RegexFastPathComplex, location: out sourceLocation, id: out id, displayname: out displayName, version: out version, fastpath: out path, providerName: out providerName))
            {
                request.Error(ErrorCategory.InvalidOperation, "package", Constants.Messages.UnableToUninstallPackage);
                return false;
            }

            if (request.AddToPath.Value) {
                request.Warning(Resources.Messages.AddOrRemovePath, Constants.ProviderName, "AddToPath", "Install-Package");
            }

            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                path.TryHardToDelete();

                var dir = Path.Combine(package.Destination, package.Name);
             
                // delete an empty directory
                if (Directory.Exists(dir) && (!Directory.GetDirectories(dir).Any()) && !(Directory.GetFiles(dir).Any())) {
                    dir.TryHardToDelete();
                }

                request.YieldFromSwidtag(package, path);

                RemoveEnvironmentVariable(request, path, SystemEnvironmentKey);
                RemoveEnvironmentVariable(request, path, UserEnvironmentKey);
            
                return true;
            }
            else
            {
                request.WriteError(ErrorCategory.InvalidData, path, Resources.Messages.DirectoryNotExist, Constants.ProviderName, path);
                return false;
            }
        }

        private static string GetItemProperty(string itemName, string path)
        {
            using (PowerShell powershell = PowerShell.Create())
            {
                if (powershell != null)
                {
                    var val =  powershell
                        .AddCommand("Get-ItemProperty")
                        .AddParameter(Constants.PATH, path)
                        .AddParameter("Name", itemName)
                        .Invoke<PSObject>().FirstOrDefault();
                    if (val != null)
                    {
                        var pathValue = val.Properties[Constants.PATH];
                        if (pathValue != null)
                        {
                            return pathValue.Value.ToStringSafe();
                        }
                    }

                }
            }
            return null;           
        }

        private static IEnumerable<PSObject> SetItemProperty(string itemName, string itemValue, string path)
        {
            using (PowerShell powershell = PowerShell.Create())
            {
                if (powershell != null)
                {
                    return powershell
                        .AddCommand("Set-ItemProperty")
                        .AddParameter(Constants.PATH, path)
                        .AddParameter("Name", itemName)
                        .AddParameter("Value", itemValue)
                        .Invoke<PSObject>();
                }
            }

            return Enumerable.Empty<PSObject>();
        }

        private static void AddEnvironmentVariable(PackageSourceListRequest request, string pathToBeAdded)
        {
            // Do nothing if a user does not specify -AddToPath
            if (!request.AddToPath.Value) {
                return;
            }

            try
            {
               var  scope = ((request.Scope == null) || string.IsNullOrWhiteSpace(request.Scope.Value)) ? Constants.AllUsers : request.Scope.Value;

                // check if the environment variable exists already
                var envPath = Environment.GetEnvironmentVariable(Constants.PATH);
                var packagePath = ";" + pathToBeAdded.Trim();

                if (string.IsNullOrWhiteSpace(envPath) || !envPath.Split(';').Where(each => each.EqualsIgnoreCase(pathToBeAdded)).Any())
                {
                    request.Debug("Adding '{0}' to PATH environment variable".format(pathToBeAdded));

                    // allow to add the path to the environment variable
                    switch (scope)
                    {
                        case Constants.AllUsers:
                            SetItemProperty(Constants.PATH, envPath + packagePath, SystemEnvironmentKey);
                            break;
                        case Constants.CurrentUser:
                            SetItemProperty(Constants.PATH, envPath + packagePath, UserEnvironmentKey);
                            break;
                    }
                    Environment.SetEnvironmentVariable(Constants.PATH, envPath + packagePath);
                }
                else
                {
                    request.Debug("Environment variable '{0}' already exists".format(pathToBeAdded));
                }
            }
            //if we cannot set the environment variable, we should not fail the install-package
            catch (Exception e)
            {
                request.Warning("Failed to update Path environment variable. {0}", e.Message);
                e.Dump(request);
            }
        }
        private static void RemoveEnvironmentVariable(PackageSourceListRequest request, string pathToBeRemoved, string target)
        {
            try
            {
                if (!request.RemoveFromPath.Value) {
                    return;
                }

                var envPath = GetItemProperty(Constants.PATH, target);    
                var trimchars = new char[] { '\\', ' '};

                if (!string.IsNullOrWhiteSpace(envPath))
                {
                    pathToBeRemoved = pathToBeRemoved.TrimEnd(trimchars);

                    var newPath = envPath.Split(';').Where(each => !each.TrimEnd(trimchars).EqualsIgnoreCase(pathToBeRemoved))
                        .Aggregate("", (current, each) => string.IsNullOrEmpty(current) ? each : (current + ";" + each));

                    request.Debug("Removing '{0}' from PATH environment variable".format(pathToBeRemoved));

                    SetItemProperty(Constants.PATH, newPath, UserEnvironmentKey);
                    SetItemProperty(Constants.PATH, newPath, SystemEnvironmentKey);

                    Environment.SetEnvironmentVariable(Constants.PATH, newPath);                    
                }
                else
                {
                    request.Warning(Resources.Messages.FailedToRetrivePathEnvironmentVariable, Constants.ProviderName);
                }
            }
            catch (Exception e)
            {
                request.Warning("Failed to update Path environment variable. {0}", e.Message);
                e.Dump(request);
            }
        }

        internal static void DownloadZipPackage(string fastPath, string location, PackageSourceListRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            request.Debug(Resources.Messages.DebugInfoCallMethod, Constants.ProviderName,
                string.Format(CultureInfo.InvariantCulture, "DownloadZipPackage' - location='{0}'", location));
           
        }
    }
}