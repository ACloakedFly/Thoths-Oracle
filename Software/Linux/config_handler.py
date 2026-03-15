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
// config_handler.py """
import yaml
import os
import fcntl
import signal
import queue
import datetime
import time
from default_config import DEFAULT_CONFIGURATION
import threading

CONFIG_FILE = 'config.yaml'
CONFIG_FOLDER = 'config/'
WALLPAPER_FOLDER = 'wallpapers/'
LOG_PATH = 'logs/'
LOG_FILE = 'logs.txt'
URI_FILE = 'session_IDs.txt'
MAX_LOG_SIZE = 20971520
#global setup_queue
setup_queue = queue.Queue(maxsize=1)
notify_queue = queue.Queue(maxsize=1)
save_sem = threading.BoundedSemaphore(value=1)
load_sem = threading.BoundedSemaphore(value=1)

def load_config(path=CONFIG_FOLDER + CONFIG_FILE):
    if load_sem.acquire(timeout=1) is False:
        return
    try:
        with open(path, 'r') as file:
            config = yaml.safe_load(file)
        load_sem.release()
        return config
    except FileNotFoundError as e:
        os.makedirs(CONFIG_FOLDER, exist_ok=True)
        with open(CONFIG_FOLDER + CONFIG_FILE, 'w') as file:
            file.write(DEFAULT_CONFIGURATION)
        with open(CONFIG_FOLDER + CONFIG_FILE, 'r') as file:
            config = yaml.safe_load(file)
            load_sem.release()
            return config

def save_config(config):
    if save_sem.acquire(timeout=1) is False:
        return
    yamls = list()
    for key, val in config.items():
        if type(val) is not list:
            yamls.append(str(key) + ": " + str(val) + "\n")
        else:
            yamls.append(str(key) + ":\n")
            for sub_val in val:
                yamls.append("- " + str(sub_val) + "\n")

    with open(CONFIG_FOLDER + CONFIG_FILE, 'r') as file:
        line = file.readline()
        line_number = 0
        while line != "":
            if line == "\n" or line.startswith('#'):
                yamls.insert(line_number, line)
            line_number += 1
            line = file.readline()

    with open(CONFIG_FOLDER + CONFIG_FILE, 'w') as file:
        lines = ''.join(map(str, yamls))
        file.writelines(lines)
    save_sem.release()

def config_changed(signum, frame):
    try:
        time.sleep(0.5)
        setup_queue.put("Config changed", timeout=1)
        logging("Queue has space")
    except Exception as e:
        logging("Queue full" + str(e))

def config_watcher():
    signal.signal(signal.SIGIO, config_changed)
    os.makedirs(CONFIG_FOLDER, exist_ok=True)
    os.makedirs(WALLPAPER_FOLDER, exist_ok=True)
    fd = os.open(CONFIG_FOLDER, os.O_RDONLY)
    fcntl.fcntl(fd, fcntl.F_SETSIG, 0)
    fcntl.fcntl(fd, fcntl.F_NOTIFY, fcntl.DN_MODIFY | fcntl.DN_MULTISHOT)

def logging(log_msg, line_end='\n', mode='a', log_path = LOG_PATH, file_name = LOG_FILE, print_to_console=False, time_stamp=True, notify=False):
    time_now = datetime.datetime.now()
    if notify:
        notify_queue.put(log_msg, block=False)
    time_stamp_msg = log_msg
    if time_stamp:
        time_stamp_msg = str(time_now.hour) + ":" + str(time_now.minute) + ":" + str(time_now.second) + " " + log_msg
    if print_to_console:
        print(time_stamp_msg)
    try:
        if os.stat(log_path + file_name).st_size >= MAX_LOG_SIZE:
            mode = 'w'
        with open(log_path + file_name, mode) as file:
            file.write(time_stamp_msg + line_end)
    except FileNotFoundError:
        os.makedirs(log_path, exist_ok=True)
        with open(log_path + file_name, 'w') as file:
            file.write(time_stamp_msg + line_end)