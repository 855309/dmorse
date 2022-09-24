# dmorse
Morse audio decoder written in C#.

## Usage 
```c
Usage: dmorse [decode/generate] [audio/text file] [options]...
	 --debug:           Print debug messages.
	 --out <string>:    Set the output audio file.
	 --wpm <int>:       Set the output wpm. (Default: 25)
	 --freq <int>:      Set the output frequency. (Default: 800)
	 --vol <double>:    Set the output volume. Min. 0, Max. 1 (Default: 0.5)
```
For real-time decoding (not working currently) use ``device:index`` for [audio/text file] argument.
Use ``--debug`` option for extra info.

## Download
You can download the latest release from [here.](https://github.com/855309/dmorse/releases)
