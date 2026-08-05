# Crestron Android TV IP Module

A simple Crestron SIMPL# Pro module for controlling Android TV and Google TV devices over IP using the Android TV Remote Protocol v2.

This module is designed to run on **Crestron 4-Series processors (CP4, MC4, etc.)** and provides a straightforward way to integrate Android TV devices into Crestron control systems without relying on ADB or enabling developer options.

## Features

- IP communication with Android TV / Google TV devices
- Compatible with Crestron 4-Series processors
- Native SIMPL# Pro implementation
- No ADB required
- Based on the official Android TV Remote Protocol v2

## Requirements

- Crestron 4-Series processor
- Android TV / Google TV device with the Android TV Remote Service enabled (default on most devices)
  
## Known limitations

- Text input is intentionally not implemented. While the Android TV Remote Protocol supports text input, it does not reliably handle special characters and international keyboard layouts, making the feature too limited for practical use.
## Credits

This project would not have been possible without the excellent work by **tronikos**.

The implementation is largely inspired by the Python library:

https://github.com/tronikos/androidtvremote2/

Many thanks for documenting and implementing the Android TV Remote Protocol v2.

## License

This project is released under the MIT License.
