"""/*
===========================================================================
Copyright (C) 2025 Dominique Negm

This file is part of Thoth's Oracle source code.

Thoth's Oracle source code is free software; you can redistribute it
and/or modify it under the terms of the GNU General Public License as
published by the Free Software Foundation; either version 3 of the License,
or (at your option) any later version.

Thoth's Oracle source code is distributed in the hope that it will be
useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Thoth's Oracle; if not, see <https://www.gnu.org/licenses/>
===========================================================================
*/
// ui_handler.py """
import pystray
from pystray import Menu, MenuItem
from PIL import Image
import threading
import queue
from device_handler import main_setup, main_exit
import serial.tools.list_ports
from config_handler import save_config, load_config, notify_queue, setup_queue
import os
import subprocess

ICONS_PATH = 'Icons_Images/' if os.path.isdir('Icons_Images/') else '../Icons_Images/'
global icon
icon_image = Image.open(fp=(ICONS_PATH+"huge.png"))
icon = pystray.Icon(name="Thoth's Oracle", icon=icon_image)
global ports_menu
ports_menu = list()
global icon_menu
global wall_list
global ports_action
global vol_action
global volume_list
global shortcut_btn
global exit_btn
global title
global notifying
notifying = True
global notify_thread
global refresh_oracle

def on_exit(icon, query):
    global notifying
    print("Exitting")
    notifying = False
    main_exit()
    icon.stop()

def on_shortcut():
    subprocess.Popen(["xdg-open", os.curdir])

def notify_loop():
    while notifying:
        try:
            msg = notify_queue.get(timeout=1)
            icon.notify(message=str(msg), title="Thoth's Oracle")
            notify_queue.task_done()
        except queue.Empty:
            pass


def on_port_select(icon, query):
    global ports_menu
    ports_menu = MenuItem(text="Port: " + str(query), action=ports_action)
    update_menu()
    or_c = load_config()
    or_c['ComPort'] = str(query)
    save_config(config=or_c)

def on_vol_sens_select(icon, query):
    global volume_list
    oracle = load_config()
    oracle['VolumeSensitivity'] = int(str(query))
    volume_list = MenuItem(text="Volume Sensitivity: " + str(oracle['VolumeSensitivity']), action=vol_action)
    save_config(oracle)
    update_menu()

def refresh_vol_sens():
    global vol_action
    global volume_list
    oracle = load_config()
    vol_list = []
    for sens in oracle['VolumeSensitivityOptions']:
        vol_list.append(MenuItem(text=str(sens), action=on_vol_sens_select))

    vol_action = Menu(*tuple(vol_list))
    volume_list = MenuItem(text="Volume Sensitivity: " + str(oracle['VolumeSensitivity']), action=vol_action)
    update_menu()

def on_wallpaper_select():
    oracle = load_config()
    global wall_list
    if oracle["WallpaperMode"]:
        oracle["WallpaperMode"] = False
        wall_list = MenuItem(text="Wallpaper Mode: Disabled", action=on_wallpaper_select)
    else:
        oracle["WallpaperMode"] = True
        wall_list = MenuItem(text="Wallpaper Mode: Enabled", action=on_wallpaper_select)
    update_menu()
    save_config(oracle)
    pass

def update_menu():
    global icon
    icon_menu = (title, refresh_oracle, volume_list, ports_menu, wall_list, shortcut_btn, exit_btn)
    icon.menu = icon_menu
    icon.update_menu()


def refresh_ports():
    global ports_menu
    global ports_action
    oracle = load_config()
    ports_list = list()
    ports = serial.tools.list_ports.grep(".*ACM.*")
    for port in ports:
        ports_list.append(MenuItem(text=port.device, action=on_port_select))

    ports_action = Menu(*tuple(ports_list))
    ports_menu = MenuItem(text="Port: " + oracle['ComPort'], action=ports_action)
    update_menu()

def reset_oracle():
    setup_queue.put("Refresh")

def ui_setup():
    global icon
    global ports_menu
    global icon_menu
    global wall_list
    global volume_list
    global exit_btn
    global title
    global notify_thread
    global refresh_oracle
    global shortcut_btn


    oracle = load_config()
    title = MenuItem("Thoth's Oracle", action=None)
    exit_btn = MenuItem("Exit", action=on_exit)
    shortcut_btn = MenuItem("Open Settings Folder", action=on_shortcut)
    wall_list = MenuItem(text="Wallpaper Mode: " + str(oracle['WallpaperMode']), action=on_wallpaper_select)
    volume_list = MenuItem(text="Volume", action=None)
    refresh_oracle = MenuItem(text="Refresh", action=reset_oracle)

    sub = MenuItem(text="Port", action=None)
    icon_menu = (wall_list, volume_list, sub, exit_btn)
    icon.title = "Thoth"
    icon.menu = icon_menu

    notify_thread = threading.Thread(target=notify_loop)
    notify_thread.start()
    #notify_loop()
    #notify_queue.put("Hello")
    #icon.notify("Thoth searches for his Oracle", "Thoth's Oracle")

    ui_thread = threading.Thread(target=icon.run)
    ui_thread.start()
    refresh_ports()
    refresh_vol_sens()
    main_setup()

ui_setup()