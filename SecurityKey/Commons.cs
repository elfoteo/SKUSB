using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SKUSB
{
    public class Commons
    {
        public static void LockFile(string path)
        {
            File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.System); // Set file attributes to hidden and system
        }

        public static void UnlockFile(string path)
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        public static string GetSecurityKeyFile(string drive)
        {
            return Path.Combine(drive, "security_key.dat");
        }

        public static bool IsSecurityKey(string drive)
        {
            return File.Exists(GetSecurityKeyFile(drive));
        }

        public static byte[] ReadSecurityKeyData(string drive)
        {
            return File.ReadAllBytes(GetSecurityKeyFile(drive));
        }
        
        public static string GetIdFromKeyData(byte[] keyData)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(keyData);
                string hashedString = BitConverter.ToString(hash);
                return hashedString;
            }
        }
        public static string ByteArrayToHex(byte[] byteArray)
        {
            // Convert to hex string with hyphens
            return BitConverter.ToString(byteArray).Replace("-", "");
        }

        public static void RunCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                throw new ArgumentException("Command cannot be null or empty.", nameof(command));

            try
            {
                // Split the command into the executable and its arguments
                string executable = GetExecutableFromCommand(command);
                string arguments = GetArgumentsFromCommand(command);

                // Create a new process start info
                ProcessStartInfo processInfo = new ProcessStartInfo
                {
                    FileName = executable,      // The executable to run (e.g., python)
                    Arguments = arguments,      // The arguments (e.g., "C:\\path\\to\\script.py unlock")
                    RedirectStandardOutput = false, // Redirect output so we can capture it
                    RedirectStandardError = false,  // Redirect error output as well
                    UseShellExecute = false,    // This allows redirection of output
                    CreateNoWindow = false       // Don't show a console window for the process
                };

                // Start the process
                Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute command: {ex.Message}");
            }
        }

        // Helper method to extract the executable (first part of the command string)
        private static string GetExecutableFromCommand(string command)
        {
            // Assume the executable is the first word of the command
            int firstSpaceIndex = command.IndexOf(' ');
            return firstSpaceIndex > -1 ? command.Substring(0, firstSpaceIndex) : command;
        }

        // Helper method to extract arguments (everything after the executable)
        private static string GetArgumentsFromCommand(string command)
        {
            int firstSpaceIndex = command.IndexOf(' ');
            return firstSpaceIndex > -1 ? command.Substring(firstSpaceIndex + 1) : string.Empty;
        }
    }
}
