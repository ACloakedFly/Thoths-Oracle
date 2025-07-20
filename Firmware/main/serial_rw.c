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

bool idle = true;

char name[TEXT_SIZE];
char jtag_msg[TEXT_SIZE];
uint16_t name_counter = 0;
uint32_t img_counter = 0;
uint32_t song_duration = 1;
uint32_t song_position = 0;
uint8_t song_pos_bytes[DUR_POS_BYTES];
bool name_dirty = false, position_dirty = false, time_dirty = false, img_dirty = false;
bool song_playing = false;
SemaphoreHandle_t info_mutex = NULL;
SemaphoreHandle_t img_mutex = NULL;
SemaphoreHandle_t date_time_mutex = NULL;

int length = 0;
char header_buffer[HEADER_BYTES];
uint16_t read_status = 0;

typedef struct frame_header{
    uint8_t tag;
    uint32_t length;
    uint16_t width;
    uint16_t height;
    uint32_t duration;
}frame_header;

frame_header header = {0, 0, 0, 0, 0};
static const frame_header empty_header = {0, 0, 0, 0, 0};

sys_time system_time = {0, 0, 0, 0};

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
    header.tag = header_buffer[0];
    header.length = (uint32_t)(header_buffer[1] + (header_buffer[2] << 8) + (header_buffer[3] << 16));
    header.width = (uint16_t)(header_buffer[4] + (header_buffer[5] << 8));
    header.height = (uint16_t)(header_buffer[6] + (header_buffer[7] << 8));
    header.duration = (uint32_t)(header_buffer[8] + (header_buffer[9] << 8) + (header_buffer[10] << 16) + (header_buffer[11] << 24)); 
    read_status = 0;
}

//We only run this while idling
static uint8_t frame_start(void){
    length = 0;
    name_counter = 0;
    img_counter = 0;
    header = empty_header;
    memset(header_buffer, 0, HEADER_BYTES);
    //Grab the first HEADER_BYTES bytes. These should be the header bytes, everything after will be data bytes
    length = usb_serial_jtag_read_bytes(header_buffer, HEADER_BYTES, (TickType_t)portDelay);
    if(length == 0 || length != HEADER_BYTES){//something went wrong or no data in buffer
        idle = true;
        length = 0;
        memset(header_buffer, 0, HEADER_BYTES);
        name_counter = 0;
        return 0;
    }
    frame_metadata(NULL);
    //Let superloop know that data is incoming
    idle = false;
    return 1;
}

//Call this at the end of the data frame to reset counters and go back to idling
static void frame_end(char *end_mes){
    idle = true;
    memset(header_buffer, 0, HEADER_BYTES);
    header = empty_header;
    char mes[32] = {0};
    //Let host know that we finished, and where from. Location is useful for debugging
    sprintf(mes, "Finished from: %s", end_mes);
    serial_jtag_write(FINISHED_TAG, mes, 31, portDelay);
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
    char mes[64] = {0};
    //Let host know something went wrong
    sprintf(mes, "Serial error, clearing cover buffer, error code: %d, tag: %d", erre, header.tag);
    serial_jtag_write(ERROR_TAG, mes, 63, portDelay);
    memset(album_cover, 0, IMG_SIZE);
    catch_and_release();
    //Ready up for more data, and remind host we just handled an error
    frame_end("Error reset");
}

//Check header for common errors, like nonsense tag or too much data. Reset if anything is wrong and return error code. Return 1 if everything is alright
static uint8_t error_check(void){
    if(header.tag > DUR_POS_TAG || header.tag == 0){
        error_reset(2);
        return 2;
    }
    else if (header.length > IMG_SIZE){
        error_reset(3);
        return 3;
    }
    else if(header.tag == IMG_TAG && (header.height != IMG_HEIGHT || header.width != IMG_WIDTH)){
        error_reset(4);
        return 4;
    }
    else if (header.tag == TEXT_TAG && header.length > TEXT_SIZE){
        error_reset(5);
        return 5;
    }
    else if (header.tag == SYS_MSG_TAG && header.length != 0){
        error_reset(6);
        return 6;
    }
    else if (header.tag == DUR_POS_TAG && header.length != DUR_POS_BYTES){
        error_reset(7);
        return 7;
    }
    return 1;
}

static void process_image(void){
    //This is our mutex. But we can not wait for it to be free. Data is coming too quick. We might as well wait since we are not expecting anything else
    //The RX buffer will and empty properly without additional buffering. At least it has in testing
    if(xSemaphoreTake(img_mutex, pdMS_TO_TICKS(10)) == pdTRUE){
        read_status = usb_serial_jtag_read_bytes(&album_cover[img_counter], RX_BUF_SIZE, portDelay);
        img_dirty = true;//Set dirty flag so ui_task knows that we changed the array
        xSemaphoreGive(img_mutex);
        if(read_status == 0){//No data read. That's weird. We are only here because an image header was sent to us. Image data should have been next
            frame_end("no image");
        }
        else{//Increment img_counter so that on the next pass we know where in the array to place the data
            img_counter += read_status;
        }
        if(img_counter >= header.length){//We have read the expected number of bytes, or a little more (uh-oh). Ready up for new frame and let host know
            frame_end("image");
        }
    }
    else{
        serial_jtag_write(INFO_TAG, "Image packet missed, mutex blocked\0", 35, portDelay);
    }
}

