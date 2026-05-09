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
// device_handler.py """
import threading
import serial
from dbus.mainloop.glib import DBusGMainLoop
import dbus
from mpris2 import get_players_uri
from mpris2 import Player
import queue
import time
import datetime
from wand.image import Image
from wand.color import Color as wand_color
from wand import exceptions as wand_exceptions
from config_handler import config_watcher, load_config, logging, URI_FILE, WALLPAPER_FOLDER
import config_handler
import re
import os
from colour import Color

MAX_QUEUE_WAIT = 5 #seconds
WRITER = 0
META = 1
SETUP = 2
global player
global oracle_ready
oracle_ready = True
global oracle_serial
oracle_serial = None
global oracle_config
global old_config
global read_thread
global serial_reading
serial_reading = False
global serial_writing
serial_writing = True
global meta_reading
meta_reading = True
global setting_up
setting_up = True
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
global meta_thread
global meta_queue
global queued_media
queued_media = False
global writer_queue
global writer_thread
global setup_thread
global exitting
global status_signal
global seeked_signal
global bus
global media_check_thread
global media_handler
global player_uri
player_uri = ""
global reconnect
reconnect = True

MAX_ATTEMPTS = 20
#Message types
IMG_TAG =       1
TEXT_TAG =      2
SYS_MSG_TAG =   3
DUR_POS_TAG =   4
CMD_TAG =       5
INFO_TAG =      6
STATUS_TAG =    7
FINISHED_TAG =  8
ERROR_TAG =     9
COLOUR_TAG =    10
CODES = ["img_tag", "text_tag", "sys_msg_tag", "dur_pos_tag", "cmd_tag", "info_tag", "status_tag", "finished_tag", "error_tag", "colour_tag"]

def serial_decode(code, cmd):
    global wallpaper_paused
    global queued_media
    if code == ERROR_TAG:
        queued_media = True
    elif code == STATUS_TAG or code == FINISHED_TAG:
        global oracle_ready
        oracle_ready = True
    elif code == CMD_TAG:
        vol_sens = 3 if oracle_config['VolumeSensitivity'] is None else oracle_config['VolumeSensitivity']
        try:
            if cmd == 1:
                player.Volume -= vol_sens/100
            elif cmd == 2:
                player.Volume += vol_sens/100
            elif cmd == 3:
                player.Volume = 0.0
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
            logging("serial_decode: " + str(e))

def queue_putter(args, mode = WRITER):
    try:
        match mode:
            case 0:
                writer_queue.put(args, timeout=MAX_QUEUE_WAIT)
                pass
            case 1:
                meta_queue.put(args, timeout=MAX_QUEUE_WAIT)
                pass
            case 2:
                config_handler.setup_queue.put(args, timeout=MAX_QUEUE_WAIT)
                pass
    except queue.Full:
        pass
    except Exception as e:
        logging("queue_putter: " + str(mode) + " | " + str(e))

def serial_reader():
    message = ""
    code = -1
    command = -1
    while serial_reading:
        next_byte = b"\x00"
        try:
            next_byte = oracle_serial.read()
        except Exception as e:
            logging("serial_reader: " + str(e))
            queue_putter("serial_reader", mode=SETUP)
            return -1
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
            if code == -1:
                continue
            logging(f"{code} | {command} : {message}")
            serial_decode(code, command)
            message = ""
            code = -1
            command = -1

def uri_selection(skip = False, sender_uri = ""):
    global player
    global status_signal
    global media_handler
    global player_uri
    uri_list = get_players_uri()
    uri_string = '\n'.join(uri_list)
    if skip and sender_uri in uri_list:
        player_uri = sender_uri
    else:
        logging(uri_string, mode='a', file_name=URI_FILE, time_stamp=False)
        uri_ordered = []
        for program in oracle_config['MonitoredProgram']:
            matched_uri = re.findall(".*MediaPlayer2.*" + program + ".*", uri_string, flags=re.IGNORECASE)
            if len(matched_uri) > 0:
                uri_ordered.append(matched_uri[0])
                
        if len(uri_ordered) == 0:
            uri_ordered.append("")
        logging("Ordered matches: ")
        logging("\n".join(uri_ordered))
        player_uri = uri_ordered[0]
    if player_uri != "":
        try:
            player = Player(dbus_interface_info={'dbus_uri': player_uri})
            logging("Connected to " + player_uri + " playing? " + str(player.PlaybackStatus))
            if player.PlaybackStatus == "Stopped":
                status_signal.remove()
                status_signal = bus.add_signal_receiver(handler_function=status_changed, bus_name=player_uri, path='/org/mpris/MediaPlayer2', dbus_interface='org.freedesktop.DBus.Properties', sender_keyword="sender", destination_keyword="destination", interface_keyword="interface", member_keyword="member", path_keyword="path", message_keyword="msg") 
            else:
                media_handler.remove()
                media_handler = bus.add_signal_receiver(handler_function=player_handler, bus_name=player_uri, dbus_interface='org.freedesktop.DBus.Properties')
            if skip:
                queue_putter("Session changed", mode=SETUP)
            return True
        except Exception as e:
            logging("If AppArmor issue, please resolve, then restart app\n" + str(e), notify=False)
            return False
    else:
        logging("No player monitored", notify=False)
        return False

