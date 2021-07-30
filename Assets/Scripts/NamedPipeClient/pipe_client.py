import time
import sys
from typing import Union
import struct

class UnityPipeClient:
    def __init__(self, timeout: Union(None, float)=None):
        assert type(timeout) is None | type(timeout) is float
        self.timeout = timeout

    def wait_for_pipe(self):
        # When connection is established sucessfully write 'Listening token' to pipe
        # wait for reply from server before returning
        #  return true when connection established
        duration = sys.maxsize
        if self.timeout is not None:
            duration = self.timeout
            self.timeout = None

        start_time = time.time
        while (not self.timeout):
            try:
                with Pipe() as conn:
                    conn.write('--connected--')
                    print(conn.read())
                    conn.write('--end-of-stream--')
            except FileNotFoundError:
                time.sleep(0.5)
            except KeyboardInterrupt:
                break

            self.timeout = time.time - start_time > duration
                
                


class Pipe:
    def __init__(self, pipe_path=r"\\.\pipe\UnityPipe"):
        self.pip_path = pipe_path

    def write(self, s: str) -> None:
        self.f.write(struct.pack('I', len(s)) + s)

    def read(self) -> str:
        message_len = struct.unpack('I', self.f.read(4))[0]
        return self.f.read(message_len).decode('ascii')
    
    def close(self):
        self.f = None

    def __enter__(self):
        self.f = open(r"\\.\pipe\UnityPipe", 'r+b', 0)
        return self

    def __exit__(self, exception_type, exception_value, traceback):
        if (exception_value is not None):
            print('Exception when leaving context manager: {} | Traceback: {}{'.format(exception_type, traceback))
        
        self.close()



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