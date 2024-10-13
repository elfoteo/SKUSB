using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace SKUSB
{
    public class SecurityKeyConfigurationForm : Form
    {
        private ListBox driveListBox;
        private Button confirmButton;
        private Button cancelButton;
        private TextBox nameTextBox;
        private TextBox commandOnPlugTextBox;
        private TextBox commandOnUnplugTextBox;
        private Label statusLabel;
        private Label legendLabel; // New label for the color legend

        public SecurityKeyConfigurationForm()
        {
            // Load configurations from file
            ConfigManager.LoadConfigurations();

            // Initialize Form components
            this.Text = "Configure Security Key";
            this.Size = new Size(400, 450);

            var tableLayoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                AutoSize = true,
                Padding = new Padding(10)
            };

            // Drive List
            driveListBox = new ListBox { Dock = DockStyle.Fill, Height = 100 };
            driveListBox.DrawMode = DrawMode.OwnerDrawFixed;  // Enable custom drawing
            driveListBox.DrawItem += DriveListBox_DrawItem;   // Hook into draw event
            driveListBox.SelectedIndexChanged += DriveListBox_SelectedIndexChanged;
            tableLayoutPanel.Controls.Add(new Label { Text = "Available Drives:", Dock = DockStyle.Top, AutoSize = true }, 0, 0);
            tableLayoutPanel.Controls.Add(driveListBox, 0, 1);

            // Status Label
            statusLabel = new Label { Dock = DockStyle.Bottom, Height = 40, AutoSize = true, ForeColor = Color.Red };
            tableLayoutPanel.Controls.Add(statusLabel, 0, 2);

            // Name TextBox
            nameTextBox = new TextBox { Dock = DockStyle.Fill };
            tableLayoutPanel.Controls.Add(new Label { Text = "Key Name:", Dock = DockStyle.Top, AutoSize = true }, 0, 3);
            tableLayoutPanel.Controls.Add(nameTextBox, 0, 4);

            // Command on Plug
            commandOnPlugTextBox = new TextBox { Dock = DockStyle.Fill };
            tableLayoutPanel.Controls.Add(new Label { Text = "Command on Plug: (%s will be replaced with the drive secure key)", Dock = DockStyle.Top, AutoSize = true }, 0, 5);
            tableLayoutPanel.Controls.Add(commandOnPlugTextBox, 0, 6);

            // Command on Unplug
            commandOnUnplugTextBox = new TextBox { Dock = DockStyle.Fill };
            tableLayoutPanel.Controls.Add(new Label { Text = "Command on Unplug: (%s will be replaced with the drive secure key)", Dock = DockStyle.Top, AutoSize = true }, 0, 7);
            tableLayoutPanel.Controls.Add(commandOnUnplugTextBox, 0, 8);

            // Buttons
            confirmButton = new Button { Text = "Save Configuration", Dock = DockStyle.Bottom };
            confirmButton.Click += ConfirmButton_Click;
            tableLayoutPanel.Controls.Add(confirmButton, 0, 9);

            cancelButton = new Button { Text = "Cancel", Dock = DockStyle.Bottom };
            cancelButton.Click += (s, e) => this.Close();
            tableLayoutPanel.Controls.Add(cancelButton, 0, 10);


            // Legend Label (New)
            legendLabel = new Label
            {
                Text = "Legend:\nGreen = Connected Security Key\nRed = Not Connected\nDark Orange = Not a Security Key",
                Dock = DockStyle.Bottom,
                AutoSize = true,
                ForeColor = Color.Black
            };
            tableLayoutPanel.Controls.Add(legendLabel, 0, 11);

            nameTextBox.Enabled = false;
            commandOnPlugTextBox.Enabled = false;
            commandOnUnplugTextBox.Enabled = false;
            confirmButton.Enabled = false;

            Controls.Add(tableLayoutPanel);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            LoadAvailableDrives();
        }

        // Custom drawing for listbox items to color-code connected/disconnected drives
        private void DriveListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();

            if (e.Index < 0)
                return;

            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
            {
                e = new DrawItemEventArgs(e.Graphics,
                                          e.Font,
                                          e.Bounds,
                                          e.Index,
                                          e.State ^ DrawItemState.Selected,
                                          Color.White,
                                          Color.Black); // Choose the color.

                // Draw the background of the ListBox control for each item.
                e.DrawBackground();
                e.Graphics.DrawString(driveListBox.Items[e.Index].ToString(), e.Font, new SolidBrush(e.ForeColor), e.Bounds, StringFormat.GenericDefault);

            }
            else
            {
                string itemText = driveListBox.Items[e.Index].ToString();
                var selectedDrive = itemText.Split(' ')[0];

                bool isSecurityKey = Commons.IsSecurityKey(selectedDrive);
                bool isConnected = DriveInfo.GetDrives().Any(d => d.IsReady && d.Name == selectedDrive);

                // Set colors based on key and connection status
                if (isSecurityKey && isConnected)
                {
                    e.Graphics.DrawString(itemText, e.Font, Brushes.Green, e.Bounds);
                }
                else if (!isConnected)
                {
                    e.Graphics.DrawString(itemText, e.Font, Brushes.Red, e.Bounds);
                }
                else
                {
                    e.Graphics.DrawString(itemText, e.Font, Brushes.DarkOrange, e.Bounds);
                }
            }

            


            e.DrawFocusRectangle();
        }

        private void LoadAvailableDrives()
        {
            if (!this.IsHandleCreated)
            {
                this.CreateHandle();
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                DriveInfo[] drivesInUI = new DriveInfo[0];
                bool firstUpdate = true;

                while (IsHandleCreated)
                {
                    // Get currently connected drives
                    DriveInfo[] currentDrives = DriveInfo.GetDrives()
                        .Where(d => d.DriveType == DriveType.Removable && d.IsReady).ToArray();

                    // Get all configured drives and find their matching physical drives (if connected)
                    var allConfiguredDrives = ConfigManager.KeyConfigurations
                        .Select(config => new
                        {
                            Config = config,
                            Drive = currentDrives.FirstOrDefault(d => Commons.IsSecurityKey(d.Name) &&
                                Commons.GetIdFromKeyData(Commons.ReadSecurityKeyData(d.Name)) == config.Id)
                        }).ToList();

                    // Find new and removed drives
                    var newDrives = currentDrives
                        .Where(cd => !drivesInUI.Any(uiDrive => uiDrive.Name.Equals(cd.Name, StringComparison.OrdinalIgnoreCase)))
                        .ToArray();

                    var removedDrives = drivesInUI
                        .Where(uiDrive => !currentDrives.Any(cd => cd.Name.Equals(uiDrive.Name, StringComparison.OrdinalIgnoreCase)))
                        .ToArray();

                    // Check if drives have changed (added/removed) or if this is the first update
                    bool drivesChanged = newDrives.Length > 0 || removedDrives.Length > 0 || firstUpdate;

                    if (drivesChanged)
                    {
                        firstUpdate = false;
                        drivesInUI = currentDrives; // Update the cached drive list

                        // Log connected and removed devices
                        if (newDrives.Length > 0 || removedDrives.Length > 0)
                        {
                            if (this.IsHandleCreated)
                            {
                                this.Invoke(new Action(() =>
                                {
                                    if (newDrives.Length > 0)
                                    {
                                        statusLabel.Text = $"{newDrives.Length} new USB drive(s) connected.";
                                        statusLabel.ForeColor = Color.Green;
                                    }

                                    if (removedDrives.Length > 0)
                                    {
                                        statusLabel.Text = $"{removedDrives.Length} USB drive(s) removed.";
                                        statusLabel.ForeColor = Color.Red;
                                    }
                                }));
                            }
                        }

                        // Update the UI only if necessary (when drives are added or removed)
                        if (this.IsHandleCreated)
                        {
                            this.Invoke(new Action(() =>
                            {
                                driveListBox.Items.Clear();
                                SecureKeyListItem sk;
                                // Display all currently connected drives, marking security keys in green and non-security drives in orange
                                foreach (var drive in currentDrives)
                                {
                                    sk = new SecureKeyListItem("", drive.Name, true, drive.TotalSize, drive.VolumeLabel);
                                    driveListBox.Items.Add(sk);
                                }

                                // Display configured drives that are not currently connected
                                foreach (var configDrive in allConfiguredDrives.Where(cd => cd.Drive == null))
                                {
                                    sk = new SecureKeyListItem(configDrive.Config.Id, configDrive.Config.Name, false, null, null);
                                    driveListBox.Items.Add(sk);
                                }
                            }));
                        }
                    }

                    // Pause for 1 second before the next check
                    Thread.Sleep(1000);
                }
            });
        }

        public class SecureKeyListItem
        {
            public string Id;
            public string DisplayName;
            public bool Connected;
            public long? TotalSize;
            public string VolumeLabel;

            public SecureKeyListItem(string id, string displayName, bool connected, long? totalSize, string volumeLabel)
            {
                Id = id;
                DisplayName = displayName;
                Connected = connected;
                TotalSize = totalSize;
                VolumeLabel = volumeLabel;
            }

            public override string ToString()
            {
                return Connected? $"{DisplayName} (Label: {VolumeLabel}, Size: {TotalSize / (1024 * 1024)} MB)" : $"Not Connected: {DisplayName} (ID: {Id})";
            }
        }

        private void ConfirmButton_Click(object sender, EventArgs e)
        {
            if (driveListBox.SelectedItem == null)
            {
                statusLabel.Text = "Please select a drive.";
                statusLabel.ForeColor = Color.Red;
                return;
            }

            SecureKeyListItem sk = (SecureKeyListItem)driveListBox.SelectedItem;
            string selectedDrive = sk.ToString().Split(' ')[0];
            var existingConfig = ConfigManager.FindConfigurationById(sk.Id);
            if (Commons.IsSecurityKey(selectedDrive) || (!string.IsNullOrEmpty(sk.Id) && existingConfig != null))
            {
                string driveId = sk.Id;

                if (existingConfig == null)
                {
                    var newConfig = new KeyConfiguration(
                        driveId,
                        nameTextBox.Text,
                        commandOnPlugTextBox.Text,
                        commandOnUnplugTextBox.Text
                    );
                    ConfigManager.KeyConfigurations.Add(newConfig);
                }
                else
                {
                    existingConfig.Name = nameTextBox.Text;
                    existingConfig.CommandOnPlug = commandOnPlugTextBox.Text;
                    existingConfig.CommandOnUnplug = commandOnUnplugTextBox.Text;
                }

                ConfigManager.SaveConfigurations();
                statusLabel.Text = "Configuration saved successfully!";
                statusLabel.ForeColor = Color.Green;
            }
            else
            {
                statusLabel.Text = "The drive is not a security key!";
                statusLabel.ForeColor = Color.Red;
            }
        }

        private void DriveListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (driveListBox.SelectedItem == null) return;

            SecureKeyListItem sk = (SecureKeyListItem)driveListBox.SelectedItem;
            string selectedDrive = sk.ToString().Split(' ')[0];
            var existingConfig = ConfigManager.FindConfigurationById(sk.Id);

            if (Commons.IsSecurityKey(selectedDrive) || (!string.IsNullOrEmpty(sk.Id) && existingConfig != null))
            {
                string driveId = sk.Id;

                if (existingConfig != null)
                {
                    nameTextBox.Text = existingConfig.Name;
                    commandOnPlugTextBox.Text = existingConfig.CommandOnPlug;
                    commandOnUnplugTextBox.Text = existingConfig.CommandOnUnplug;
                    statusLabel.Text = "Selected Security key";
                    statusLabel.ForeColor = Color.Green;
                }
                else
                {
                    statusLabel.Text = "No configuration found for the security key";
                    statusLabel.ForeColor = Color.Red;
                    nameTextBox.Clear();
                    commandOnPlugTextBox.Clear();
                    commandOnUnplugTextBox.Clear();
                }
                nameTextBox.Enabled = true;
                commandOnPlugTextBox.Enabled = true;
                commandOnUnplugTextBox.Enabled = true;
                confirmButton.Enabled = true;
            }
            else if (!string.IsNullOrEmpty(sk.Id) && existingConfig != null)
            {
                statusLabel.Text = "The selected drive is a security key but is not connected";
                statusLabel.ForeColor = Color.Green;

                if (existingConfig != null)
                {
                    nameTextBox.Text = existingConfig.Name;
                    commandOnPlugTextBox.Text = existingConfig.CommandOnPlug;
                    commandOnUnplugTextBox.Text = existingConfig.CommandOnUnplug;
                }
                else
                {
                    statusLabel.Text = "";
                    nameTextBox.Clear();
                    commandOnPlugTextBox.Clear();
                    commandOnUnplugTextBox.Clear();
                }

                nameTextBox.Enabled = true;
                commandOnPlugTextBox.Enabled = true;
                commandOnUnplugTextBox.Enabled = true;
                confirmButton.Enabled = true;
            }
            else
            {
                statusLabel.Text = "The selected drive isn't a security key. Please create one first.";
                statusLabel.ForeColor = Color.DarkOrange;

                nameTextBox.Clear();
                commandOnPlugTextBox.Clear();
                commandOnUnplugTextBox.Clear();
                nameTextBox.Enabled = false;
                commandOnPlugTextBox.Enabled = false;
                commandOnUnplugTextBox.Enabled = false;
                confirmButton.Enabled = false;
            }
        }
    }
}
