import threading
import serial
from dbus.mainloop.glib import DBusGMainLoop
from mpris2 import get_players_uri
from mpris2 import Player
from gi.repository import GLib
import queue
import time
import datetime
from wand.image import Image
from wand.color import Color
from config_handler import config_watcher, load_config, logging, URI_FILE, WALLPAPER_FOLDER
import config_handler
import re
import os

global player
#global meta_queue
global oracle_ready
oracle_ready = True
global oracle_serial
global oracle_config
global read_thread
global serial_read
serial_read = True
global duration
duration = 0
global images
images = []
global wallpaper_index
wallpaper_index = 0
global wallpaper_num
global wallpaper_mode
global wallpaper_period
global wallpaper_timer
global wallpaper_paused
wallpaper_paused = False
wallpaper_period = 0
wallpaper_mode = False
wallpaper_num = 0

def serial_decode(code, cmd):
    global wallpaper_paused
    if code == 7 or code == 8:
        #print("Oracle Ready")
        global oracle_ready
        oracle_ready = True
    if code == 5:
        #print("Serial read config: ", oracle_config)
        vol_sens = 3 if oracle_config['VolumeSensitivity'] is None else oracle_config['VolumeSensitivity']
        try:
            if cmd == 1:
                player.Volume -= vol_sens/100
            elif cmd == 2:
                player.Volume += vol_sens/100
            elif cmd == 3:
                player.Volume = 0
            elif cmd == 4:
                if wallpaper_mode:
                    wallpaper_timer.cancel()
                    next_wallpaper(-1)
                else:
                    player.Previous()
            elif cmd == 5:
                if wallpaper_mode:
                    if wallpaper_paused is False:
                        wallpaper_paused = True
                        pause_wallpaper()
                        wallpaper_timer.cancel()
                    else:
                        wallpaper_paused = False
                        next_wallpaper(0)
                else:
                    player.PlayPause()
            elif cmd == 6:
                if wallpaper_mode:
                    wallpaper_timer.cancel()
                    next_wallpaper()
                else:
                    player.Next()
        except Exception as e:
            logging(str(e))

def serial_reader():
    message = ""
    code = -1
    command = -1
    while serial_read:
        next_byte = b"\x00"
        try:
            next_byte = oracle_serial.read()
        except Exception as e:
            logging(str(e))
        next_int = int.from_bytes(next_byte)
        if next_int == 0:
            continue
        elif next_int < 10:
            if code <= 0:
                code = next_int
            else:
                command = next_int
        elif next_int != 10:
            message += next_byte.decode("utf-8")
        else:
            logging(f"{code} | {command} : {message}")
            serial_decode(code, command)
            message = ""
            code = -1
            command = -1

def uri_selection():
    global player
    uri_list = get_players_uri()
    uri_string = '\n'.join(uri_list)
    logging(uri_string, mode='w', file_name=URI_FILE, time_stamp=False)
    uri_ordered = []
    for program in oracle_config['MonitoredProgram']:
        matched_uri = re.findall(".*MediaPlayer2.*" + program + ".*", uri_string, flags=re.IGNORECASE)
        if len(matched_uri) > 0:
            uri_ordered.append(matched_uri[0])
            
    if len(uri_ordered) == 0:
        uri_ordered.append("")
    logging("Ordered matches: ")
    logging("\n".join(uri_ordered))
    uri = uri_ordered[0]
    if uri != "":
        try:
            player = Player(dbus_interface_info={'dbus_uri': uri})
            player.PropertiesChanged = player_handler
        except Exception as e:
            logging("If AppArmor issue, please resolve, then restart app\n" + str(e))
    else:
        logging("No player monitored")

def serial_setup():
    global oracle_ready
    global oracle_serial
    global read_thread
    global serial_read
    oracle_ready = True
    try:
        oracle_serial = serial.Serial(oracle_config['ComPort'], oracle_config['Speed'], timeout=oracle_config['ReadTimeout'], parity=serial.PARITY_NONE, rtscts=1)
        oracle_serial.close()
        oracle_serial.open()
        if read_thread.is_alive():
            serial_read = False
            read_thread.join()
        read_thread = threading.Thread(target=serial_reader, name="SerialRead")
        serial_read = True
        read_thread.start()
    except serial.SerialException as e:
        logging("ComPort not found, please select another.")

