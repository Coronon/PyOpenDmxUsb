import win32pipe, win32file

class DMXServerError(Exception):
    pass

class DMXClient():
    """Interface to connect to a DMXServer controlling an Entec OPEN DMX USB
    
    Arguments:
        pipeName {string} -- Name of named pipe (DMXServer must use the same)
    """
    def __init__(self, pipeName):
        self.pipe = win32pipe.CreateNamedPipe(
        r'\\.\pipe\\'+pipeName,
        win32pipe.PIPE_ACCESS_OUTBOUND,
        win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_READMODE_MESSAGE | win32pipe.PIPE_WAIT,
        win32pipe.PIPE_UNLIMITED_INSTANCES,
        65536,
        65536,
        0,
        None)
        self.connected = False
    
    def connect(self, verbose=False):
        """Connect to the DMXServer using a named pipe (blocks until connected)
        
        Keyword Arguments:
            verbose {bool} -- Print verbose output to stdout (default: {False})
        """
        if verbose:
            print("Waiting for connection to DMXServer...", end='', flush=True)
        win32pipe.ConnectNamedPipe(self.pipe, None)
        self.connected = True
        if verbose:
            print("Connected")

    def close(self, verbose=False):
            """Close connection to the DMXServer
            
            Keyword Arguments:
                verbose {bool} -- Print verbose output to stdout (default: {False})
            
            Raises:
                DMXServerError: Not connected to DMXServer
            """
            if not self.connected:
                raise DMXServerError("Not connected to DMXServer")
            self.connected = False
            if verbose:
                print("Closing connection to DMXServer...", end='', flush=True)
            win32file.CloseHandle(self.pipe)
            if verbose:
                print("Disconnected")

    def write(self, message):
        """Sends a DMXCommand to the DMXServer to control an Enttec OPEN DMX USB
        
        Arguments:
            message {string} -- ('DMX channel value...') DMXCommand to send with unlimited channel->value pairs
            message {list} -- ([channel, value...]) DMXCommand to send with unlimited channel->value pairs
            message {dictionary} -- ({channel: value...}) DMXCommand to send with unlimited channel->value pairs, pairs can be {int, string}
        
        Raises:
            DMXServerError: Not connected to DMXServer
            ValueError: Malformed DMXCommand
        """

        #Check type of message to convert to string DMXCommand
        messaget = type(message)
        command = "DMX"
        if messaget == list:
            if len(message) % 2 != 0:
                raise ValueError("Malformed DMX-List")
            for i in message:
                command += " " + str(i)
        elif messaget == dict:
            for key, value in message.items():
                command += ' ' + str(key) + ' ' + str(value)
        elif messaget == str:
            command = message
        else:
            raise ValueError("DMXCommand has invalid data type")

        if not self.connected:
            raise DMXServerError("Not connected to DMXServer")
        self._write(command)

    def effect(self, message):
        """Sends a DMXEffect to the DMXServer to control an Enttec OPEN DMX USB
        
        Arguments:
            message {string} -- ('EFFECT name time channel value...') DMXEffect to send with unlimited time->channel->value pairs
            message {list} -- ([name, time, channel, value...]) DMXEffect to send with unlimited time->channel->value pairs
        
        Raises:
            DMXServerError: Not connected to DMXServer
            ValueError: Malformed DMXEffect
        """

        #Check type of message to convert to string DMXCommand
        messaget = type(message)
        command = "EFFECT"
        if messaget == list:
            command += " {0}".format(message.pop(0)) #HACK: !This will be migrated to f-Strings in a future release
            if len(message) % 3 != 0:
                raise ValueError("Malformed Effect-List")
            for i in message:
                command += " " + str(i)
        elif messaget == str:
            command = message
        else:
            raise ValueError("DMXCommand has invalid data type")

        if not self.connected:
            raise DMXServerError("Not connected to DMXServer")
        self._write(command)

    def _write(self, message):
        """Directly send Command to DMXServer without any checks
        
        Arguments:
            message {string} -- Command without trailing newline('\\n')
        """
        win32file.WriteFile(self.pipe, message.encode()+b'\n')