import time
import struct

if __name__ == '__main__':
    f = open(r"\\.\pipe\UnityPipe", 'r+b', 0)
    i = 1

    while True:
        s = 'Message[{0}]'.format(i).encode('ascii')
        i += 1
            
        f.write(struct.pack('I', len(s)) + s)   # Write str length and str
        f.seek(0)                               # EDIT: This is also necessary
        print('Wrote:', s)

        n = struct.unpack('I', f.read(4))[0]    # Read str length
        s = f.read(n).decode('ascii')           # Read str
        f.seek(0)                               # Important!!!
        print('Read:', s)

        time.sleep(2)