def general_setup():
    while True:
        global oracle_config
        global wallpaper_mode
        global wallpaper_period
        time.sleep(0.5)
        config_handler.setup_queue.get(block=True)
        old_config = None
        if 'oracle_config' in locals():
            old_config = oracle_config
        oracle_config = load_config()
        if old_config != None:
            if old_config['MonitoredProgram'] != oracle_config['MonitoredProgram']:
                uri_selection()
            if old_config['ComPort'] != oracle_config['ComPort']:
                serial_setup()
        else:
            uri_selection()
            serial_setup()
        wallpaper_mode = oracle_config['WallpaperMode']
        wallpaper_period = oracle_config['WallpaperPeriod']*60
        if wallpaper_mode:
            wallpaper_handler()
            next_wallpaper()
        else:
            try:
                values = dict(Metadata=player.Metadata)
                data_handler(values)
            except Exception as e:
                logging(str(e))
        config_handler.setup_queue.task_done()

def convert_to_565(bytes):
    byte_count = len(bytes)
    rgb565 = bytearray()
    for i in range(0, byte_count, 3):
        rg = ((bytes[i] & 0xF8) | (bytes[i + 1] >> 5))
        rgb565.extend(rg.to_bytes(1, 'little'))
        gb = (((bytes[i + 1] & 0x1C) << 3) | (bytes[i + 2] >> 3))
        rgb565.extend(gb.to_bytes(1, 'little'))
    return rgb565

def write_bytes(tag, length, data, width, height, dur = 0):
    attemps = 0
    global oracle_ready
    while not oracle_ready:
        if attemps >= 2:
            logging("Tried to send " + str(tag) + " oracle timed out")
            return
        attemps+=1
        time.sleep(0.2)
    oracle_ready = False

    bytes_to_send = bytearray([tag])
    bytes_to_send.extend(length.to_bytes(3, 'little'))
    bytes_to_send.extend(width.to_bytes(2, 'little'))
    bytes_to_send.extend(height.to_bytes(2, 'little'))
    bytes_to_send.extend(dur.to_bytes(4, 'little'))
    if data is not None:
        bytes_to_send.extend(data)
    #print(" ".join(hex(b) for b in bytes_to_send))
    oracle_serial.write(bytes_to_send)

def data_handler(*args):
    global duration
    properties = args[0]
    position = 0
    try:
        position = int(player.Position/1000000)
    except Exception as e:
        logging("Position unavailable")

    if "Metadata" in properties:
        #Date and time
        current_time = datetime.datetime.now()
        day_month = int(current_time.day + (current_time.month << 8))
        current_seconds = int((current_time-datetime.datetime(current_time.year, current_time.month, current_time.day)).total_seconds())
        writer_queue.put(dict(tag=3, length=0, data=None, width=day_month, height=current_time.year, dur=current_seconds))

        #Position and duration
        pos_dur = bytearray()
        duration = int((properties['Metadata']['mpris:length'])/1000000)
        pos_dur.extend(position.to_bytes(4, 'little'))
        pos_dur.extend(duration.to_bytes(4, 'little'))
        playing = 0
        if "PlaybackStatus" in properties:
            if properties['PlaybackStatus'] == "Playing":
                playing = 1
        writer_queue.put(dict(tag=4, length=len(pos_dur), data=pos_dur, width=playing, height=1))

        #Text data
        artists = ", ".join([str(ele) for ele in properties['Metadata']['xesam:albumArtist']]) if oracle_config['AlbumArtist'] else ", ".join([str(ele) for ele in properties['Metadata']['xesam:artist']])
        meta_text = properties['Metadata']['xesam:title'] + "\n" + artists + "\n" + properties['Metadata']['xesam:album'] + "\n"
        meta_bytes = bytearray(meta_text, encoding='utf8')
        writer_queue.put(dict(tag=2, length=len(meta_bytes), data=meta_bytes, width=0, height=0))

        #Image data
        if "mpris:artUrl" in properties['Metadata']:
            thumb = properties['Metadata']['mpris:artUrl']
            with Image(filename=thumb) as img:
                img.transform(resize='304x304')
                img.background_color = Color('black')
                img.extent(304, 304, gravity='center')
                img.save(filename='thumby.png')
                pixels = img.export_pixels(channel_map="RGB")
                rgb565 = convert_to_565(pixels)
                writer_queue.put(dict(tag=1, length=len(rgb565), data=rgb565, width=img.width, height=img.height))

    #Playback changed
    elif "PlaybackStatus" in properties:
        logging("Playback status: " + properties['PlaybackStatus'])
        playing = 0
        if properties['PlaybackStatus'] == "Playing":
            playing = 1
        pos_dur = bytearray()
        pos_dur.extend(position.to_bytes(4, 'little'))
        pos_dur.extend(duration.to_bytes(4, 'little'))
        writer_queue.put(dict(tag=4, length=len(pos_dur), data=pos_dur, width=playing, height=0))


