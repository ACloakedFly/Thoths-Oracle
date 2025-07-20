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
// lvgl_ui.c

#include "data.h"

//Thumbnail image header
const lv_img_dsc_t img_cover_rgb = {
  .header.always_zero = 0,
  .header.w = IMG_WIDTH,
  .header.h = IMG_HEIGHT,
  .data_size = IMG_SIZE,
  .header.cf = LV_IMG_CF_TRUE_COLOR,
  .data = album_cover,
};

static lv_obj_t *l_album, *ld_album, *l_artist, *ld_artist, *l_title, *ld_title, *img_cover, *ld_dur,
 *ld_pos, *ld_bar, *ld_date, *ld_time, *ll_line;
static lv_disp_rot_t rotation = LV_DISP_ROT_NONE;
//char name_info[50];
bool updated = false;

//Array to hold song duration text ie hh:mm:ss
char song_dur[11];
//Array to hold song position text same as above
char song_pos[11];
//Array to hold date text dd/mm/yy, day and month order can be swapped from software side
char date_string[15];
//Array to hold current time ie hh:mm
char time_string[8];
//Song position counter and duration local to this thread
static uint32_t song_secs = 0, song_durs = 0;
//Last current time stored to compare against new time. Used to cut down on label updates 
static uint8_t sys_last_min = 0;
static sys_time ui_time = {0, 0, 0, 0};
//Timer for updating song position and system time
lv_timer_t *song_time;
//Bool for checking if song is playing, increment song position when true. Bool for updating song metadata only when text has changed
static bool song_play = false;
static bool pos_dirty = false;
//LVGL points for setting position of bar at the top of the screen
lv_point_t points[] = {{12,26}, {308,26}};

static void update_timer(lv_timer_t * timer);
//Styles for general objects
static lv_style_t style;

