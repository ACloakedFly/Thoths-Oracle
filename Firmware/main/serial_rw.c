/*
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
// serial_rw.c

//Handles all serial communication aside from inputs.
//The operating principle is that while we have no data coming in, we will periodically send a ready signal to the host (every ~8 seconds (not really important how often))
//When we receive data, we read the first few bytes, expecting them to be the header bytes that tell us about the incoming stream; what type of info, and how much to expect
//Depending on the tag of the data, and assuming nothing is funky about the header, we will place it, and all subsequent data in the correct arrays
//Once we have read frame_length bytes, we prepare ourselves for more data, then emit a ready signal.
//The host will be listening for those ready signals. No new data streams will be started until a ready signal is received. 
//So we know that any data we are receiving after the header will only be related to that header.

#include "data.h"

uint8_t album_cover[IMG_SIZE + RX_BUF_SIZE];//was 256
uint8_t album_bofer[BOFER_SIZE];

bool idle = true;

char name[TEXT_SIZE];
uint16_t name_counter = 0;
uint32_t frame_length = 0;
uint32_t img_counter = 0;
uint32_t song_duration = 1;
uint32_t song_position = 0;
uint32_t sys_time = 0;
uint8_t sys_date = 0;
uint8_t sys_month = 0;
uint32_t sys_year = 0;
uint8_t song_pos_bytes[8];
bool name_dirty = false, position_dirty = false, time_dirty = false, img_dirty = false;
char frame_header[HEADER_BYTES];
bool done_writing;
bool song_playing = false;
SemaphoreHandle_t info_mutex = NULL;
SemaphoreHandle_t img_mutex = NULL;
SemaphoreHandle_t date_time_mutex = NULL;

int length = 0;
uint16_t frame_width = 0;
uint16_t frame_height = 0;
uint32_t frame_duration = 0;
uint16_t read_status = 0, bofer_status = 0, bofer_counter = 0;

//Setup the serial communication. Should be USB OTG. It will communicate as fast as it can up to how fast the host communicates and <= USB full speed I think 
static int serial_setup(){
    usb_serial_jtag_driver_config_t serial_config = {
        .rx_buffer_size = RX_BUF_SIZE*3,
        .tx_buffer_size = TX_BUF_SIZE,
    };
    esp_err_t err = ESP_OK;
    err = usb_serial_jtag_driver_install(&serial_config);
    if(err != ESP_OK){
        printf("No serial for you :(\n)");
        return 0;
    }
    return 1;
}

//Break down the frame header bytes to know what to do with the data stream
static void frame_metadata(void *pvParameters){
    frame_length = (uint32_t)(frame_header[1] + (frame_header[2] << 8) + (frame_header[3] << 16));
    frame_width = (uint16_t)(frame_header[4] + (frame_header[5] << 8));
    frame_height = (uint16_t)(frame_header[6] + (frame_header[7] << 8));
    frame_duration = (uint32_t)(frame_header[8] + (frame_header[9] << 8) + (frame_header[10] << 16) + (frame_header[11] << 24)); 
    read_status = 0;
}

//We only run this while idling
static uint8_t frame_start(void){
    length = 0;
    name_counter = 0;
    frame_length = 0;
    read_status = 0;
    img_counter = 0;
    memset(album_bofer, 0, BOFER_SIZE);
    memset(frame_header, 0, HEADER_BYTES);
    length = usb_serial_jtag_read_bytes(frame_header, HEADER_BYTES, (TickType_t)portDelay);
    if(!length || length != HEADER_BYTES){//something went wrong no data in buffer
        idle = true;
        length = 0;
        memset(frame_header, 0, HEADER_BYTES);
        name_counter = 0;
        return 0;
    }
    //We have received at least 12 bytes, assume they are the header bytes. Figure out what to expect
    frame_metadata(NULL);
    //Echo what we received. Not neccessary, only useful fore debugging
    //char fd[71];
    //sprintf(fd, "Frame data: %d, %lu, %u, %u, %lu", frame_header[0], frame_length, frame_width, frame_height, frame_duration);
    //serial_jtag_write(6, fd, 71, portDelay);
    //Let superloop know that data is incoming
    idle = false;
    return 1;
}

//Call this at the end of the data frame to reset counters and go back to idling
static void frame_end(char *mes){
    idle = true;
    length = 0;
    name_counter = 0;
    frame_length = 0;
    img_counter = 0;
    memset(album_bofer, 0, BOFER_SIZE);
    memset(frame_header, 0, HEADER_BYTES);
    char tg[31];
    //Let host know that we finished, and where from. Location is useful for debugging
    sprintf(tg, "Finished from: %s", mes);
    serial_jtag_write(7, tg, 31, portDelay);
}

//Uh-oh. dump all the data received and wait for quiet period of 250ms to know host is done transmitting
static void catch_and_release(void){
    int* p = malloc(RX_BUF_SIZE);
    uint16_t garb_length = 1;
    while(garb_length != 0){
        garb_length = usb_serial_jtag_read_bytes(p, RX_BUF_SIZE, (TickType_t)portDelay*5);
    }
    free(p);
}

//Something went wrong with the header. We know more data is on it's way, so lets handle that and reset counters
static void error_reset(uint8_t erre){
    char mess[63];
    //Let host know something went wrong
    sprintf(mess, "Serial error, clearing cover buffer, error code: %d, tag: %d", erre, frame_header[0]);
    serial_jtag_write(8, mess, 63, portDelay);
    memset(album_cover, 0, IMG_SIZE);
    catch_and_release();
    length = 0;
    name_counter = 0;
    frame_length = 0;
    read_status = 0;
    img_counter = 0;
    bofer_counter = 0;
    //Ready up for more data, and remind host we just handled an error
    frame_end("Error reset");
}

//Check header for common errors, like nonsense tag or too much data. Reset if anything is wrong and return error code. Return 1 if everything is alright
static uint8_t error_check(void){
    if(frame_header[0] > 4 || frame_header[0] == 0){
        error_reset(2);
        return 2;
    }
    else if (frame_length > IMG_SIZE){
        error_reset(3);
        return 3;
    }
    else if(frame_header[0] == 1 && (frame_height != IMG_HEIGHT || frame_width != IMG_WIDTH)){
        error_reset(4);
        return 4;
    }
    else if (frame_header[0] == 2 && frame_length > TEXT_SIZE){
        error_reset(5);
        return 5;
    }
    else if (frame_header[0] == 3 && frame_length != 0){
        error_reset(6);
        return 6;
    }
    else if (frame_header[0] == 4 && frame_length != 8){
        error_reset(7);
        return 7;
    }
    return 1;
}

static void process_image(void){
    //This is our mutex. But we can not wait for it to be free. Data is coming too quick
    if(xSemaphoreTake(img_mutex, pdMS_TO_TICKS(10)) == pdTRUE){
        read_status = usb_serial_jtag_read_bytes(&album_cover[img_counter], RX_BUF_SIZE, portDelay);
        img_dirty = true;
        xSemaphoreGive(img_mutex);
        if(read_status == 0){
            frame_end("no image");
        }
        else{
            img_counter += read_status;
        }
        if(img_counter >= frame_length){
            frame_end("image");
        }
    }
    else{//might be a good idea to implement buffer if mutex is 
        serial_jtag_write(6, "Image packet missed, mutex blocked", 35, portDelay);
    }
}

static void process_text(void){
    if(xSemaphoreTake(info_mutex, portTICK_PERIOD_MS*20) == pdTRUE){
        read_status = usb_serial_jtag_read_bytes(&name[name_counter], frame_length, portDelay);
        name_counter += read_status;
        name_dirty = true;
        xSemaphoreGive(info_mutex);
    }
    if(name_counter >= frame_length){
        frame_end("text");
    }
}

static void process_sys_msg(void){
    char messy[34];
    if((frame_header[4] + (frame_header[5] << 8)) != 0){
        if(xSemaphoreTake(date_time_mutex, portTICK_PERIOD_MS*20) == pdTRUE){
            time_dirty = true;
            sys_date = frame_header[4];
            sys_month = frame_header[5];
            sys_year = frame_header[6] + (frame_header[7] << 8);
            sys_time = frame_header[8] + (frame_header[9] << 8) + (frame_header[10] << 16);
            sprintf(messy, "Received date: %d/%d/%lu", sys_date, sys_month, sys_year);
            serial_jtag_write(6, messy, 34, portDelay);
            xSemaphoreGive(date_time_mutex);
        }
    }
    frame_end("sys_msg");
}

static void process_dur_pos(void){
    char playing[72];
    if(xSemaphoreTake(info_mutex, portTICK_PERIOD_MS*20) == pdTRUE){
        memset(song_pos_bytes, 0, 8);
        usb_serial_jtag_read_bytes(&song_pos_bytes, 8, portDelay);
        song_duration = song_pos_bytes[4] + (song_pos_bytes[5] << 8) + (song_pos_bytes[6] << 16) + (song_pos_bytes[7] << 24);
        if(!(song_duration == 0 && frame_height == 0)){
            position_dirty = true;
        }
        song_position = song_pos_bytes[0] + (song_pos_bytes[1] << 8) + (song_pos_bytes[2] << 16) + (song_pos_bytes[3] << 24);
        song_position++;
        song_playing = frame_width;
        sprintf(playing, "Serial received play status: %u pos: %lu duration: %lu", frame_width, song_position, song_duration);
        serial_jtag_write(6, playing, 72, portDelay);
        xSemaphoreGive(info_mutex);
    }
    frame_end("dur_pos");
}

void serial_jtag_write(uint8_t msg_type, char *msg, uint16_t length, TickType_t ticks){
    char jtag_msg[512];
    memset(jtag_msg, 0, 512);
    sprintf(jtag_msg, "%c%s%c", msg_type, msg, '\n');
    usb_serial_jtag_write_bytes(jtag_msg, length+2, ticks);
}

void serial_task(void *pvParameters){
    serial_setup();
    name_counter = 0;
    memset(name, 0, 512);
    memset(frame_header, 0, HEADER_BYTES);
    done_writing = false;
    frame_end("start");
    uint16_t idle_counter = 0;
    char resetted[28];
    sprintf(resetted, "Last restart caused by: %d", (uint8_t)esp_reset_reason());
    serial_jtag_write(6, resetted, 28, portDelay);
    for(;;){
        if(idle){
            if(frame_start() != 1){//something went wrong or no data in buffer
                if(idle_counter == 0){
                    serial_jtag_write(7, "idling", 7, portDelay);
                }
                idle_counter++;//increments every ~5ms -> portDelay/portTICK_PERIOD_MS why's it actually closer to ~1s
                idle_counter%= 15;//emit message every 10s
                continue;
            }
        }//We now have frame metadata
        error_check();//If error it will wait until data transmission ends and clear buffers and counters
        //we now know no garbage is being sent and header makes sense
        if(frame_header[0] == 1){//Image
            process_image();
        }
        else if (frame_header[0] == 2){
            process_text();
        }
        else if (frame_header[0] == 3){
            process_sys_msg();
        }
        else if (frame_header[0] == 4){
            process_dur_pos();
        }
    }
    vTaskDelete(NULL);
}