def serial_setup():
    logging("serial setting up")
    global oracle_ready
    global oracle_serial
    global read_thread
    global serial_reading
    oracle_ready = True
    try:
        oracle_serial = serial.Serial(oracle_config['ComPort'], oracle_config['Speed'], timeout=oracle_config['ReadTimeout']/1000, parity=serial.PARITY_NONE, rtscts=1)
        oracle_serial.close()
        oracle_serial.open()
        if read_thread.is_alive():
            serial_reading = False
            read_thread.join()
        if oracle_serial.readable() and oracle_serial is not None and oracle_serial.is_open:
            serial_reading = True
            read_thread = threading.Thread(target=serial_reader, name="SerialRead")
            read_thread.start()
            update_date_time()
            logging("Connected to device")
        else:
            serial_reading = False
    except serial.SerialException as e:
        oracle_serial = None
        serial_reading = False
        oracle_ready = True
        logging("ComPort not found, please select another.", notify=False)
    except Exception as e:
        logging("serial_setup error: " + str(e))

def general_setup():
    while setting_up:
        global oracle_config
        global old_config
        global wallpaper_mode
        global wallpaper_period
        updated = True
        try:
            time.sleep(0.5)
            setup_instance = config_handler.setup_queue.get(block=True, timeout=1)
            if exitting or str(setup_instance) == "Exit":
                continue
            logging("Setup changed from " + setup_instance)
            oracle_config = load_config()
            if old_config != None:
                if old_config['MonitoredProgram'] != oracle_config['MonitoredProgram']:
                    updated = uri_selection()
                if old_config['ComPort'] != oracle_config['ComPort'] or str(setup_instance) == "serial_reader" or str(setup_instance) == "Refresh":
                    serial_setup()
                if old_config['Colour'] != oracle_config['Colour']:
                    update_colour()
            else:
                updated = uri_selection()
                serial_setup()
                update_colour()
            old_config = oracle_config
            if str(setup_instance) == "Reconnect":
                serial_setup()
            wallpaper_mode = oracle_config['WallpaperMode']
            wallpaper_period = oracle_config['WallpaperPeriod']*60
            if wallpaper_mode:
                wallpaper_handler()
                next_wallpaper()
            else:
                if not updated:
                    continue
                try:
                    values = dict(Metadata=player.Metadata, PlaybackStatus=player.PlaybackStatus)
                    if meta_queue.qsize() < 38 and oracle_config['WallpaperMode'] is False:
                        queue_putter((values, 0), mode=META)
                    
                    logging("Starting media check timer")
                except Exception as e:
                    logging("general_setup initial_values: " + str(e))
            config_handler.setup_queue.task_done()
        except queue.Empty as e:
            pass
            #logging("No tasks")
        except Exception as e:
            logging("general_setup general: " + str(e))

def convert_to_565(bytes):
    byte_count = len(bytes)
    rgb565 = bytearray()
    for i in range(0, byte_count, 3):
        rg = ((bytes[i] & 0xF8) | (bytes[i + 1] >> 5))
        rgb565.extend(rg.to_bytes(1, 'little'))
        gb = (((bytes[i + 1] & 0x1C) << 3) | (bytes[i + 2] >> 3))
        rgb565.extend(gb.to_bytes(1, 'little'))
    return rgb565

def update_colour():
    hex_c = "#%0.6X" % oracle_config['Colour']
    c = Color(str(hex_c))
    cd = Color(c)
    cd.set_luminance((cd.get_luminance() - 0.2) % 1)
    ci = int(c.get_hex_l().removeprefix("#"), 16)
    cdi = int(cd.get_hex_l().removeprefix("#"), 16)
    logging("Progress bar colour: " + str(c.get_hex_l()) + " date/time line colour: " + str(cd.get_hex_l()))
    queue_putter(dict(tag=COLOUR_TAG, length=cdi, data=None, width=0, height=0, dur=ci))

