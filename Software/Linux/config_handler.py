import yaml
import os
import fcntl
import signal
import queue
import datetime
import time
from default_config import DEFAULT_CONFIGURATION

CONFIG_FILE = 'config.yaml'
CONFIG_FOLDER = 'config/'
WALLPAPER_FOLDER = 'wallpapers/'
LOG_PATH = 'logs/'
LOG_FILE = 'logs.txt'
URI_FILE = 'session_IDs.txt'
#global setup_queue
setup_queue = queue.Queue(maxsize=1)

def load_config(path=CONFIG_FOLDER + CONFIG_FILE):
    try:
        with open(path, 'r') as file:
            config = yaml.safe_load(file)
        return config
    except FileNotFoundError as e:
        os.makedirs(CONFIG_FOLDER, exist_ok=True)
        with open(CONFIG_FOLDER + CONFIG_FILE, 'w') as file:
            file.write(DEFAULT_CONFIGURATION)
        with open(CONFIG_FOLDER + CONFIG_FILE, 'r') as file:
            config = yaml.safe_load(file)
            return config

#oracle_config = load_config(CONFIG_FOLDER + CONFIG_FILE)

#print(oracle_config['VolumeSensitivityOptions'][0])
#oracle_config['VolumeSensitivity'] = 4

def save_config(config):
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
        non_ui = False
        while line != "":
            if line == "\n" or line.startswith('#'):
                yamls.insert(line_number, line)
                non_ui = (line == "#Non UI Settings\n")
            line_number += 1
            line = file.readline()

    new_file = 'new_.yaml'
    with open(new_file, 'w') as file:
        lines = ''.join(map(str, yamls))
        file.writelines(lines)

#save_config(oracle_config)

def config_changed(signum, frame):
    try:
        time.sleep(0.25)
        setup_queue.put("Config changed", block=False)
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

def logging(log_msg, line_end='\n', mode='a', log_path = LOG_PATH, file_name = LOG_FILE, print_to_console=False, time_stamp=True):
    time_now = datetime.datetime.now()
    time_stamp_msg = log_msg
    if time_stamp:
        time_stamp_msg = str(time_now.hour) + ":" + str(time_now.minute) + ":" + str(time_now.second) + " " + log_msg
    if print_to_console:
        print(time_stamp_msg)
    try:
        with open(log_path + file_name, mode) as file:
            file.write(time_stamp_msg + line_end)
    except FileNotFoundError:
        os.makedirs(log_path, exist_ok=True)
        with open(log_path + file_name, 'w') as file:
            file.write(time_stamp_msg + line_end)