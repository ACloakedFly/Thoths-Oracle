#include "data.h"

uint8_t album_cover[IMG_SIZE + RX_BUF_SIZE];//was 256

bool idle = true;

char name[TEXT_SIZE];
//char name_buffer[512];
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
char frame_garbage[HEADER_BYTES];
bool done_writing;
bool song_playing = false;
SemaphoreHandle_t info_mutex = NULL;
SemaphoreHandle_t img_mutex = NULL;
SemaphoreHandle_t date_time_mutex = NULL;

int length = 0;
uint16_t frame_width = 0;
uint16_t frame_height = 0;
uint32_t frame_duration = 0;
uint16_t read_status = 0;

void serial_setup(){
    usb_serial_jtag_driver_config_t serial_config = {
        .rx_buffer_size = RX_BUF_SIZE,
        .tx_buffer_size = TX_BUF_SIZE,
    };
    esp_err_t err = ESP_OK;
    err = usb_serial_jtag_driver_install(&serial_config);
    if(err != ESP_OK){
        printf("No serial for you :(\n)");
        return;
    }
    printf("Serial!\n)");
}

void frame_metadata(void *pvParameters){
    frame_length = frame_header[1] + (frame_header[2] << 8) + (frame_header[3] << 16);
    frame_width = frame_header[4] + (frame_header[5] << 8);
    frame_height = frame_header[6] + (frame_header[7] << 8);
    frame_duration = frame_header[8] + (frame_header[9] << 8) + (frame_header[10] << 16) + (frame_header[11] << 24); 
    read_status = 0;
}

int frame_start(void){
    length = 0;
    name_counter = 0;
    frame_length = 0;
    read_status = 0;
    img_counter = 0;
    memset(frame_header, 0, HEADER_BYTES);
    length = usb_serial_jtag_read_bytes(frame_header, HEADER_BYTES, (TickType_t)portDelay);
    if(!length || length != HEADER_BYTES){//something went wrong no data in buffer
        idle = true;
        length = 0;
        memset(frame_header, 0, HEADER_BYTES);
        name_counter = 0;
        return 0;
    }
    frame_metadata(NULL);
    char fd[71];
    sprintf(fd, "Frame data: %d, %lu, %u, %u, %lu", frame_header[0], frame_length, frame_width, frame_height, frame_duration);
    serial_jtag_write(6, fd, 71, portDelay);
    idle = false;
    return 1;
}

void frame_end(){
    idle = true;
    length = 0;
    name_counter = 0;
    frame_length = 0;
    img_counter = 0;
    serial_jtag_write(7, "Finished", 9, portDelay);
}

void catch_and_release(void){
    int* p = malloc(RX_BUF_SIZE);
    uint16_t garb_length = 1;
    while(garb_length != 0){
        garb_length = usb_serial_jtag_read_bytes(p, RX_BUF_SIZE, (TickType_t)portDelay*5);
    }
    free(p);
}

void error_reset(uint8_t erre){
    char mess[63];
    sprintf(mess, "Serial error, clearing cover buffer, error code: %d, tag: %d", erre, frame_header[0]);
    serial_jtag_write(8, mess, 63, portDelay);
    memset(album_cover, 0, IMG_SIZE);
    catch_and_release();
    length = 0;
    name_counter = 0;
    frame_length = 0;
    read_status = 0;
    img_counter = 0;
    frame_end();
}

uint8_t error_check(void){
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

void process_image(void){
    if(xSemaphoreTake(img_mutex, portTICK_PERIOD_MS*20) == pdTRUE){
        read_status = usb_serial_jtag_read_bytes(&album_cover[img_counter], RX_BUF_SIZE, portDelay);
        img_dirty = true;
        xSemaphoreGive(img_mutex);
        if(read_status == 0){
            frame_end();
        }
        else{
            img_counter += read_status;
        }
        if(img_counter >= frame_length){
            frame_end();
        }
    }
}

void process_text(void){
    if(xSemaphoreTake(info_mutex, portTICK_PERIOD_MS*20) == pdTRUE){
        read_status = usb_serial_jtag_read_bytes(&name[name_counter], frame_length, portDelay);
        //song_duration = frame_duration;
        name_counter += read_status;
        name_dirty = true;
        xSemaphoreGive(info_mutex);
    }
    if(name_counter >= frame_length){
        frame_end();
    }
}

void process_sys_msg(void){
    frame_end();
    if((frame_header[4] + (frame_header[5] << 8)) != 0){
        if(xSemaphoreTake(date_time_mutex, portTICK_PERIOD_MS*20) == pdTRUE){
            time_dirty = true;
            sys_date = frame_header[4];
            sys_month = frame_header[5];
            sys_year = frame_header[6] + (frame_header[7] << 8);
            sys_time = frame_header[8] + (frame_header[9] << 8) + (frame_header[10] << 16);
            xSemaphoreGive(date_time_mutex);
        }
    }
}

void process_dur_pos(void){
    char playing[72];
    if(xSemaphoreTake(info_mutex, portTICK_PERIOD_MS*20) == pdTRUE){
        position_dirty = true;
        song_position = usb_serial_jtag_read_bytes(&song_pos_bytes, 8, portDelay);
        song_position = song_pos_bytes[0] + (song_pos_bytes[1] << 8) + (song_pos_bytes[2] << 16) + (song_pos_bytes[3] << 24);
        song_position++;
        song_duration = song_pos_bytes[4] + (song_pos_bytes[5] << 8) + (song_pos_bytes[6] << 16) + (song_pos_bytes[7] << 24);
        song_playing = frame_width;
        xSemaphoreGive(info_mutex);
    }
    sprintf(playing, "Serial received play status: %u pos: %lu duration: %lu", frame_width, song_position, song_duration);
    serial_jtag_write(6, playing, 72, portDelay);
    frame_end();
}

void serial_jtag_write(uint8_t msg_type, char *msg, uint16_t length, TickType_t ticks){
    char jtag_msg[512];
    memset(jtag_msg, 0, 512);
    sprintf(jtag_msg, "%c%s%c", msg_type, msg, '\n');
    usb_serial_jtag_write_bytes(jtag_msg, length+2, ticks);
}

void serial_task(void *pvParameters){
    memset(frame_header, 0, HEADER_BYTES);
    name_counter = 0;
    memset(name, 0, 512);
    done_writing = false;
    frame_end();
    uint16_t idle_counter = 0;
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