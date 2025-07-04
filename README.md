# Thoth's Oracle
USB operated media display/controller for Windows, Linux, MacOS

# Features

- Software agnostic companion app listens to operating system rather than a specific program like Spotify app or Firefox
  - If OS is aware of media, companion app will be as well
- IPS display for maximum readability at 320x480
- Display the available metadata of current media
  - Thumbnail
  - Title
  - Album
  - Artist
  - Duration
  - Current position
- Basic media control
  - Play/pause
  - Skip track
  - Previous track
  - Mute/unmute
  - Volume control
- Tiltable screen
- Brightness control
- Lightweight companion app
- Screensaver mode to cycle through local images (WIP)
- Standard components allowing for custom configuration and ease of assembly
  - Hotswappable MX style or Kailh Choc low-profile style switches
  - 6mm rotary potentiometer and encoder
  - 3D printed shell
  - One screw size
  - Small PCB with only THT components

# How it works

## Hardware

- An ESP32-S3 zero controls all of the device inputs and displays information on screen
- ESP32 communicates with PC over USB C OTG connection

Diagram

## Software

- The software is written in C# for the Windows App.
- Companion app catches changes to focused media broadcast from the operating system.
- On media change, app requests metadata, packages it, then waits for signal from the Oracle that it is ready to receive the data.
- Metadata is packaged as follows with header tag denoting type, size, etc.:
  - System information containing date and time
  - Media duration and position
  - Media text: title, album, artist
  - Thumbnail
- All text is converted to UTF-8.
- Once ready signal is received, app sends a package over serial port, waits for ready signal, then proceeds to next package.
- When input commands are received, app requests the OS completes the action.

## Firmware

- The firmware is written in C.
- All serial communications are handled by Core 1. Core 0 handles all display features running LVGL.
- Thoth's Oracle emits ready signal periodically while idling, and emits control commands as soon as inputted.
- When data is received, the header tag is analyzed to know which buffer it should be put in.
- If there is an error with the header tag, all subsequent data is dumped, and firmware waits for a pause in data transmission before resetting header information and buffers, and returning to idle state.
- If no errors are detected, data is placed directley into the correct buffer for further analysis in the case of text data, or displayed immediately for images (rolling effect as image loads).
- Media text starts as one big array of characters, and is split into the three fields using escape characters inserted by companion app.
- The font used is DejaVu-Sans in 16 point, with as much of the character set as is hopefully reasonable to ensure maximum language compatibility, without containing the entire set.
- Once the expected number of bytes is received based on the header tag, the header is reset, Oracle emits a ready signal, then enters idle state.
- If firmware times out before receiving expected data, header tag is reset and device enters idle state.

# Configuration

 The following can be configured from companion app (WIP)
- Which program to listen to, or any playing media
- Port the device is connected to
- Sensitivity of volume knob

# FAQ

Someone ask me something, please.

# Installation

Companion app is provided as portable application that doesn't need installation.

## Instructions on making app launch on startup (Section WIP)

### Windows
- Select exe

# Support 
