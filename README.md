
## Features

### ✅ Config UI
- [x] The user should be able to configure what each key does.
- [x] The user should be able to put labels on each key.

### ✅ Detection of Thumb Drive Events
- [x] Detect when a thumb drive is plugged in.
- [x] Detect when a thumb drive is unplugged.

### 🟩 Actions on USB Plug/Unplug
- [x] **Action on Plug**: 
  - [x] Upon plug, specify an action (run a file).
  - [x] Pass the hash of the key ID/data as a configurable parameter.
- [x] **Action on Unplug**: 
  - [x] Upon unplug, specify an action (run a file).
  - [x] Pass the hash of the key ID/data as a configurable parameter.

### USB Cloning
- [ ] Allow the user to clone USB Security Keys.

### 🔐 Security
- [x] Config and actions for security concerns should be saved on the user's system to prevent unauthorized code execution.
- [ ] Encrypt configuration files to avoid tampering.

### 🕑 Time-Based Locking
- [ ] Option to set time-based locking, where actions are only performed within a specific time window.
## Other features

- [x] **Multiple Keys**: Allow multiple keys to be assigned to one action (for backup in case of loss).
