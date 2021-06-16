﻿using System.Linq;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;
using WixSharp.CommonTasks;
using WixSharp.Msiexec;

namespace WixSharp
{
    /// <summary>
    ///   <para>Allows the installer to display full UI for the "Uninstall" button in the Control Panel. </para>
    ///   <para>By default, The Windows Installer executes "Repair" and "Uninstall" actions in the basic UI mode. <br />
    /// The extension methods allow to enable the full UI for all maintenance operations. <br /></para>
    ///   <para>References:</para>
    ///   <list type="bullet">
    ///     <item>https://www.advancedinstaller.com/user-guide/qa-uninstall-change-button.html</item>
    ///   </list>
    /// </summary>
    public static class UninstallFullUI
    {
        /// <summary>
        /// Enables the full UI.
        /// </summary>
        /// <param name="project">The project.</param>
        public static void EnableUninstallFullUI(this Project project)
        {
            EnableUninstallFullUI(project, null);
        }

        /// <summary>
        /// Enables the full UI.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <param name="logFilePath">Path for MSI logs file</param>
        /// <param name="logSwitches">MSI logging switches</param>
        public static void EnableUninstallFullUI(this Project project, string logFilePath, MsiexecLogSwitches logSwitches)
        {
            EnableUninstallFullUI(project, null, logFilePath, logSwitches);
        }

        /// <summary>
        /// Enables the full UI.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <param name="displayIconPath">The location of the app icon in the ARP.</param>
        /// <param name="logFilePath">Path for MSI logs file</param>
        /// <param name="logSwitches">MSI logging switches</param>
        public static void EnableUninstallFullUI(this Project project, string displayIconPath,
            string logFilePath = null, MsiexecLogSwitches logSwitches = MsiexecLogSwitches.None)
        {
            if (displayIconPath != null)
            {
                // Add the DisplayIcon key to the Uninstall section
                project.AddRegValues(
                    new RegValue(new Id("WixSharp_RegValue_DisplayIcon"),
                        RegistryHive.LocalMachine,
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\[ProductCode]",
                        "DisplayIcon",
                        displayIconPath));
            }

            project.AddAction(new ElevatedManagedAction(
                new Id(nameof(WixSharp_EnableUninstallFullUI_Action)),
                WixSharp_EnableUninstallFullUI_Action,
                typeof(UninstallFullUI).Assembly.Location,
                Return.ignore,
                When.Before,
                Step.InstallFinalize,
                Condition.NOT_Installed)
            {
                UsesProperties =
                    $"MSIEXEC_LOG_COMMAND={MsiexecLogCommand.Generate(logFilePath, logSwitches)}",
            });

            project.WixSourceGenerated += doc =>
            {
                /*
                  <Component Id="...
                      <RegistryKey Root="...
                          <RegistryValue Id="WixSharp_RegValue_DisplayIcon..."
                 */

                var comp = doc.FindAll("RegistryValue")
                    .First(x => x.HasAttribute("Id", "WixSharp_RegValue_DisplayIcon"))
                    .Parent
                    .Parent;

                var compId = comp.Attribute("Id").Value;

                var features = doc.FindAll("Feature")
                    .Where(x => !x.FindAll("ComponentRef")
                        .Any(y => y.HasAttribute("Id", compId))).ToArray();

                features.ForEach(f => f.AddElement("ComponentRef", $"Id={compId}"));
            };
        }

        /// <summary>
        /// Internal UninstallFullUI action. It must be public for the DTF accessibility but it is not to be used by the user/developer.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <returns></returns>
        [CustomAction]
        public static ActionResult WixSharp_EnableUninstallFullUI_Action(Session session)
        {
            return session.HandleErrors(() =>
            {
                var productCode = session.Property("ProductCode");
                var logCommand = session.Property("MSIEXEC_LOG_COMMAND");

                var keyName = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{productCode}";

                using (var uninstallKey = Registry.LocalMachine.OpenSubKey(keyName, true))
                {
                    if (uninstallKey == null)
                    {
                        return;
                    }

                    uninstallKey.SetValue("ModifyPath", string.Empty);
                    uninstallKey.SetValue("UninstallString", $"MsiExec.exe /I{productCode} {logCommand}");
                    uninstallKey.SetValue("WindowsInstaller", 0);
                }
            });
        }
    }
}