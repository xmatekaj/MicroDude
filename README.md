# Work in progress. Only some functionality works. 

# ![MicroDude](src/Resources/Icon.png) MicroDude

**MicroDude** is an extension for **[Microchip Studio](https://www.microchip.com/en-us/tools-resources/develop/microchip-studio)** which integrates **[AvrDude](https://github.com/avrdudes/avrdude)** into the IDE. 

Current version of **![Flash](src/Resources/Icon_MicroDudeSettings.png) MicroDude** is shipped with [AvrDude v8.0](https://github.com/avrdudes/avrdude/releases/tag/v8.0).


## Features/HowTo
- ![AutoFlash](src/Resources/Icon_Flash_Auto.png) **Auto-flash** the microcontroller after successful compilation. No more additional clicks. ***You compile it, You flash it!***
Enable or disable this option via toolbar icon, contextual menu or shortcut keys just as easily.
Don't worry - rebuild won't cause it to flash again!
- ![UsbDetection](src/Resources/Icon_USB.png) **USB detection** of connected programmer.
It no only detects your programmer automatically, it also sets it to work with AvrDude. ***Plug & Play!***
- ![ReadTheChip](graphics//Graphics_Icon_Read_Project.png) **Microcontroller detection** of selected chip in Microchip Studio.
You no longer need to pick up a microcontroller from a long list. ***Load, compile, flash, repeat!***
- ![Color](src/Resources/Icon_Color.png) **Colored text output** of Output Pane window. **$\textcolor{red}{\text{Errors are in Red}}$, $\textcolor{orange}{\text{Warnings in Orange}}$, $\textcolor{green}{\text{build headers are Green}}$, $\textcolor{lightblue}{\text{ MicroDude output is Light Blue}}$**   $\textcolor{grey}{\text{ and the rest is grey}}$. Colors can be modified. 
- ![StatusBar](src/Resources/Icon_StatusBar.png) **Status Bar information** gives you all the information you need. What programmer is connected, on which port, what MCU is loaded (shows conflict if Project's MCU is not the same as the one you connected), Flash and EEPROM size and usage. 



The rest comes with AvrDude itself:

- ![Flash](src/Resources/Icon_Flash.png) **Flash** the microcontroller.
- ![Check](src/Resources/Icon_Detect.png) **Detect** the microcontroller.
- ![FuseBits](src/Resources/Icon_Fuse.png) **Fuse bits** programming, choose from the list or select manually. 
- ![Oscillator](src/Resources/Icon_Oscillator.png) **Frequency** change, fast and easy.
- ![Lock](src/Resources/Icon_Lock.png) **Lock bits** programming. Just as easly as fuse bits.


## Screenshots
<div align="center">

Context Menu:<br>
![ContextMenu](/graphics/Graphics_Context_Menu.png)

Menu:<br>
![Menu](/graphics/Graphics_Menu.png)

Status Bar:<br>
![StatusBar](/graphics/Graphics_StatusBar.png)

Output Pane Window:<br>
![Output](/graphics/Graphics_Output.png)

Oscillator Window:<br>
![Oscillator](/graphics/Graphics_Oscillator.png)

</div>


## Supported versions (== Tested on)

- [Atmel Studio v6.0.1996](https://ww1.microchip.com/downloads/archive/as6installer-6.0.1996.exe)
- [Atmel Studio v6.1.2674](https://ww1.microchip.com/downloads/archive/AStudio61sp1_1.exe)
- [Atmel Studio v6.2.1563](https://ww1.microchip.com/downloads/archive/AStudio6_2sp2_1563.exe)
- [Atmel Studio v7.0.2389](https://ww1.microchip.com/downloads/en/DeviceDoc/installer-7.0.2389-full.exe)


## Support
I've spent quite a lot of time to make it bug free and user friendly so if you find this project usefull you can show your appreciation and [BuyMeACoffee.com](https://buymeacoffee.com/matekaj) or [BuyCoffee.To](https://buycoffee.to/matekaj).


## License
[MIT](http://opensource.org/licenses/MIT)


## TODO
- better text classifications for color output.