def update_date_time():
    #Date and time
    current_time = datetime.datetime.now()
    day_month = int(current_time.day + (current_time.month << 8))
    current_seconds = int((current_time-datetime.datetime(current_time.year, current_time.month, current_time.day)).total_seconds())
    queue_putter(dict(tag=SYS_MSG_TAG, length=0, data=None, width=day_month, height=current_time.year, dur=current_seconds))

def data_handler(*args):
    global duration
    properties = args[0]
    position = 0
    try:
        position = int(player.Position/1000000)
    except Exception as e:
        logging("Position unavailable")
    if "Metadata" in properties:
        try:
            update_date_time()
            #Position and duration
            pos_dur = bytearray()
            duration = int((properties['Metadata']['mpris:length'])/1000000)
            pos_dur.extend(position.to_bytes(4, 'little'))
            pos_dur.extend(duration.to_bytes(4, 'little'))
            playing = 0
            if "PlaybackStatus" in properties:
                if properties['PlaybackStatus'] == "Playing":
                    playing = 1
            queue_putter(dict(tag=DUR_POS_TAG, length=len(pos_dur), data=pos_dur, width=playing, height=1))
            #Text data
            artists = ", ".join([str(ele) for ele in properties['Metadata']['xesam:albumArtist']]) if oracle_config['AlbumArtist'] else ", ".join([str(ele) for ele in properties['Metadata']['xesam:artist']])
            meta_text = properties['Metadata']['xesam:title'] + "\n" + artists + "\n" + properties['Metadata']['xesam:album'] + "\n"
            meta_bytes = bytearray(meta_text, encoding='utf8')
            queue_putter(dict(tag=TEXT_TAG, length=len(meta_bytes), data=meta_bytes, width=0, height=0))
            #Image data
            if "mpris:artUrl" in properties['Metadata']:
                thumb = properties['Metadata']['mpris:artUrl']
                thumb = thumb.replace("%20",  " ")
                #logging("Opening file: " + thumb)
                with Image(filename=thumb) as img:
                    img.transform(resize='304x304')
                    img.background_color = wand_color('black')
                    img.extent(304, 304, gravity='center')
                    img.save(filename='thumby.png')
                    pixels = img.export_pixels(channel_map="RGB")
                    rgb565 = convert_to_565(pixels)
                    queue_putter(dict(tag=IMG_TAG, length=len(rgb565), data=rgb565, width=img.width, height=img.height))
        except KeyError:
            pass
        except wand_exceptions.BlobError:
            logging("Error with image file")
        except Exception as e:
            logging("data_handler: " + str(e))
    #Playback changed
    if "PlaybackStatus" in properties:
        logging("Playback status: " + properties['PlaybackStatus'])
        playing = 0
        if properties['PlaybackStatus'] == "Playing":
            playing = 1
        pos_dur = bytearray()
        pos_dur.extend(position.to_bytes(4, 'little'))
        pos_dur.extend(duration.to_bytes(4, 'little'))
        queue_putter(dict(tag=DUR_POS_TAG, length=len(pos_dur), data=pos_dur, width=playing, height=0))

def log_media(*media):
    if "Metadata" in media[0]:
        try:
            logging(str(media[0]['Metadata']['xesam:title']) + "\n" + str(media[0]['Metadata']['xesam:albumArtist'][0]) + "\n" + str(media[0]['Metadata']['xesam:album']), time_stamp=False)
        except:
            pass
    pass 

def queue_handler():
    global queued_media
    while meta_reading:
        try:
            properties = meta_queue.get(timeout=1)
            #logging("meta_thread: " + str(properties))
            if str(properties) == "Exit":
                logging("Queue handler exitting")
                return
            if meta_queue.qsize() >= 5:
                meta_queue.task_done()
                while meta_queue.not_empty:
                    meta_queue.get(block=False)
                    meta_queue.task_done()
                #logging(" ending prematurely ")
                queued_media = True
            elif len(properties) == 1:
                #logging("Sending properties: " + str(properties))
                data_handler(properties)
            elif len(properties) >= 2:
                if type(properties[1]) is dbus.Dictionary:
                    #logging("Sending properties" + str(properties[1]))
                    data_handler(properties[1])
                    log_media(properties[1])    
                else:
                    data_handler(properties[0])
                    log_media(properties[0])
            else:
                #logging("Not right " + str(len(properties)) + " \n" + str(properties))
                pass
            meta_queue.task_done()
        except queue.Empty:
            pass
        except Exception as e:
            logging("queue_handler: " + str(e))

