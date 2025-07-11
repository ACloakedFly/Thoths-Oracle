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

CHAR_TYPE song_title[CHARS];
CHAR_TYPE song_album[CHARS];
CHAR_TYPE song_artist[CHARS];
char song_dur[11];
char song_pos[11];
char date_string[15];
char time_string[8];
static uint32_t song_secs = 0;
static uint8_t sys_last_min = 0;
//System day of the month
static uint8_t system_date = 0;
//System month
static uint8_t system_month = 0;
//System year
static uint16_t system_year = 0;
//System time in seconds
static uint32_t system_time = 0;
lv_timer_t *song_time, *ref_time;
static bool song_play = false, text_dirty = false;

lv_point_t points[] = {{12,26}, {308,26}};

static void update_timer(lv_timer_t * timer);

static lv_style_t style, style_bar;

void lvgl_ui(lv_disp_t *disp)
{

    lv_obj_t *scr = lv_disp_get_scr_act(disp);
    lv_disp_set_bg_color(disp, lv_color_black());
    lv_disp_set_rotation(disp, rotation);

    lv_style_init(&style);
    lv_style_set_bg_color(&style, lv_color_hex(0x000000));
    lv_style_set_text_color(&style, lv_color_hex(0xffffff));
    lv_style_set_bg_color(&style_bar, lv_color_hex(0xbb00cc));
    
    lv_obj_add_style(scr, &style, 0);

    ESP_LOGI(TAG, "LVGL base");
    //Title icon
    l_title = lv_img_create(scr);
    lv_img_set_src(l_title, &icon_title_rgb);
    lv_obj_align(l_title, LV_ALIGN_TOP_LEFT, 10, 44);

    //Title data text
    ld_title = lv_label_create(scr);
    lv_label_set_long_mode(ld_title, LV_LABEL_LONG_SCROLL_CIRCULAR);
    lv_obj_set_style_anim_speed(ld_title, 20, LV_PART_MAIN);
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
    //lv_img_set_zoom(img_cover, 360);
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
    lv_obj_add_style(ld_bar, &style_bar, 0);
    lv_obj_set_style_bg_color(ld_bar, lv_color_hex(0xee00ff), LV_PART_INDICATOR);

    //Date and time
    system_date = 26;
    system_month = 5;
    system_year = 2025;
    system_time = 82000;
    ld_date = lv_label_create(scr);
    lv_obj_set_width(ld_date, 90);
    lv_label_set_text(ld_date, "26/05/2025");
    lv_obj_align(ld_date, LV_ALIGN_TOP_LEFT, 16, 4);
    ld_time = lv_label_create(scr);
    lv_obj_set_width(ld_time, 60);
    lv_label_set_text(ld_time, "22:13");
    lv_obj_align(ld_time, LV_ALIGN_TOP_LEFT, 258, 4);

    ll_line = lv_line_create(scr);
    lv_line_set_points(ll_line, points, 2);
    lv_style_set_line_width(&style, 3);
    lv_style_set_line_color(&style, lv_color_hex(0xbb00bb));
    lv_obj_add_style(ll_line, &style, 0);

       
    LV_FONT_DECLARE(dejavu_sans_16_phl);   
    lv_obj_set_style_text_font(ld_album, &dejavu_sans_16_phl, 0);
    lv_obj_set_style_text_font(ld_artist, &dejavu_sans_16_phl, 0);
    lv_obj_set_style_text_font(ld_title, &dejavu_sans_16_phl, 0);
    lv_obj_set_style_text_font(ld_dur, &dejavu_sans_16_phl, 0);
    lv_obj_set_style_text_font(ld_pos, &dejavu_sans_16_phl, 0);
    lv_obj_set_style_text_font(ld_time, &dejavu_sans_16_phl, 0);
    lv_obj_set_style_text_font(ld_date, &dejavu_sans_16_phl, 0);

    song_time = lv_timer_create(update_timer, 1000, NULL);
    lv_timer_set_repeat_count(song_time, -1);
    lv_timer_ready(song_time);

    ESP_LOGI(TAG, "LVGL Done, onto update");
}