void lvgl_ui(lv_disp_t *disp)
{
    //LVGL display setup
    lv_obj_t *scr = lv_disp_get_scr_act(disp);
    lv_disp_set_bg_color(disp, lv_color_black());
    lv_disp_set_rotation(disp, rotation);

    //LVGL theming
    lv_style_init(&style);
    lv_style_set_bg_color(&style, lv_color_hex(0x000000));
    lv_style_set_text_color(&style, lv_color_hex(0xffffff));
    //lv_style_set_bg_color(&style_bar, lv_color_hex(0xbb00cc));
    
    //Set theme to the screen
    lv_obj_add_style(scr, &style, 0);

    ESP_LOGI(TAG, "LVGL base");
    //Setup of all the objects' positions, sizes, sources, scroll speeds, etc.
    //Title icon
    l_title = lv_img_create(scr);
    lv_img_set_src(l_title, &icon_title_rgb);
    lv_obj_align(l_title, LV_ALIGN_TOP_LEFT, 10, 44);

    //Title data text
    ld_title = lv_label_create(scr);
    lv_label_set_long_mode(ld_title, LV_LABEL_LONG_SCROLL_CIRCULAR);
    lv_obj_set_style_anim_speed(ld_title, 20, LV_PART_MAIN);//Used to slow down scroll speed
    lv_obj_set_width(ld_title, 286);
    lv_label_set_text(ld_title, "No Data");
    lv_obj_align(ld_title, LV_ALIGN_TOP_LEFT, 24, 42);
    
    //Album icon
    l_album = lv_img_create(scr);
    lv_img_set_src(l_album, &icon_album_rgb);
    lv_obj_align(l_album, LV_ALIGN_TOP_LEFT, 6, 72);

    //Album data text
    ld_album = lv_label_create(scr);
    lv_label_set_long_mode(ld_album, LV_LABEL_LONG_SCROLL_CIRCULAR);
    lv_obj_set_style_anim_speed(ld_album, 20, LV_PART_MAIN);
    lv_obj_set_width(ld_album, 286);
    lv_label_set_text(ld_album, "No Data");
    lv_obj_align(ld_album, LV_ALIGN_TOP_LEFT, 24, 70);

    //Artist icon
    l_artist = lv_img_create(scr);
    lv_img_set_src(l_artist, &icon_artist_rgb);
    lv_obj_align(l_artist, LV_ALIGN_TOP_LEFT, 8, 100);

    //Artist data text
    ld_artist = lv_label_create(scr);
    lv_label_set_long_mode(ld_artist, LV_LABEL_LONG_SCROLL_CIRCULAR);
    lv_obj_set_style_anim_speed(ld_artist, 20, LV_PART_MAIN);
    lv_obj_set_width(ld_artist, 286);
    lv_label_set_text(ld_artist, "No Data");
    lv_obj_align(ld_artist, LV_ALIGN_TOP_LEFT, 24, 98);

    //Album cover
    img_cover = lv_img_create(scr);
    lv_img_set_src(img_cover, &img_cover_rgb);
    lv_obj_align(img_cover, LV_ALIGN_CENTER, 0, 54);

    //Song duration
    ld_dur = lv_label_create(scr);
    lv_obj_set_width(ld_dur, 70);
    lv_label_set_text(ld_dur, "00:00:00");
    lv_obj_align(ld_dur, LV_ALIGN_TOP_LEFT, 234, 454);
    ld_pos = lv_label_create(scr);
    lv_obj_set_width(ld_pos, 70);
    lv_label_set_text(ld_pos, "00:00:00");
    lv_obj_align(ld_pos, LV_ALIGN_TOP_LEFT, 16, 454);
    ld_bar = lv_bar_create(scr);
    lv_obj_set_size(ld_bar, 304, 6);
    lv_obj_align(ld_bar, LV_ALIGN_TOP_LEFT, 8, 450);
    lv_bar_set_value(ld_bar, 70, LV_ANIM_OFF);
    lv_obj_set_style_bg_color(ld_bar, lv_color_hex(0xee00ff), LV_PART_INDICATOR);

    //Date and time
    ui_time.date = 26;
    ui_time.month = 5;
    ui_time.year = 2025;
    ui_time.seconds = 82000;
    ld_date = lv_label_create(scr);
    lv_obj_set_width(ld_date, 90);
    lv_label_set_text(ld_date, "26/05/2025");
    lv_obj_align(ld_date, LV_ALIGN_TOP_LEFT, 16, 4);
    ld_time = lv_label_create(scr);
    lv_obj_set_width(ld_time, 60);
    lv_label_set_text(ld_time, "22:13");
    lv_obj_align(ld_time, LV_ALIGN_TOP_LEFT, 258, 4);

    //Bar under date and time
    ll_line = lv_line_create(scr);
    lv_line_set_points(ll_line, points, 2);
    lv_style_set_line_width(&style, 3);
    lv_style_set_line_color(&style, lv_color_hex(0xbb00bb));
    lv_obj_add_style(ll_line, &style, 0);

    //Custom font declration and assigning it to all the text
    LV_FONT_DECLARE(dejavu_sans_16_phl);   
    lv_obj_set_style_text_font(ld_album, &dejavu_sans_16_phl, 0);
    lv_obj_set_style_text_font(ld_artist, &dejavu_sans_16_phl, 0);
    lv_obj_set_style_text_font(ld_title, &dejavu_sans_16_phl, 0);
    lv_obj_set_style_text_font(ld_dur, &dejavu_sans_16_phl, 0);
    lv_obj_set_style_text_font(ld_pos, &dejavu_sans_16_phl, 0);
    lv_obj_set_style_text_font(ld_time, &dejavu_sans_16_phl, 0);
    lv_obj_set_style_text_font(ld_date, &dejavu_sans_16_phl, 0);

    //Timer for updating song position and system time
    song_time = lv_timer_create(update_timer, 1000, NULL);
    lv_timer_set_repeat_count(song_time, -1);
    lv_timer_ready(song_time);

    ESP_LOGI(TAG, "LVGL Done, onto update");
}