#add another queue as buffer for media check rather than a super loop?
def media_check():
    global queued_media
    global reconnect
    counter = 0
    QUEUED_WAIT = 5 #seconds
    UNQUEUED_WAIT = 1 #seconds
    COUNT = 10 / UNQUEUED_WAIT
    while not exitting:
        try:
            if queued_media:
                queued_media = False
                if player is not None:
                    time.sleep(QUEUED_WAIT)
                    values = dict(Metadata=player.Metadata, PlaybackStatus=player.PlaybackStatus)
                    queue_putter((values, 0), mode=META)
            else:
                time.sleep(UNQUEUED_WAIT)
            counter += 1
            counter %= COUNT
            if player is not None and counter == COUNT-1 and oracle_serial == None:
                reconnect = True
                logging("Reconnecting")
                queue_putter("Reconnect", mode= SETUP)
        except queue.Full:
            pass
        except dbus.exceptions.DBusException:
            pass
        except Exception as e:
            logging("media_check: " + str(e))

def player_handler(*args, **kw):
    global queued_media
    global reconnect
    try:
        if meta_queue.qsize() < 5 and oracle_config['WallpaperMode'] is False:
            #logging("Player handler: " + str(args))
            reconnect = True
            queue_putter(args, mode=META)
        else:
            queued_media = True
            #logging("Queue full")
    except Exception as e: 
        queued_media = True
        logging(str(e))
        #logging("player_handler:" + str(e))

def wallpaper_handler():
    global images
    global wallpaper_num
    images = os.listdir(WALLPAPER_FOLDER)
    images = [(WALLPAPER_FOLDER + f) for f in images if os.path.isfile(WALLPAPER_FOLDER+f)]
    wallpaper_num = len(images)

def pause_wallpaper():
    pos_dur = bytearray(8)
    playing = 0
    queue_putter(dict(tag=DUR_POS_TAG, length=len(pos_dur), data=pos_dur, width=playing, height=1))

def next_wallpaper(direction=1):
    global wallpaper_index
    global wallpaper_timer
    wallpaper_index += direction
    wallpaper_index %= wallpaper_num
    thumb = images[wallpaper_index]
    logging("Loading next image: " + thumb)
    meta_text = oracle_config['WallpaperTitle'] + "\n" + oracle_config['WallpaperAlbum'] + "\n" + oracle_config['WallpaperArtist'] + "\n"
    meta_bytes = bytearray(meta_text, encoding='utf8')
    queue_putter(dict(tag=TEXT_TAG, length=len(meta_bytes), data=meta_bytes, width=0, height=0))

    #Position and duration
    pos_dur = bytearray(4)
    duration = oracle_config['WallpaperPeriod']*60
    pos_dur.extend(duration.to_bytes(4, 'little'))
    playing = 1
    queue_putter(dict(tag=DUR_POS_TAG, length=len(pos_dur), data=pos_dur, width=playing, height=1))

    with Image(filename=thumb) as img:
        img.transform(resize='304x304')
        img.background_color = wand_color('black')
        img.extent(304, 304, gravity='center')
        img.save(filename='thumby.png')
        pixels = img.export_pixels(channel_map="RGB")
        rgb565 = convert_to_565(pixels)
        queue_putter(dict(tag=IMG_TAG, length=len(rgb565), data=rgb565, width=img.width, height=img.height))
    
    if wallpaper_mode:
        wallpaper_timer = threading.Timer(interval=wallpaper_period, function=next_wallpaper)
        wallpaper_timer.start()

def serial_write_bytes():
    global reconnect
    while serial_writing:
        try:
            kwargs = writer_queue.get(timeout=1)
            if str(kwargs) == "Exit":
                writer_queue.task_done()
                return
            attemps = 0
            maxed_attemps = False
            global oracle_ready
            while not oracle_ready and not maxed_attemps and not exitting:
                if attemps >= MAX_ATTEMPTS:
                    logging("Tried to send " + CODES[kwargs['tag']-1] + " oracle timed out " + str(oracle_ready))
                    if reconnect:
                        queue_putter("Reconnect", mode=SETUP)
                        reconnect = False
                    maxed_attemps = True
                attemps+=1
                time.sleep(0.2)
            if maxed_attemps:
                writer_queue.task_done()
                continue
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
            if oracle_serial != None and oracle_serial.writable():
                oracle_serial.write(bytes_to_send)
            writer_queue.task_done()
        except queue.Empty:
            pass
        except serial.SerialException:
            reconnect = True
        except Exception as e:
            logging("serial_writer: " + str(e))


