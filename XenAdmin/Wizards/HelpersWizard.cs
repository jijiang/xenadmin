/* Copyright (c) Citrix Systems, Inc. 
 * All rights reserved. 
 * 
 * Redistribution and use in source and binary forms, 
 * with or without modification, are permitted provided 
 * that the following conditions are met: 
 * 
 * *   Redistributions of source code must retain the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer. 
 * *   Redistributions in binary form must reproduce the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer in the documentation and/or other 
 *     materials provided with the distribution. 
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
 * SUCH DAMAGE.
 */

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using DiscUtils.Iso9660;
using XenAdmin.Actions;
using XenAdmin.Alerts;
using XenAdmin.Controls;
using XenAdmin.Core;
using XenAdmin.Dialogs;
using XenAdmin.Wizards.PatchingWizard;
using XenAdmin.Wizards.RollingUpgradeWizard;


namespace XenAdmin.Wizards
{
    public static class HelpersWizard
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static void AddSuppPackFromDisk(XenTabPage page)
        {
            String oldDir = String.Empty;
            try
            {
                oldDir = Directory.GetCurrentDirectory();
                using (OpenFileDialog dlg = new OpenFileDialog
                {
                    Multiselect = false,
                    ShowReadOnly = false,
                    Filter = string.Format(Messages.PATCHINGWIZARD_SELECTPATCHPAGE_UPDATESEXT, Branding.Update),
                    FilterIndex = 0,
                    CheckFileExists = true,
                    ShowHelp = false,
                    Title = Messages.PATCHINGWIZARD_SELECTPATCHPAGE_CHOOSE
                })
                {
                    if (dlg.ShowDialog(page) == DialogResult.OK && dlg.CheckFileExists)
                        AddFile(dlg.FileName, page);
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(oldDir);
            }
        }

        public static void AddFile(string fileName, XenTabPage page)
        {
            if (isValidFile(fileName))
            {
                var patchingWizardPage = page as PatchingWizard_SelectPatchPage;
                if (patchingWizardPage != null)
                    patchingWizardPage.FilePath = fileName;
                else
                {
                    var rpuWizardAutoPage = page as RollingUpgradeWizardInstallMethodPage;
                    if (rpuWizardAutoPage != null)
                        rpuWizardAutoPage.FilePath = fileName;
                    else
                    {
                        var rpuWizardManualPage = page as RollingUpgradeReadyToUpgradePage;
                        if (rpuWizardManualPage != null)
                            rpuWizardManualPage.FilePath = fileName;
                        else
                        {
                            log.ErrorFormat("Error adding an update or supp pack file from disk on page {0}", page.Text);
                            throw new Exception("Exception of adding a disk file.");
                        }
                    }
                }
            }
            else
            {
                using (var dlg = new ThreeButtonDialog(new ThreeButtonDialog.Details(
                        SystemIcons.Error, string.Format(Messages.UPDATES_WIZARD_NOTVALID_EXTENSION, Branding.Update), Messages.UPDATES)))
                {
                    dlg.ShowDialog(page);
                }
            }
        }

        public static void ParseSuppPackFile(string path, string unzippedPath, XenTabPage page, ref bool cancel, out string suppPackPath)
        {
            if (Path.GetExtension(path).ToLowerInvariant().Equals(".zip") &&
                    Path.GetFileNameWithoutExtension(unzippedPath) !=
                    Path.GetFileNameWithoutExtension(path))
            {
                unzippedPath = ExtractUpdate(path, page);
                if (unzippedPath == null)
                    cancel = true;
            }
            else
                unzippedPath = null;

            var fileName = isValidFile(unzippedPath)
                ? unzippedPath.ToLowerInvariant()
                : path.ToLowerInvariant();

            if (isValidFile(fileName))
            {
                if (!fileName.EndsWith("." + Branding.Update)
                    && !fileName.EndsWith("." + Branding.UpdateIso)
                    && !cancel)
                {
                    using (var dlg = new ThreeButtonDialog(new ThreeButtonDialog.Details(
                        SystemIcons.Error,
                        string.Format(Messages.UPDATES_WIZARD_NOTVALID_ZIPFILE, Path.GetFileName(fileName)),
                        Messages.UPDATES)))
                    {
                        dlg.ShowDialog(page);
                    }
                    cancel = true;
                }
                suppPackPath = fileName;
            }
            else
                suppPackPath = string.Empty;
        }

        public static string ExtractUpdate(string zippedUpdatePath, XenTabPage page)
        {
            var unzipAction =
                new DownloadAndUnzipXenServerPatchAction(Path.GetFileNameWithoutExtension(zippedUpdatePath), null,
                    zippedUpdatePath, true, Branding.Update, Branding.UpdateIso);
            using (var dlg = new ActionProgressDialog(unzipAction, ProgressBarStyle.Marquee))
            {
                dlg.ShowDialog(page.Parent);
            }

            if (string.IsNullOrEmpty(unzipAction.PatchPath))
            {
                using (var dlg = new ThreeButtonDialog(new ThreeButtonDialog.Details(
                    SystemIcons.Error,
                    string.Format(Messages.UPDATES_WIZARD_NOTVALID_ZIPFILE, Path.GetFileName(zippedUpdatePath)),
                    Messages.UPDATES)))
                {
                    dlg.ShowDialog(page);
                }
                return null;
            }
            else
            {
                return unzipAction.PatchPath;
            }
        }

        public static bool isValidFile(string fileName)
        {
            return !string.IsNullOrEmpty(fileName) && File.Exists(fileName) && (fileName.ToLowerInvariant().EndsWith(UpdateExtension.ToLowerInvariant())
                || fileName.ToLowerInvariant().EndsWith(".zip")
                || fileName.ToLowerInvariant().EndsWith(".iso")); //this iso is supplemental pack iso for XS, not branded
        }

        private static string UpdateExtension
        {
            get { return "." + Branding.Update; }
        }
    }
}
