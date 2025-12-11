from wand.image import Image
from wand.color import Color
import binascii
from io import BytesIO

def convert_to_565(bytes):
    byte_count = len(bytes)
    rgb565 = bytearray()
    for i in range(0, byte_count, 3):
        rg = ((bytes[i] & 0xF8) | (bytes[i + 1] >> 5))
        rgb565.extend(rg.to_bytes(1, 'little'))
        gb = (((bytes[i + 1] & 0x1C) << 3) | (bytes[i + 2] >> 3))
        rgb565.extend(gb.to_bytes(1, 'little'))
    return rgb565

#with Image(filename="logo_64.png") as img:
with Image(filename="thumby.png") as img:
    img.background_color = Color('black')
    pixels = img.export_pixels(channel_map="RGB")
    rgb565 = convert_to_565(pixels)
    hex565 = binascii.hexlify(rgb565)
    byte_file = open("bytes", "wb")
    byte_text = open("bytes.txt", "w")
    #byte_file = open("bytes", "w")
    for i in range(0, len(rgb565), 2):
        byte = bytearray()
        byte.extend(rgb565[i].to_bytes(1, 'little'))
        byte.extend(rgb565[i+1].to_bytes(1, 'little'))
        #print(hex((rgb565[i])), end=" ")
        #print(f"{rgb565[i]:x}", end=" ")
        r = (rgb565[i] & 0xF8)
        g = ((rgb565[i] & 0x07) << 5) | ((rgb565[i+1] >> 3) & 0x1C)
        b = (rgb565[i+1] & 0x1f) << 3

        byte_str = f"{rgb565[i]:x}"
        rgbhex = f"#{r:02x}{g:02x}{b:02x}"
        #print(rgbhex, end=", ")
        #print(hex(hex565[i]), end=", ")
        #byte_file.write(str(hex((byte[0]))))
        #byte_file.write(byte_str)
        byte_file.write(byte)
        byte_text.write(f"#{byte[0]:02x}{byte[1]:02x}, ")
    #print(rgb565)
    #byte_file = open("bytes", "wb")
    #byte_file.write(rgb565)
    byte_file.close()
    byte_text.close()
    write_byte = BytesIO(rgb565)
    with open("test.bin", "wb") as f:
        f.write(rgb565)

with open("bytes", "rb") as bin:
    byte_bin = bin.read(2)
    hexrgb = open("hex.txt", "w")
    while len(byte_bin) >= 2:
        r = (byte_bin[0] & 0xF8)
        g = ((byte_bin[0] & 0x07) << 5) | ((byte_bin[1] >> 3) & 0x1C)
        b = (byte_bin[1] & 0x1f) << 3
        print(f"#{r:02x}{g:02x}{b:02x}", end=", ")
        hexrgb.write(f"#{r:02x}{g:02x}{b:02x}, ")
        byte_bin = bin.read(2)
    hexrgb.close()