def session_changed(*args, **kwargs):
    global media_handler
    global status_signal
    global player
    sender = str(args[0])
    if "MediaPlayer2" in sender:
        for program in oracle_config['MonitoredProgram']:
            name = re.search(program, sender, flags=re.IGNORECASE)
            if name:
                logging("Session changed to " + sender)
                uri_selection(skip=True, sender_uri=sender)
    
def status_changed(*args, **kwargs):
    global bus
    global media_handler
    status_signal.remove()
    media_handler.remove()
    logging("Activating uri " + player_uri)
    media_handler = bus.add_signal_receiver(handler_function=player_handler, bus_name=player_uri, dbus_interface='org.freedesktop.DBus.Properties', sender_keyword="sender", destination_keyword="destination", interface_keyword="interface", member_keyword="member", path_keyword="path", message_keyword="msg")
    #logging("Status changed " + str(args) + "\n " + str(kwargs))
    queue_putter("Status changed", mode=SETUP)

def seeked(*args, **kwargs):
    global queued_media
    queued_media = True

def main_setup():
    global read_thread
    global meta_thread
    global meta_queue
    global writer_queue
    global writer_thread
    global setup_thread
    global wallpaper_timer
    global exitting
    global bus
    global seeked_signal
    global media_check_thread
    global media_handler
    global status_signal
    global old_config
    global player
    global oracle_config
    exitting = False
    read_thread = threading.Thread(target=serial_reader, name="SerialRead")
    logging("Start log", mode='w')
    logging("", mode='w', file_name=URI_FILE, time_stamp=False)
    meta_queue = queue.Queue(maxsize=5)
    DBusGMainLoop(set_as_default=True)
    bus = dbus.SessionBus()
    #status_signal = bus.add_signal_receiver(handler_function=status_changed, bus_name='org.mpris.MediaPlayer2.io.bassi.Amberol', path='/org/mpris/MediaPlayer2', dbus_interface='org.freedesktop.DBus.Properties', sender_keyword="sender", destination_keyword="destination", interface_keyword="interface", member_keyword="member", path_keyword="path", message_keyword="msg")
    #bus.add_signal_receiver(handler_function=status_changed, path='/org/mpris/MediaPlayer2', dbus_interface='org.freedesktop.DBus.Properties')
    #status_signal.remove()
    seeked_signal = bus.add_signal_receiver(handler_function=seeked, signal_name='Seeked', dbus_interface='org.mpris.MediaPlayer2.Player') 
    media_handler = bus.add_signal_receiver(handler_function=player_handler)
    media_handler.remove()
    status_signal = bus.add_signal_receiver(handler_function=status_changed)
    status_signal.remove()
    bus.add_signal_receiver(handler_function=session_changed, dbus_interface='org.freedesktop.DBus')

    old_config = None
    setup_thread = threading.Thread(target=general_setup)
    setup_thread.start()

    queue_putter("Main", mode=SETUP)
    meta_thread = threading.Thread(target=queue_handler, name="MetadataThread")
    meta_thread.start()

    writer_thread = threading.Thread(target=serial_write_bytes, name="WriterThread")
    writer_queue = queue.Queue(maxsize=1)
    writer_thread.start()

    media_check_thread = threading.Thread(target=media_check, name="MediaCheckThread")
    media_check_thread.start()

    wallpaper_timer = threading.Timer(interval=wallpaper_period, function=next_wallpaper)
    config_watcher()

#main_setup()
def main_exit():
    global meta_reading
    global serial_reading
    global setting_up
    global serial_writing
    global exitting

    exitting = True
    #meta_reading = False
    #serial_writing = False
    serial_reading = False
    setting_up = False
    logging("Exitting")
    print("main_exit")
    writer_queue.put("Exit")
    logging("Put writer exit")
    meta_queue.put("Exit")
    logging("Put meta exit")
    config_handler.setup_queue.put("Exit")
    logging("Put setup exit")
    #meta_queue.join()
    #writer_queue.join()
    #config_handler.setup_queue.join()
    logging("Joined queues")
    if media_check_thread.is_alive():
        media_check_thread.join()
    logging("Joined media_check")
    if read_thread.is_alive():
        read_thread.join()
    logging("Joined read")
    if setup_thread.is_alive():
        setup_thread.join()
    logging("Joined setup")
    if meta_thread.is_alive():
        logging("Joining meta")
        meta_thread.join()
    logging("Joined meta")
    if writer_thread.is_alive():
        writer_thread.join()
    logging("Joined write")
