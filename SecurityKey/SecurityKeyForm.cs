using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace SKUSB
{
    public class SecurityKeyForm : Form
    {
        private ListBox driveListBox;
        private Button confirmButton;
        private Button cancelButton;
        private Label statusLabel;
        private Label instructionLabel;

        public SecurityKeyForm()
        {
            // Initialize Form components
            this.Text = "Create Security Key";
            this.Size = new Size(400, 250);

            // TableLayoutPanel for better layout
            var tableLayoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = true,
                Padding = new Padding(10)
            };

            // Instruction Label
            instructionLabel = new Label
            {
                Text = "Select a drive to create a security key file.",
                Dock = DockStyle.Top,
                AutoSize = true
            };
            tableLayoutPanel.Controls.Add(instructionLabel, 0, 0);

            // Drive List
            driveListBox = new ListBox { Dock = DockStyle.Fill, Height = 100 };
            tableLayoutPanel.Controls.Add(driveListBox, 0, 1);

            // Buttons
            confirmButton = new Button { Text = "Confirm", Dock = DockStyle.Bottom };
            confirmButton.Click += ConfirmButton_Click;
            tableLayoutPanel.Controls.Add(confirmButton, 0, 2);

            cancelButton = new Button { Text = "Cancel", Dock = DockStyle.Bottom };
            cancelButton.Click += (s, e) => this.Close();
            tableLayoutPanel.Controls.Add(cancelButton, 0, 3);

            // Status Label
            statusLabel = new Label { Dock = DockStyle.Bottom, Height = 40, AutoSize = true, ForeColor = Color.Red };
            tableLayoutPanel.Controls.Add(statusLabel, 0, 4);

            // Add the TableLayoutPanel to the Form
            Controls.Add(tableLayoutPanel);

            LoadAvailableDrives();
        }

        private void LoadAvailableDrives()
        {
            if (!this.IsHandleCreated)
            {
                this.CreateHandle();
            }

            statusLabel.Text = "Please insert your USB thumb drive...";

            ThreadPool.QueueUserWorkItem(_ =>
            {
                DriveInfo[] initialDrives = DriveInfo.GetDrives()
                    .Where(d => d.DriveType == DriveType.Removable && d.IsReady).ToArray();

                DriveInfo[] drivesInUI = new DriveInfo[0]; // Tracks drives shown in the UI

                while (IsHandleCreated)
                {
                    // Get the current list of removable drives
                    DriveInfo[] currentDrives = DriveInfo.GetDrives()
                        .Where(d => d.DriveType == DriveType.Removable && d.IsReady).ToArray();

                    // Drives that are new (present in currentDrives but not in drivesInUI)
                    var newDrives = currentDrives
                        .Where(cd => !drivesInUI.Any(uiDrive => uiDrive.Name.Equals(cd.Name, StringComparison.OrdinalIgnoreCase)))
                        .ToArray();

                    // Drives that are removed (present in drivesInUI but no longer in currentDrives)
                    var removedDrives = drivesInUI
                        .Where(uiDrive => !currentDrives.Any(cd => cd.Name.Equals(uiDrive.Name, StringComparison.OrdinalIgnoreCase)))
                        .ToArray();

                    if (newDrives.Length > 0 || removedDrives.Length > 0)
                    {
                        // Update the list box on the UI thread
                        Invoke(new Action(() =>
                        {
                            driveListBox.Items.Clear();

                            // Add the currently connected drives to the list
                            foreach (var drive in currentDrives)
                            {
                                driveListBox.Items.Add($"{drive.Name} (Label: {drive.VolumeLabel}, Size: {drive.TotalSize / (1024 * 1024)} MB)");
                            }

                            // Update the status label based on drive changes
                            if (newDrives.Length > 0)
                            {
                                statusLabel.Text = $"{newDrives.Length} new USB drive(s) detected!";
                                statusLabel.ForeColor = Color.Green;
                            }
                            else if (removedDrives.Length > 0)
                            {
                                statusLabel.Text = $"{removedDrives.Length} USB drive(s) removed!";
                                statusLabel.ForeColor = Color.Red;
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                            }
                            else
                            {
                                statusLabel.Text = "Please insert your USB thumb drive...";
                                statusLabel.ForeColor = Color.Black;
                            }
                        }));

                        // Update the drivesInUI to reflect the current state
                        drivesInUI = currentDrives;
                    }

                    Thread.Sleep(1000); // Wait before checking again
                }
            });
        }

        private void ConfirmButton_Click(object sender, EventArgs e)
        {
            if (driveListBox.SelectedItem == null)
            {
                statusLabel.Text = "Please select a drive.";
                statusLabel.ForeColor = Color.Red;
                return;
            }

            string selectedDrive = driveListBox.SelectedItem.ToString().Split(' ')[0];
            string filePath = Commons.GetSecurityKeyFile(selectedDrive);

            if (File.Exists(filePath))
            {
                DialogResult result = MessageBox.Show("A key is already present on this drive, it will get overwritten.\nAre you sure you want to continue?", "Overwrite Security Key", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly | MessageBoxOptions.ServiceNotification);
                if (result == DialogResult.No)
                {
                    statusLabel.Text = "Operation Cancelled.";
                    statusLabel.ForeColor = Color.DarkOrange;
                    return;
                }
                else
                {
                    Commons.UnlockFile(filePath);
                }
            }

            try
            {
                // Generate a simple security key (e.g., random 512-byte data)
                byte[] securityKey = new byte[512];
                new Random().NextBytes(securityKey);

                // Save the key to a file on the USB drive
                File.WriteAllBytes(filePath, securityKey);
                Commons.LockFile(filePath);

                statusLabel.Text = "Security key created and saved successfully.";
                statusLabel.ForeColor = Color.Green;
            }
            catch (UnauthorizedAccessException ex)
            {
                statusLabel.Text = $"Error: Unauthorized access to the drive. {ex.Message}";
            }
            catch (IOException ex)
            {
                statusLabel.Text = $"I/O Error: Could not write to the drive. {ex.Message}";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"An unexpected error occurred: {ex.Message}";
            }
        }
    }
}
