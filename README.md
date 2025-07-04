# Thoth's Oracle
USB operated media display/controller for Windows, Linux, MacOS

# Features

- Software agnostic; companion app listens to operating system rather than a specific program like Spotify app or Firefox
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
- Metadata is packaged as follows:
  - System information containing date and time
  - Media duration and position
  - Media text: title, album, artist
  - Thumbnail
- Once ready signal is received, app sends a package over serial port, waits for ready signal, then proceeds to next package.
- When input commands are received, app requests the OS completes the action.

## Firmware

- The firmware is written in C.
- All serial communications are handled by Core 1. Core 0 handles all display features running LVGL.
- Thoth's Oracle emits ready signal periodically while idling, and emits control commands as soon as inputted.

# Configuration

# FAQ

# Installation

# Support 
