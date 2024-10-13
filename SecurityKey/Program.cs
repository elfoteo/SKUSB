using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SKUSB
{
    class Program
    {
        public static Dictionary<string, byte[]> PluggedSecurityKeys = new Dictionary<string, byte[]>();

        private static NotifyIcon notifyIcon;
        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient, IntPtr notificationFilter, uint flags);

        private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;
        private const int DBT_DEVTYP_DEVICEINTERFACE = 0x00000005;

        [StructLayout(LayoutKind.Sequential)]
        public struct DEV_BROADCAST_HDR
        {
            public int dbch_size;
            public int dbch_devicetype;
            public int dbch_reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;
            public int dbcc_devicetype;
            public int dbcc_reserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public char[] dbcc_name;
        }

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ConfigManager.LoadConfigurations();
            // Initialize the NotifyIcon
            notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application, // Change this to your icon file if needed
                ContextMenuStrip = CreateContextMenu(),
                Visible = true
            };

            // Create a hidden window to receive device notifications
            NativeWindowWithNotifications window = new NativeWindowWithNotifications();
            window.CreateHandle(new CreateParams());

            // Register to receive device notifications
            RegisterForDeviceNotifications(window.Handle);

            window.DeviceArrived += (sender, deviceName) => { OnDeviceChange(true, deviceName); };
            window.DeviceRemoved += (sender, deviceName) => { OnDeviceChange(false, deviceName); };

            foreach (string key in GetPluggedKeys())
            {
                OnDeviceChange(true, key);
            }

            // Show initial balloon tooltip if notifications are allowed
            if (ConfigManager.GlobalConfig.AllowToastNotifications)
            {
                notifyIcon.ShowBalloonTip(1000, "Security Key Manager", "Running in the system tray", ToolTipIcon.Info);
            }

            // Run the application (system tray)
            Application.Run();

            // Clean up on exit
            notifyIcon.Visible = false;
        }

        // Registers the application to receive device change notifications
        private static void RegisterForDeviceNotifications(IntPtr windowHandle)
        {
            DEV_BROADCAST_DEVICEINTERFACE dbdi = new DEV_BROADCAST_DEVICEINTERFACE
            {
                dbcc_size = Marshal.SizeOf(typeof(DEV_BROADCAST_DEVICEINTERFACE)),
                dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE
            };

            IntPtr buffer = Marshal.AllocHGlobal(dbdi.dbcc_size);
            Marshal.StructureToPtr(dbdi, buffer, true);

            RegisterDeviceNotification(windowHandle, buffer, DEVICE_NOTIFY_WINDOW_HANDLE);
        }

        private static ContextMenuStrip CreateContextMenu()
        {
            var contextMenu = new ContextMenuStrip();

            // Create menu items
            contextMenu.Items.Add("Create a Security Key", null, (s, e) => { new SecurityKeyForm().ShowDialog(); });
            contextMenu.Items.Add("Configure Keys", null, (s, e) => { new SecurityKeyConfigurationForm().ShowDialog(); });

            // Add a dynamic list of security keys
            var pluggedKeys = GetPluggedKeys();
            if (pluggedKeys.Length > 0)
            {
                ToolStripMenuItem keysMenuItem = new ToolStripMenuItem("Connected Security Keys");
                foreach (var key in pluggedKeys)
                {
                    // Add keys as non-clickable items
                    ToolStripMenuItem keyItem = new ToolStripMenuItem(key)
                    {
                        Enabled = false // Disable the item to make it non-clickable
                    };
                    keysMenuItem.DropDownItems.Add(keyItem);
                }
                contextMenu.Items.Add(keysMenuItem);
            }
            else
            {
                contextMenu.Items.Add("No Security Keys Found");
            }

            // Toggleable "Allow Notifications" menu item
            ToolStripMenuItem allowNotificationsMenuItem = new ToolStripMenuItem("Allow Notifications")
            {
                Checked = ConfigManager.GlobalConfig.AllowToastNotifications // Set the initial state
            };
            allowNotificationsMenuItem.Click += (s, e) =>
            {
                // Toggle the boolean value
                ConfigManager.GlobalConfig.AllowToastNotifications = !ConfigManager.GlobalConfig.AllowToastNotifications;

                // Update the checkmark
                allowNotificationsMenuItem.Checked = ConfigManager.GlobalConfig.AllowToastNotifications;
                ConfigManager.SaveConfigurations();
            };

            // Add the toggleable item to the menu
            contextMenu.Items.Add(allowNotificationsMenuItem);

            contextMenu.Items.Add("Exit", null, (s, e) => { Application.Exit(); Environment.Exit(0); });

            return contextMenu;
        }

        static string[] GetPluggedKeys()
        {
            // Get all removable drives that have a 'security_key.dat' file
            DriveInfo[] drivesWithKeys = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
                .Where(d => File.Exists(Path.Combine(d.Name, "security_key.dat"))) // Only drives with 'security_key.dat'
                .ToArray();
            // Return the drive names
            return drivesWithKeys.Select(d => d.Name).ToArray();
        }

        // Handles the device arrival and removal events
        public static void OnDeviceChange(bool isInserted, string driveLetter)
        {
            if (driveLetter.Contains("Unknown"))
            {
                if (ConfigManager.GlobalConfig.AllowToastNotifications)
                {
                    notifyIcon.ShowBalloonTip(1000, "Security Key", $"Plugged/Unplugged Unknown device ({driveLetter})", ToolTipIcon.Warning);
                }
                return;
            }
            
            // Sanitize drive letter
            if (isInserted)
            {
                string secureKeyFile = Commons.GetSecurityKeyFile(driveLetter);
                // Check if the plugged-in drive is ready and contains 'security_key.dat'
                if (Directory.Exists(driveLetter) && File.Exists(secureKeyFile))
                {
                    // Ensure secure key file is locked
                    Commons.LockFile(secureKeyFile);
                    
                    // Read from the drive the security key data
                    byte[] keyData = Commons.ReadSecurityKeyData(driveLetter);
                    // Add to the plugged security keys the current key and the security key data
                    if (PluggedSecurityKeys.ContainsKey(driveLetter))
                    {
                        PluggedSecurityKeys.Remove(driveLetter);
                    }
                    PluggedSecurityKeys.Add(driveLetter, keyData);
                    // Run the security key action if any
                    string keyId = Commons.GetIdFromKeyData(keyData);
                    KeyConfiguration keyConfig = ConfigManager.FindConfigurationById(keyId);
                    if (!string.IsNullOrEmpty(keyConfig.CommandOnPlug))
                    {
                        Commons.RunCommand(keyConfig.CommandOnPlug.Replace("%s", Commons.ByteArrayToHex(keyData)));
                    }
                    else
                    {
                        Console.WriteLine($"Security key {driveLetter} plugged but no action configured");
                    }
                    // Notify user about the plugged-in security key
                    if (ConfigManager.GlobalConfig.AllowToastNotifications)
                    {
                        notifyIcon.ShowBalloonTip(1000, "Security Key Detected", $"Security Key plugged in: {driveLetter}", ToolTipIcon.Info);
                    }
                }
                else
                {
                    Console.WriteLine($"No security key found on drive: {driveLetter}");
                }
            }
            else
            {
                byte[] keyData;
                PluggedSecurityKeys.TryGetValue(driveLetter, out keyData);
                if (keyData != null)
                {
                    PluggedSecurityKeys.Remove(driveLetter);
                    string keyId = Commons.GetIdFromKeyData(keyData);
                    KeyConfiguration keyConfig = ConfigManager.FindConfigurationById(keyId);
                    if (!string.IsNullOrEmpty(keyConfig.CommandOnUnplug))
                    {
                        Commons.RunCommand(keyConfig.CommandOnUnplug.Replace("%s", Commons.ByteArrayToHex(keyData)));
                    }
                    else
                    {
                        Console.WriteLine($"Security key {driveLetter} unplugged but no action configured");
                    }
                }
                else
                {
                    Console.WriteLine("Security key has no data");
                }

                // Notify the user that the drive was removed
                if (ConfigManager.GlobalConfig.AllowToastNotifications)
                {
                    notifyIcon.ShowBalloonTip(1000, "Security Key Removed", $"Security Key unplugged from drive: {driveLetter}", ToolTipIcon.Info);
                }
            }
            notifyIcon.ContextMenuStrip = CreateContextMenu();
        }
    }

    // NativeWindow subclass to handle window messages
    public class NativeWindowWithNotifications : NativeWindow
    {
        private const int WM_DEVICECHANGE = 0x0219;
        private const int DBT_DEVICEARRIVAL = 0x8000;
        private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

        private const int DBT_DEVTYP_VOLUME = 0x00000002;  // For volume devices (e.g., USB mass storage)

        [StructLayout(LayoutKind.Sequential)]
        public struct DEV_BROADCAST_HDR
        {
            public int dbch_size;
            public int dbch_devicetype;
            public int dbch_reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEV_BROADCAST_VOLUME
        {
            public int dbcv_size;
            public int dbcv_devicetype;
            public int dbcv_reserved;
            public int dbcv_unitmask;    // Bitmask of drive letters
            public short dbcv_flags;
        }

        // Define the events for device arrival and removal
        public event EventHandler<string> DeviceArrived;
        public event EventHandler<string> DeviceRemoved;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_DEVICECHANGE)
            {
                if (m.WParam.ToInt32() == DBT_DEVICEARRIVAL)
                {
                    // A device was plugged in
                    string deviceName = GetDeviceName(m.LParam);
                    Console.WriteLine($"USB pendrive plugged in: {deviceName}");

                    // Raise the event for device arrival
                    new Thread(
                        () => DeviceArrived?.Invoke(this, deviceName)
                    ).Start();
                }
                else if (m.WParam.ToInt32() == DBT_DEVICEREMOVECOMPLETE)
                {
                    // A device was unplugged
                    string deviceName = GetDeviceName(m.LParam);
                    Console.WriteLine($"USB pendrive unplugged: {deviceName}");

                    // Raise the event for device removal
                    new Thread(
                        () => DeviceRemoved?.Invoke(this, deviceName)
                    ).Start();
                }
            }

            base.WndProc(ref m);
        }

        // Method to retrieve the drive letter from the lParam of the WM_DEVICECHANGE message
        private string GetDeviceName(IntPtr lParam)
        {
            var hdr = (DEV_BROADCAST_HDR)Marshal.PtrToStructure(lParam, typeof(DEV_BROADCAST_HDR));

            if (hdr.dbch_devicetype == DBT_DEVTYP_VOLUME)
            {
                var vol = (DEV_BROADCAST_VOLUME)Marshal.PtrToStructure(lParam, typeof(DEV_BROADCAST_VOLUME));
                char driveLetter = GetDriveLetterFromMask(vol.dbcv_unitmask);
                return driveLetter != '\0' ? $"{driveLetter}:\\" : "Unknown USB drive";
            }

            return "Unknown device";
        }

        // Convert the unitmask to a drive letter
        private char GetDriveLetterFromMask(int unitmask)
        {
            for (int i = 0; i < 26; i++)
            {
                if ((unitmask & (1 << i)) != 0)
                {
                    return (char)('A' + i);
                }
            }
            return '\0'; // No valid drive letter found
        }
    }
}
