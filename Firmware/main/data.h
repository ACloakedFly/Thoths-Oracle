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
// data.h

#include <string.h>
#include <stdio.h>
#include <stddef.h>
#include <stdlib.h>
#include <stdint.h>
#include <wchar.h>
#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"
#include "freertos/task.h"
#include "freertos/queue.h"
#include "esp_err.h"
#include "esp_log.h"
#include "esp_timer.h"
#include "esp_lcd_panel_io.h"
#include "esp_lcd_panel_vendor.h"
#include "esp_lcd_panel_ops.h"
#include "driver/gpio.h"
#include "driver/spi_master.h"
#include "driver/usb_serial_jtag.h"
#include "driver/ledc.h"
#include "lvgl.h"
#include "button_gpio.h"
#include "iot_button.h"
#include "esp_sleep.h"
#include "driver/pulse_cnt.h"
#include "esp_lcd_ili9341.h"

#define UTF8 1
#define UTF16 2
#define UTF32 4
#define BITS UTF8
#define CHARS 100
#define HEADER_BYTES 12

#define portDelay 50
#define BAUD_RATE 921600//115200
#define RX_BUF_SIZE (1024)
#define TX_BUF_SIZE (512)

//Inputs
#define PREV_PIN 7//4
#define MUTE_PIN 2//22
#define PLAY_PIN 8//4//18
#define NEXT_PIN 6//5//23
#define ENC_EDGE_PIN 4//6//13
#define ENC_LEVEL_PIN 5//7//14

//Display
#define PIN_NUM_SCLK        11//8//32//17//   GPIO_NUM_18
#define PIN_NUM_MOSI        10//9//33//18//   GPIO_NUM_19
#define PIN_NUM_LCD_DC      9//10//25//19//   GPIO_NUM_17//5 or 17
#define PIN_NUM_LCD_RST     12//11//26//21//   GPIO_NUM_22//3 is used as rx for uart
#define PIN_NUM_LCD_CS      13//12//27//22//   GPIO_NUM_4
#define PIN_NUM_MISO        -1//Unuse

#define IMG_WIDTH 304 //160, 224, 304
#define IMG_HEIGHT 304//160, 224, 304
#define IMG_SIZE IMG_HEIGHT*IMG_WIDTH*2//65536//140*140*3

#define TEXT_SIZE 512

#if BITS == 1
#define CHAR_SIZE uint8_t
#define CHAR_TYPE unsigned char
#define char_cpy memcpy
#elif BITS == 2
#define CHAR_SIZE uint16_t
#define CHAR_TYPE wchar_t
#define char_cpy wmemcpy
#else
#define CHAR_SIZE uint32_t
#define CHAR_TYPE wchar_t
#define char_cpy wmemcpy
#endif


extern const char TAG[3];
extern char name[TEXT_SIZE];
//extern CHAR_TYPE song_title[CHARS];
//extern CHAR_TYPE song_album[CHARS];
//extern CHAR_TYPE song_artist[CHARS];
extern bool updated;
extern uint8_t album_cover[IMG_SIZE + RX_BUF_SIZE];//was 256
extern uint8_t width, height;
extern uint32_t cover_bytes;
extern uint32_t song_duration;
extern uint32_t song_position;
extern uint32_t sys_time;
extern uint8_t sys_date;
extern uint8_t sys_month;
extern uint32_t sys_year;
extern bool name_dirty;
extern bool position_dirty;
extern bool time_dirty;
extern bool img_dirty;
extern bool song_playing;

extern SemaphoreHandle_t info_mutex;
extern SemaphoreHandle_t img_mutex;
extern SemaphoreHandle_t date_time_mutex;

//extern const lv_img_dsc_t img_cover_rgb;

extern const uint8_t icon_title[352];
extern const lv_img_dsc_t icon_title_rgb;

extern const uint8_t icon_album[512];
extern const lv_img_dsc_t icon_album_rgb;

extern const uint8_t icon_artist[416];
extern const lv_img_dsc_t icon_artist_rgb;

//LCD
#define LCD_HOST  SPI2_HOST

#define LVGL_TICK_PERIOD_MS    2//2
#define LVGL_HANDLER_PERIOD_MS    20//2

#define LCD_PIXEL_CLOCK_HZ     (1000/LVGL_HANDLER_PERIOD_MS)*480*320
#define LCD_BK_LIGHT_ON_LEVEL  1
#define LCD_BK_LIGHT_OFF_LEVEL !LCD_BK_LIGHT_ON_LEVEL


// The pixel number in horizontal and vertical
#define LCD_H_RES              320
#define LCD_V_RES              480
#define LCD_BUF 42

// Bit number used to represent command and parameter
#define LCD_CMD_BITS           8
#define LCD_PARAM_BITS         8



//Functions
extern void lvgl_ui(lv_disp_t *disp);
extern void inputs_main();
extern void serial_task(void *pvParameters);
//extern void serial_setup();
extern void ui_setup(void *pvParameters);
extern void UpdateInfo(void*v);
extern void timer_incer(void *pvParameters);
extern void serial_jtag_write(uint8_t msg_type, char *msg, uint16_t length, TickType_t ticks);