//Text decoder
static void decode_unicode(){
    //Create array to hold text data. Having configurable char type is a hold over from messing around with UTF16
    CHAR_TYPE wide_data[TEXT_SIZE+1];
    //Ping info mutex with no block time. Arrays are written to from serial_task, which takes priority. So we are trying to be as fast as possible.
    //Copy the data into local fields and return the mutex ASAP. We'll process it later at our own pace.
    if(xSemaphoreTake(info_mutex, 0) == pdTRUE){   
        //If serial_task has not signalled that any of the values have changed, lets return the mutex, then exit the function. We have nothing to do anyways
        if(name_dirty == false){
            xSemaphoreGive(info_mutex);
            return;
        }
        //Data has changed, so we'll reset the flag set by serial_task
        name_dirty = false;
        //Copy the data into local fields and cast to uint8_t
        for(int i = 0; i < TEXT_SIZE; i += BITS){
            wide_data[i/BITS] = (uint8_t)name[i];
        }
        song_durs = song_duration;
        //Return the mutex. The rest we can do on our own copies without getting in the way of serial_task
        xSemaphoreGive(info_mutex);
    }
    else //We weren't able to acquire the mutex, no data has changed anyways, so we'll exit the function
        return;
    //If we have gotten this far, then we successfully acquired the mutex, data has changed and we have copied it.
    //Initialize some counters for keeping track of where we are in the wide_data array
    uint8_t song_index = 0;
    uint16_t artist_i = 0, album_i = 0;
    //Loop through all the bytes in the array, we're looking for new lines (UTF-8 number 10)
    for(int i = 0; i < TEXT_SIZE/BITS; i++){
        //Whenever we hit a new line, we replace it with a 0 to null terminate the string before it,
        //then record the proceeding index as the start of the next string (provided it's within range)
        if(wide_data[i] == 10){
            if(song_index == 0){
                song_index++;
                wide_data[i] = 0;
                album_i = i < TEXT_SIZE? i+1 : i;
            }
            else if(song_index == 1){
                song_index++;
                wide_data[i] = 0;
                artist_i = i < TEXT_SIZE? i+1 : i;
            }
            else{
                wide_data[i] = 0;
                break;
            }
        }
    }
    //Pass the address of the start of the strings that we found as the text to set the labels to.
    //Since the function expects a null terminated string, which we terminated ourselves, 
    //we effectively have three dynamically sized arrays for holding strings of any sizes less than 512 characters in total
    lv_label_set_text(ld_title, (char*)wide_data);
    lv_label_set_text(ld_album, (char*)(&wide_data[album_i]));
    lv_label_set_text(ld_artist, (char*)(&wide_data[artist_i]));
    //Zero out song duration array
    memset(song_dur, 0, 11);
    //Gotta be careful here, LVGL does not like bars with maximums of 0, so set it to 1 in that case. 
    if(song_durs != 0){
        //Construct our duration string by taking the total seconds and converting into hours, minutes component, and seconds component
        sprintf(song_dur, "%.2u:%.2u:%.2u", (uint8_t)(song_durs/3600), (uint8_t)((song_durs/60)%60), (uint8_t)(song_durs%60));
        lv_bar_set_range(ld_bar, 0, song_durs);
    }
    else{
        memcpy(song_dur, "00:00:00", 9);
        lv_bar_set_range(ld_bar, 0, 1);
    }
    lv_label_set_text(ld_dur, song_dur);
}

//Timer for updating song position and date/time
static void update_timer(lv_timer_t * timer){
    ui_time.seconds++;
    //Store the system minute separately. We don't display the seconds, so let's only update the label on a minute change
    uint8_t sys_min = (uint8_t)((ui_time.seconds/60)%60);
    //Only update the song position if the song is actually playing
    if(song_play || pos_dirty){
        song_secs++;
        pos_dirty = false;
        sprintf(song_pos, "%.2u:%.2u:%.2u", (uint8_t)(song_secs/3600), (uint8_t)((song_secs/60)%60), (uint8_t)(song_secs%60));
        lv_label_set_text(ld_pos, song_pos);
        lv_bar_set_value(ld_bar, song_secs, LV_ANIM_ON);
    }
    //Update time on minute change. Might as well do the date here too. No point for more frequent updates
    if(sys_min != sys_last_min){
        sprintf(time_string, "%.2u:%.2u", (uint8_t)(ui_time.seconds/3600), sys_min);
        lv_label_set_text(ld_time, time_string);
        sprintf(date_string, "%.2u/%.2u/%.4u", ui_time.date, ui_time.month, ui_time.year);
        lv_label_set_text(ld_date, date_string);
    }
    sys_last_min = sys_min;
}
//Really just copying the data, the decoding happens in update_timer
static void decode_timer(void*v){
    //Same as with text, ping mutex, grab data for ourselves if it has changed, return mutex
    if(xSemaphoreTake(info_mutex, 0) == pdTRUE){  
        song_play = song_playing;
        if(position_dirty){
            song_secs = song_position;
            position_dirty = false;
            pos_dirty = true;
        }        
        xSemaphoreGive(info_mutex);
    }
    if(xSemaphoreTake(date_time_mutex, 0) == pdTRUE){
        if(time_dirty){
            time_dirty = false;
            ui_time.date = system_time.date;
            ui_time.month = system_time.month;
            ui_time.seconds = system_time.seconds;
            ui_time.year = system_time.year;
        }
        xSemaphoreGive(date_time_mutex);
    }
}

//LVGL superloop
void UpdateInfo(void*v){
    while(1){
        lv_timer_handler();
        //Periodically handle text and date/time updates
        decode_unicode();
        decode_timer(NULL);
        if(xSemaphoreTake(img_mutex, 0) == pdTRUE){
            //This is a little bit different. If the image is dirty, we just remind LVGL where the image data is stored, and it will flush it to the display. No extra arrays needed
            if(img_dirty){
                img_dirty = false;
                lv_img_set_src(img_cover, &img_cover_rgb);
            }
            xSemaphoreGive(img_mutex);
        }
        //Delay calls until about the time of the next screen refresh. Faster may improve smoothness, or harm it. Slower will definitely reduce the framerate
        vTaskDelay(pdMS_TO_TICKS(LVGL_HANDLER_PERIOD_MS));
    }
}