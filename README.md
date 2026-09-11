# Headset Battery

<p align="center">
  <img src="Assets/Logo_anim2.gif" width="128" style="max-width: 100%; height: auto;" alt="Headset Battery Monitor logo">
</p>

[Русский](README.ru.md)

**Headset Battery** is a compact, portable, tray-only Windows app that shows the current audio output device and, when available, the battery level of connected Bluetooth headphones or headsets.

No installation is required: unpack the release archive and run `HeadsetBattery.exe` from any folder. Settings and the optional startup entry are stored for the current user.

## Features

![Headset Battery in the Windows notification area](Assets/live.png)

- Detects the active Windows audio output: speakers, Bluetooth headphones, or a headset in Hands-Free mode.
- Shows the matching tray glyph. For headphones and headsets, the glyph fill is green at a normal level and red at 30% or below.
- Can show the exact percentage in a coloured badge instead of the fill; the option is available in Settings.
- Lists detected connected Bluetooth audio devices, their available battery levels, and marks the active device in the context menu.
- Optionally tracks other connected Bluetooth and BLE devices, such as mice or keyboards, when Windows exposes their battery level; they appear in a separate menu section.
- Reads data supplied by Windows for classic HFP headsets, the system battery property, and, when available, the standard BLE Battery Service.
- Lets you choose a low-battery threshold (40%, 30%, 20%, or 10%) and shows independent connection and low-battery notifications for each device. Charging devices do not trigger low-battery alerts.
- Uses the Windows accent colour for devices at or below the selected threshold.
- Offers automatic, light, and dark tray-icon themes, and opens its menu with either mouse button.
- Saves user preferences, supports per-user startup with Windows, and permits only one running instance.
- Localizes the menu to the Windows display language: Russian, German, Spanish, French, or Chinese; English is used as a fallback.

## Compatibility

- Windows 10 version 1709 or later, including Windows 11; both 32-bit and 64-bit editions are supported.
- .NET Framework 4.8 is required. It is included with recent Windows 10 and Windows 11 releases; older Windows 10 versions may need the Microsoft .NET Framework 4.8 installer.
- Windows 7 and Windows 8.1 are not supported: the Bluetooth and battery APIs used by the app are available only on Windows 10 or later.

## Notes

Battery availability and accuracy depend on the headphones, the Bluetooth driver, and whether the device publishes battery data to Windows. If no supported source provides a value, the app still lists the device but does not show a battery level.

## Building from source

This section is only needed if you want to build the app yourself or modify its source code.

The project targets Visual Studio 2019 and .NET Framework 4.8.

1. Open `HeadsetBat.csproj`.
2. Restore the `Microsoft.Windows.SDK.Contracts` package.
3. Build the solution. The executable is created at `bin\Debug\net48\win-x86\HeadsetBattery.exe`.
