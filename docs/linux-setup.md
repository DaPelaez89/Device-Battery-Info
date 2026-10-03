# Setting up Device Battery Info on Linux

Most of the plugin works on Linux without any setup: this computer's battery, Android phones through
Macro Deck's adb, and Bluetooth devices. **Only the USB devices from the "Other devices" catalog** (Razer,
Logitech, Corsair, Rapoo, AULA and Sony) need a one-time permission. If Macro Deck shows the problem
*"Linux needs a one-time permission to read a device"* for this plugin, this page is the fix.

## Why it is needed

The plugin asks these devices for their battery level directly over USB, through a file Linux creates for
each of them (`/dev/hidraw0`, `/dev/hidraw1`, ...). By default only root may open those files, so the
plugin can see that your mouse is plugged in but cannot ask it anything.

The fix is a udev rule: a small text file that tells Linux to give **the user logged in at this computer**
access to devices from these brands only. Steam, OpenRGB and similar tools set up their devices the same
way. Nothing runs as root, and other users on the machine get no access.

## Install the rule

Open a terminal and run:

```bash
sudo curl -fsSL -o /etc/udev/rules.d/70-device-battery-info.rules \
  https://raw.githubusercontent.com/PyFlat/Device-Battery-Info/main/packaging/linux/70-device-battery-info.rules
sudo udevadm control --reload-rules
sudo udevadm trigger --subsystem-match=hidraw
```

Then **unplug the device (or its USB receiver) and plug it back in**. The problem in Macro Deck disappears
on its own at the plugin's next battery read. No restart is needed.

You can read the rule before installing it:
[packaging/linux/70-device-battery-info.rules](../packaging/linux/70-device-battery-info.rules).

## Check that it worked

```bash
ls -l /dev/hidraw*
```

The nodes of your device now end in a `+` (for example `crw-rw----+`), which means an extra permission is
attached. `getfacl /dev/hidraw0` should list your user name with `rw-`.

## If it still does not work

- **The rule file name must start with `70-`** (or any number below 73). Linux applies the permission in
  `73-seat-late.rules`, and a rule that runs later has no effect.
- **Your distribution needs systemd** (true for Ubuntu, Fedora, Debian, Arch, openSUSE and most others).
  The rule relies on systemd granting access to the logged-in user, and the Bluetooth support relies on
  systemd's `busctl`.
- **A device connected over Bluetooth** instead of USB is not covered by this rule. Add it as a
  **Bluetooth device** in the plugin instead.
- **Updating the plugin can add new brands.** If a newly supported device shows the problem again, run the
  install commands once more to get the updated rule.

## Removing it

```bash
sudo rm /etc/udev/rules.d/70-device-battery-info.rules
sudo udevadm control --reload-rules
```