//Same as process_image, copy data from rx buffer, increment name_counter, return mutex
//We don't need to zero out the name array since we null terminate the strings in ui_task
static void process_text(void){
    if(xSemaphoreTake(info_mutex, portTICK_PERIOD_MS*20) == pdTRUE){
        read_status = usb_serial_jtag_read_bytes(&name[name_counter], header.length, portDelay);
        name_counter += read_status;
        name_dirty = true;//Set dirty flag so ui_task knows that we changed the array
        xSemaphoreGive(info_mutex);
    }
    //Way less data is sent, it should all fit into the rx buffer, so less safeties are needed
    if(name_counter >= header.length || read_status == 0){//We have read the expected number of bytes, or a little more or none (uh-oh). Ready up for new frame and let host know
        frame_end("text");
    }
}

//This one is a little different, not a lot of data is sent, so it is all packed into the header itself
static void process_sys_msg(void){
    char mes[34] = {0};
    if(header.width != 0){//Let's just make sure the date and month aren't 0. No point in updating to a nonsense date
        //Grab mutex, copy data from the header into the shared fields, then return mutex
        if(xSemaphoreTake(date_time_mutex, portTICK_PERIOD_MS*20) == pdTRUE){
            time_dirty = true;//Set dirty flag so ui_task knows that we changed the array
            system_time.date = (uint8_t)(header.width & 255);//header_buffer[4];
            system_time.month = header.width >> 8;//header_buffer[5];
            system_time.year = header.height;
            system_time.seconds = header.duration;
            sprintf(mes, "Received date: %d/%d/%u", system_time.date, system_time.month, system_time.year);
            serial_jtag_write(INFO_TAG, mes, 34, portDelay);
            xSemaphoreGive(date_time_mutex);
        }
    }
    //No more sys_msg data is sent past the frame_header, so no need to count any bytes received
    frame_end("sys_msg");//Ready up and let host know we are ready.
}

//Mostly the same as process_sys_msg but with larger integers. Keeping separate stream simplified the code here, and what host needs to send
static void process_dur_pos(void){
    char mes[73] = {0};
    if(xSemaphoreTake(info_mutex, portTICK_PERIOD_MS*20) == pdTRUE){
        memset(song_pos_bytes, 0, DUR_POS_BYTES);
        read_status = usb_serial_jtag_read_bytes(&song_pos_bytes, DUR_POS_BYTES, portDelay);
        song_duration = song_pos_bytes[4] + (song_pos_bytes[5] << 8) + (song_pos_bytes[6] << 16) + (song_pos_bytes[7] << 24);
        //Some applications do not broadcast a song duration or position. In that case, we don't want to reset the position everytime the play status changes (ie paused/unpaused)
        if(!(song_duration == 0 && header.height == 0)){
            //When the song duration is 0 and the frame_height is 0, we know that the host is telling us that the song was only paused/unpaused and no position will be sent
            //So we won't let ui_task know that anything changed. ui_task handles incrementing the position. 
            //If we tell ui_task the position has changed, it will reset it to zero everytime we unpause since we have no accurate position here (it's 0)
            //Otherwise, we have a duration and accurate position, or the track changed, 
            //so we will want to update the position, or reset it to 0 
            position_dirty = true;
        }
        song_position = song_pos_bytes[0] + (song_pos_bytes[1] << 8) + (song_pos_bytes[2] << 16) + (song_pos_bytes[3] << 24);
        //Song position seems to lag a little, so we give it a boost here. Not really sure if it makes it more accurate on aaverage, but feels better
        song_position++;
        //Host sends play status in the frame_header
        song_playing = header.width;
        sprintf(mes, "Serial received play status: %u pos: %lu duration: %lu", header.width, song_position, song_duration);
        serial_jtag_write(INFO_TAG, mes, 72, portDelay);
        xSemaphoreGive(info_mutex);
    }
    //We have read the expected number of bytes, or a little more/none (uh-oh). Ready up for new frame and let host know
    if(read_status >= DUR_POS_BYTES || read_status == 0)
        frame_end("dur_pos");
}

//Serial writer helper
//Append provided string of length length to msg_type, then a new line to the end, wait ticks for TX buffer to be available
void serial_jtag_write(uint8_t msg_type, char *msg, uint16_t length, TickType_t ticks){
    memset(jtag_msg, 0, TEXT_SIZE);
    sprintf(jtag_msg, "%c%s%c", msg_type, msg, 10);
    usb_serial_jtag_write_bytes(jtag_msg, length+2, ticks);
}

//Serial superloop
void serial_task(void *pvParameters){
    //Install the driver
    serial_setup();
    //Zero out counters and arrays
    name_counter = 0;
    memset(name, 0, TEXT_SIZE);
    memset(header_buffer, 0, HEADER_BYTES);
    header = empty_header;
    frame_end("start");
    uint16_t idle_counter = 0;
    //Report the reason for the last restart, useful for debugging
    char mes[29] = {0};
    sprintf(mes, "Last restart caused by: %d", (uint8_t)esp_reset_reason());
    serial_jtag_write(INFO_TAG, mes, 28, portDelay);
    for(;;){
        //If we're idling, wait for data. When frame_start returns 1, we will no longer be idling
        if(idle){
            if(frame_start() != 1){//something went wrong or no data in buffer
                if(idle_counter == 0){//Periodically emit idling status message to host
                    serial_jtag_write(STATUS_TAG, "idling\0", 7, portDelay);
                }
                idle_counter++;
                idle_counter%= 15;//emit message every ~8s, timing not critical
                continue;//Skip everything below. No data was received anyways
            }
        }//We now have frame metadata
        error_check();
        if(header.tag == IMG_TAG){
            process_image();
        }
        else if (header.tag == TEXT_TAG){
            process_text();
        }
        else if (header.tag == SYS_MSG_TAG){
            process_sys_msg();
        }
        else if (header.tag == DUR_POS_TAG){
            process_dur_pos();
        }
    }
}