def queue_handler():
    while True:
        properties = meta_queue.get()[0]
        if meta_queue.qsize() >= 5:
            logging(" ending prematurely ")
            meta_queue.task_done()
        else:
            #logging("Sending properties: " + str(properties))
            data_handler(properties)
            meta_queue.task_done()


def player_handler(self, *args, **kw):
    try:
        #print("Total tasks from player ", meta_queue.qsize(), " song ", player.Metadata['xesam:title'])
        if meta_queue.qsize() < 38 and oracle_config['WallpaperMode'] is False:
            meta_queue.put(args, block=False)
    except Exception as e: 
        logging(str(e))

def wallpaper_handler():
    global images
    global wallpaper_num
    images = os.listdir(WALLPAPER_FOLDER)
    images = [(WALLPAPER_FOLDER + f) for f in images if os.path.isfile(WALLPAPER_FOLDER+f)]
    wallpaper_num = len(images)

def pause_wallpaper():
    pos_dur = bytearray(8)
    playing = 0
    writer_queue.put(dict(tag=4, length=len(pos_dur), data=pos_dur, width=playing, height=1))

def next_wallpaper(direction=1):
    global wallpaper_index
    global wallpaper_timer
    wallpaper_index += direction
    wallpaper_index %= wallpaper_num
    thumb = images[wallpaper_index]
    logging("Loading next image: " + thumb)
    meta_text = oracle_config['WallpaperTitle'] + "\n" + oracle_config['WallpaperAlbum'] + "\n" + oracle_config['WallpaperArtist'] + "\n"
    meta_bytes = bytearray(meta_text, encoding='utf8')
    writer_queue.put(dict(tag=2, length=len(meta_bytes), data=meta_bytes, width=0, height=0))

    #Position and duration
    pos_dur = bytearray(4)
    duration = oracle_config['WallpaperPeriod']*60
    pos_dur.extend(duration.to_bytes(4, 'little'))
    playing = 1
    writer_queue.put(dict(tag=4, length=len(pos_dur), data=pos_dur, width=playing, height=1))

    with Image(filename=thumb) as img:
        img.transform(resize='304x304')
        img.background_color = Color('black')
        img.extent(304, 304, gravity='center')
        img.save(filename='thumby.png')
        pixels = img.export_pixels(channel_map="RGB")
        rgb565 = convert_to_565(pixels)
        writer_queue.put(dict(tag=1, length=len(rgb565), data=rgb565, width=img.width, height=img.height))
    
    if wallpaper_mode:
        wallpaper_timer = threading.Timer(interval=wallpaper_period, function=next_wallpaper)
        wallpaper_timer.start()

def serial_write_bytes():
    while True:
        kwargs = writer_queue.get()
        attemps = 0
        global oracle_ready
        while not oracle_ready:
            if attemps >= 2:
                logging("Tried to send " + str(kwargs['tag']) + " oracle timed out")
                return
            attemps+=1
            time.sleep(0.2)
        oracle_ready = False
        dur = 0 if "dur" not in kwargs else kwargs['dur']
        bytes_to_send = bytearray([kwargs['tag']])
        bytes_to_send.extend(kwargs['length'].to_bytes(3, 'little'))
        bytes_to_send.extend(kwargs['width'].to_bytes(2, 'little'))
        bytes_to_send.extend(kwargs['height'].to_bytes(2, 'little'))
        bytes_to_send.extend(dur.to_bytes(4, 'little'))
        if kwargs['data'] is not None:
            bytes_to_send.extend(kwargs['data'])
        #print(" ".join(hex(b) for b in bytes_to_send))
        oracle_serial.write(bytes_to_send)
        writer_queue.task_done()

read_thread = threading.Thread(target=serial_reader, name="SerialRead")
logging("Start log", mode='w')
meta_queue = queue.Queue(maxsize=40)
DBusGMainLoop(set_as_default=True)
setup_thread = threading.Thread(target=general_setup)
setup_thread.start()
config_handler.setup_queue.put("Main", block=False)
meta_thread = threading.Thread(target=queue_handler, name="MetadataThread")
meta_thread.start()

writer_thread = threading.Thread(target=serial_write_bytes, name="WriterThread")
writer_queue = queue.Queue(maxsize=1)
writer_thread.start()

wallpaper_timer = threading.Timer(interval=wallpaper_period, function=next_wallpaper)

config_watcher()
mloop = GLib.MainLoop()
mloop.run()