static void decode_unicode(uint16_t bytes){
    CHAR_TYPE wide_data[bytes/BITS+1];
    wide_data[bytes/BITS] = 0;
    memset(song_title, 0, CHARS);
    memset(song_album, 0, CHARS);
    memset(song_artist, 0, CHARS);

    if(xSemaphoreTake(info_mutex, 0) == pdTRUE){     
        if(name_dirty == false){
            xSemaphoreGive(info_mutex);
            return;
        }
        else
            name_dirty = false;
        text_dirty = true;
        for(int i = 0; i < bytes; i += BITS){
            wide_data[i/BITS] = (uint8_t)name[i];
        }  
        memset(song_dur, 0, 11);
        if(song_duration != 0){
            sprintf(song_dur, "%.2u:%.2u:%.2u", (uint8_t)(song_duration/3600), (uint8_t)((song_duration/60)%60), (uint8_t)(song_duration%60));
            lv_bar_set_range(ld_bar, 0, song_duration);
        }
        else{
            memcpy(song_dur, "00:00:00", 9);
            lv_bar_set_range(ld_bar, 0, 1);
        }
        xSemaphoreGive(info_mutex);
    }
    //printf("\nESP unicode decoded says: '%ls'\n", wide_data);
    uint8_t song_index = 0, album_i = 0, title_i = 0;//, artist_i = 0;
    for(int i = 0; i < bytes/BITS; i++){
        if(wide_data[i] == 10){
            switch (song_index){
                case 0:
                    char_cpy(song_title, wide_data, i);
                    song_index++;
                    title_i = i;
                    break;
                case 1:
                    char_cpy(song_album, &wide_data[title_i+1], i-title_i-1);
                    album_i = i;
                    song_index++;
                    break;
                case 2:
                    char_cpy(song_artist, &wide_data[album_i+1], i-album_i-1);
                    //artist_i = i;
                    song_index++;
                    break;
                default:
                break;
            }
        }
    }
    if(text_dirty){
        lv_label_set_text(ld_title, (char*)song_title);
        lv_label_set_text(ld_album, (char*)song_album);
        lv_label_set_text(ld_artist, (char*)song_artist);
        lv_label_set_text(ld_dur, song_dur);
        printf("Text dirty\n");
        text_dirty = false;
    }
}

static void update_timer(lv_timer_t * timer){
    system_time++;
    uint8_t sys_min = (uint8_t)((system_time/60)%60);
    if(song_play){
        song_secs++;
        sprintf(song_pos, "%.2u:%.2u:%.2u", (uint8_t)(song_secs/3600), (uint8_t)((song_secs/60)%60), (uint8_t)(song_secs%60));
        lv_label_set_text(ld_pos, song_pos);
        lv_bar_set_value(ld_bar, song_secs, LV_ANIM_ON);
    }
    if(sys_min != sys_last_min){
        sprintf(time_string, "%.2u:%.2u", (uint8_t)(system_time/3600), sys_min);
        lv_label_set_text(ld_time, time_string);
        sprintf(date_string, "%.2u/%.2u/%.4u", system_date, system_month, system_year);
        lv_label_set_text(ld_date, date_string);
    }
    sys_last_min = sys_min;
}

static void decode_timer(void*v){
    
    if(xSemaphoreTake(info_mutex, 0) == pdTRUE){  
        song_play = song_playing;
        if(position_dirty){
            song_secs = song_position;
            position_dirty = false;
        }        
        xSemaphoreGive(info_mutex);
    }
    if(xSemaphoreTake(date_time_mutex, 0) == pdTRUE){
        if(time_dirty){
            time_dirty = false;
            system_date = sys_date;
            system_month = sys_month;
            system_time = sys_time;
            system_year = sys_year;
        }
        xSemaphoreGive(date_time_mutex);
    }
}

void UpdateInfo(void*v){
    while(1){
        lv_timer_handler();
        decode_unicode(512);
        decode_timer(NULL);
        if(xSemaphoreTake(img_mutex, 0) == pdTRUE){
            if(img_dirty){
                img_dirty = false;
                lv_img_set_src(img_cover, &img_cover_rgb);
            }
            xSemaphoreGive(img_mutex);
        }
        vTaskDelay(pdMS_TO_TICKS(LVGL_HANDLER_PERIOD_MS));
    }
}