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
// inputs.c

#include "data.h"

//uint8s for storing input commands
char cmd_vol_dn[2]      = {CMD_VAL_VOL_DN, 0};
char cmd_vol_up[2]      = {CMD_VAL_VOL_UP, 0};
char cmd_mute[2]        = {CMD_VAL_MUTE, 0};
char cmd_prev[2]        = {CMD_VAL_PREV, 0};
char cmd_play_pause[2]  = {CMD_VAL_PLAY_PAUSE, 0};
char cmd_next[2]        = {CMD_VAL_NEXT, 0};

//Button config, reused for simplicity
button_config_t gpio_btn_cfg = {
    .type = BUTTON_TYPE_GPIO,//Same for all buttons
    .long_press_time = CONFIG_BUTTON_LONG_PRESS_TIME_MS,//Same for all buttons, not used currently
    .short_press_time = CONFIG_BUTTON_SHORT_PRESS_TIME_MS,//Same for all buttons, not used currently
    .gpio_button_config = {
        .gpio_num = 33,//Updated immediately, so not important what we set it to here
        .active_level = 0,//All the buttons are pullups, so active is low
    },
};

//Handle reused for simplicity, we don't use them, so no point having multiple. Why is this even global?
button_handle_t btn_handle;

//Pulse counter configs. This is used for the rotary encoder for sending volume commands. 
//Brightness rotary potentiometer acts as voltage divider sending direct voltage to LCD backlight input. No code required.
pcnt_unit_config_t unit_config = {
    .high_limit = 1,
    .low_limit = -1,
};
pcnt_unit_handle_t pcnt_unit = NULL;
pcnt_glitch_filter_config_t filter_config = {
    .max_glitch_ns = 10000,
};
pcnt_chan_config_t chan_config = {
    .edge_gpio_num = ENC_LEVEL_PIN,
    .level_gpio_num = ENC_EDGE_PIN,
};
pcnt_channel_handle_t pcnt_chan = NULL;
//Watch points used for generating pulse counter events
int watch_points[] = {-1, 0, 1};

//Button callback. All the buttons do the same thing; send msg_type tag byte, plus command code byte. Might as well have one that does it all.
//Each command code needs an address for callback to function, so they're all const uint8_t 
static void button_cbs(void *arg, void *data){
    //Should we be adding event to queue and monitoring in instead of calling this directly?
    serial_jtag_write(CMD_TAG, data, 1, portDelay);
}

//Add event to queue on pulse counter watch point reached. Loop at bottom will monitor queue for events
static bool pcnt_on_reach(pcnt_unit_handle_t unit, const pcnt_watch_event_data_t *edata, void *user_ctx)
{
    BaseType_t high_task_wakeup;
    QueueHandle_t queue = (QueueHandle_t)user_ctx;
    // send event data to queue, from this interrupt callback
    xQueueSendFromISR(queue, &(edata->watch_point_value), &high_task_wakeup);
    return (high_task_wakeup == pdTRUE);
}

void inputs_main(){

    //previous track
    //Update config with new GPIO pin, nothing else needs to change
    gpio_btn_cfg.gpio_button_config.gpio_num = PREV_PIN;
    btn_handle = iot_button_create(&gpio_btn_cfg);
    //Pass button handle updated with new config, listen to press down events, run callback on selected event, pass the address of the specific command code to the callback
    iot_button_register_cb(btn_handle, BUTTON_PRESS_DOWN, button_cbs, (void*)&cmd_prev);
    
    //The rest of the buttons are the same as above but with updated pin, handle, and command code byte
    //mute toggle
    gpio_btn_cfg.gpio_button_config.gpio_num = MUTE_PIN;
    btn_handle = iot_button_create(&gpio_btn_cfg);
    iot_button_register_cb(btn_handle, BUTTON_PRESS_DOWN, button_cbs, (void*)&cmd_mute);

    //pause/play
    gpio_btn_cfg.gpio_button_config.gpio_num = PLAY_PIN;
    btn_handle = iot_button_create(&gpio_btn_cfg);
    iot_button_register_cb(btn_handle, BUTTON_PRESS_DOWN, button_cbs, (void*)&cmd_play_pause);

    //next track
    gpio_btn_cfg.gpio_button_config.gpio_num = NEXT_PIN;
    btn_handle = iot_button_create(&gpio_btn_cfg);
    iot_button_register_cb(btn_handle, BUTTON_PRESS_DOWN, button_cbs, (void*)&cmd_next);
    //For other events (ie BUTTON_DOUBLE_CLICK or BUTTON_LONG_PRESS) a new callback may be needed, or not. Probably just a new cmd_code byte

    
    //pulse counter for encoder
    pcnt_new_unit(&unit_config, &pcnt_unit);
    pcnt_unit_set_glitch_filter(pcnt_unit, &filter_config);
    pcnt_new_channel(pcnt_unit, &chan_config, &pcnt_chan);
    ESP_ERROR_CHECK(pcnt_channel_set_edge_action(pcnt_chan, PCNT_CHANNEL_EDGE_ACTION_DECREASE, PCNT_CHANNEL_EDGE_ACTION_INCREASE));
    ESP_ERROR_CHECK(pcnt_channel_set_level_action(pcnt_chan, PCNT_CHANNEL_LEVEL_ACTION_KEEP, PCNT_CHANNEL_LEVEL_ACTION_INVERSE));
    
    pcnt_unit_add_watch_point(pcnt_unit, watch_points[0]);
    pcnt_unit_add_watch_point(pcnt_unit, watch_points[2]);

    pcnt_event_callbacks_t cbs = {
        .on_reach = pcnt_on_reach,
    };
    QueueHandle_t queue = xQueueCreate(10, sizeof(int));
    ESP_ERROR_CHECK(pcnt_unit_register_event_callbacks(pcnt_unit, &cbs, queue));

    ESP_ERROR_CHECK(pcnt_unit_enable(pcnt_unit));
    ESP_ERROR_CHECK(pcnt_unit_clear_count(pcnt_unit));
    ESP_ERROR_CHECK(pcnt_unit_start(pcnt_unit));

    // Report counter value
    int event_count = 0;
    while(1){
        //What is my purpose? Your purpose is to wait for pcnt events :)
        if (xQueueReceive(queue, &event_count, pdMS_TO_TICKS(1000))) {
            //Send command over serial based on rotation direction 
            if(event_count == -1){
                serial_jtag_write(CMD_TAG, cmd_vol_dn, 1, portDelay);
            }
            else{
                serial_jtag_write(CMD_TAG, cmd_vol_up, 1, portDelay);
            }
        }
    }
}