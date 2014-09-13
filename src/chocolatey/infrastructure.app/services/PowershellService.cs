﻿namespace chocolatey.infrastructure.app.services
{
    using System;
    using System.IO;
    using System.Linq;
    using configuration;
    using filesystem;
    using infrastructure.commands;
    using logging;
    using results;

    public class PowershellService : IPowershellService
    {
        private readonly IFileSystem _fileSystem;

        public PowershellService(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public void install_noop(PackageResult packageResult)
        {
            var packageDirectory = packageResult.InstallLocation;
            var installScript = _fileSystem.get_files(packageDirectory, "chocolateyInstall.ps1", SearchOption.AllDirectories);
            if (installScript.Count != 0)
            {
                var chocoInstall = installScript.FirstOrDefault();

                this.Log().Info("Would have run '{0}':".format_with(chocoInstall));
                this.Log().Warn(_fileSystem.read_file(chocoInstall));
            }
        }

        public string wrap_command_with_module(string command)
        {
            var installerModules =_fileSystem.get_files(ApplicationParameters.InstallLocation, "chocolateyInstaller.psm1", SearchOption.AllDirectories);
            var installerModule = installerModules.FirstOrDefault();
            return "[System.Threading.Thread]::CurrentThread.CurrentCulture = '';[System.Threading.Thread]::CurrentThread.CurrentUICulture = ''; & import-module -name '{0}'; {1}".format_with(installerModule,command);
        }

        public void install(ChocolateyConfiguration configuration, PackageResult packageResult)
        {
            var packageDirectory = packageResult.InstallLocation;

            if (!_fileSystem.directory_exists(packageDirectory))
            {
                packageResult.Messages.Add(new ResultMessage(ResultType.Error, "Package install not found:'{0}'".format_with(packageDirectory)));
                return;
            }

            var installScript = _fileSystem.get_files(packageDirectory, "chocolateyInstall.ps1", SearchOption.AllDirectories);
            if (installScript.Count != 0)
            {
                var chocoInstall = installScript.FirstOrDefault();

                this.Log().Debug(ChocolateyLoggers.Important, "Contents of '{0}':".format_with(chocoInstall));
                this.Log().Debug(_fileSystem.read_file(chocoInstall));

                var failure = false;

                var package = packageResult.Package;
                Environment.SetEnvironmentVariable("chocolateyPackageName", package.Id);
                Environment.SetEnvironmentVariable("chocolateyPackageVersion", package.Version.to_string());
                Environment.SetEnvironmentVariable("chocolateyPackageFolder", ApplicationParameters.PackagesLocation);
                Environment.SetEnvironmentVariable("installerArguments", configuration.InstallArguments);
                Environment.SetEnvironmentVariable("chocolateyPackageParameters", configuration.PackageParameters);
                 if (configuration.ForceX86)
                { 
                    Environment.SetEnvironmentVariable("chocolateyForceX86","true");
                }
                if (configuration.OverrideArguments)
                {
                    Environment.SetEnvironmentVariable("chocolateyInstallOverride","true");
                }



                if (configuration.Debug)
                {
                    Environment.SetEnvironmentVariable("ChocolateyEnvironmentDebug","true");
                }
                //todo:if (configuration.NoOutput)
                //{
                //    Environment.SetEnvironmentVariable("ChocolateyEnvironmentQuiet","true");
                //}
               
                
              //$env:chocolateyInstallArguments = ''
              //$env:chocolateyInstallOverride = $null


                PowershellExecutor.execute(
                    wrap_command_with_module(installScript.FirstOrDefault()),
                    _fileSystem,
                    (s, e) =>
                        {
                            if (string.IsNullOrWhiteSpace(e.Data)) return;
                            this.Log().Info(() => " " + e.Data);
                        },
                    (s, e) =>
                        {
                            if (string.IsNullOrWhiteSpace(e.Data)) return;
                            failure = true;
                            this.Log().Error(() => " " + e.Data);
                        });

                if (failure)
                {
                    packageResult.Messages.Add(new ResultMessage(ResultType.Error, "Error while running '{0}'.{1} See log for details.".format_with(installScript.FirstOrDefault(), Environment.NewLine)));
                }
            }

            packageResult.Messages.Add(new ResultMessage(ResultType.Note, "Ran '{0}'".format_with(installScript)));
        }
    }
}