from wand.image import Image
from wand.color import Color
import binascii
from io import BytesIO
import os

def debug_layers(image, output):
    print('Debugging to file', output)
    with Image(image) as img:
        img.background_color = Color('lime')
        for index, frame in enumerate(img.sequence):
            
            frame.transform(resize='304x304')
            frame.background_color = Color('black')
            frame.extent(304, 304, gravity='center')
            print('Frame {0} size : {1} page: {2}'.format(index,
                                                          frame.size,
                                                          frame.page))
        img.concat(stacked=True)
        img.save(filename=output)

def convert_to_565(bytes):
    byte_count = len(bytes)
    rgb565 = bytearray()
    for i in range(0, byte_count, 3):
        rg = ((bytes[i] & 0xF8) | (bytes[i + 1] >> 5))
        rgb565.extend(rg.to_bytes(1, 'little'))
        gb = (((bytes[i + 1] & 0x1C) << 3) | (bytes[i + 2] >> 3))
        rgb565.extend(gb.to_bytes(1, 'little'))
    return rgb565

with Image(filename="shocked.gif") as gif:
    gif.iterator_reset()
    frames_num = len(gif.sequence)
    byte_file = open("gif_bytes", "wb")
    frame_size = 170
    print(gif.delay*10)
    for i in range(0, frames_num):
        frame = Image(gif.sequence[i])
        frame.transform(resize=f'{frame_size}x{frame_size}!')
        #frame.transform('80%x100')
        frame.background_color = Color('black')
        frame.extent(frame_size, frame_size, gravity='center')
        frame.save(filename=f"gif/gif{i}.png")
        pixels = frame.export_pixels(channel_map="RGB")
        rgb565 = convert_to_565(pixels)
        for i in range(0, len(rgb565), 2):
            byte = bytearray()
            byte.extend(rgb565[i].to_bytes(1, 'little'))
            byte.extend(rgb565[i+1].to_bytes(1, 'little'))
            byte_file.write(byte)
    print(os.path.getsize(filename="gif_bytes"))
    byte_file.close()
    byte_size = os.path.getsize(filename="gif_bytes")
    if(byte_size > 2457600):
        print("File too big, max size is 2.4 MB")
    #debug_layers(gif, "expanded.png")
    
    #gif.coalesce()
    #gif.convert("gif").coalesce().to_bytes()

    #gif.transform(resize='304x304')
    #gif.background_color = Color('black')
    #gif.extent(304, 304, gravity='center')
    #gif.save(filename='gify.gif')