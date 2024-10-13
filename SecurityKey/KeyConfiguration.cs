using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SKUSB
{
    public class KeyConfiguration
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string CommandOnPlug { get; set; }
        public string CommandOnUnplug { get; set; }

        public KeyConfiguration(string id, string name, string commandOnPlug, string commandOnUnplug)
        {
            Id = id;
            Name = name;
            CommandOnPlug = commandOnPlug;
            CommandOnUnplug = commandOnUnplug;
        }
    }

    // New Global Configuration class
    public class GlobalConfiguration
    {
        public bool AllowToastNotifications { get; set; } = true; // Default to true
    }

    public class ConfigManager
    {
        private const string ConfigFilePath = "usb_key_config.json";

        public static GlobalConfiguration GlobalConfig { get; private set; } = new GlobalConfiguration(); // Default global config
        public static List<KeyConfiguration> KeyConfigurations { get; private set; } = new List<KeyConfiguration>();

        // Load both global and USB key configurations
        public static void LoadConfigurations()
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);

                // Create a wrapper object to load both GlobalConfig and KeyConfigurations
                var configWrapper = JsonConvert.DeserializeObject<ConfigurationWrapper>(json);

                // Assign loaded values if present
                if (configWrapper != null)
                {
                    GlobalConfig = configWrapper.GlobalConfig ?? new GlobalConfiguration();
                    KeyConfigurations = configWrapper.KeyConfigurations ?? new List<KeyConfiguration>();
                }
            }
        }

        // Save both global and USB key configurations
        public static void SaveConfigurations()
        {
            var configWrapper = new ConfigurationWrapper
            {
                GlobalConfig = GlobalConfig,
                KeyConfigurations = KeyConfigurations
            };

            string json = JsonConvert.SerializeObject(configWrapper, Formatting.Indented);
            File.WriteAllText(ConfigFilePath, json);
        }

        // Find configuration by USB Key ID
        public static KeyConfiguration FindConfigurationById(string id)
        {
            return KeyConfigurations.Find(c => c.Id == id);
        }
    }

    // Wrapper class to hold both global and USB-specific configurations
    public class ConfigurationWrapper
    {
        public GlobalConfiguration GlobalConfig { get; set; }
        public List<KeyConfiguration> KeyConfigurations { get; set; }
    }
}
