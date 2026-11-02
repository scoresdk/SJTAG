# SJTAG - Sunplus JTAG
This contains code related to my (@AzureianGH) implementation of Sunplus' SJprobe.

## Where to find X

`/encore/`: Utility made for reading, writing, and verifying programs sent to the flash.
`/Example/`: Simple memory read example.
`/SJTag/`: The main code behind most my research. Where you will find the most information.
`/CH347/`: Support for my JTAG debugger. Feel free to add your own devices.
`/Jtag/`: Interfaces for adding your own debugger's support.

## Notice
This code is an amalgamation of half a year worth of tests. It's built upon itself in a way not intended to be. I never expected to get this far, and under constraint of wanting to release, do keep this in mind. The library is tested well, so report any bugs found.

## History
I started around half a year ago, being introduced to this project. So far nobody had gotten JTAG working, so I took it upon myself to try and crack it. Originally my goal wasn't JTAG, in-fact I thought I wouldn't be able to figure it out. I started working on ways around JTAG, eventually making tools that at that point would be easier to just break JTAG.
<br> <br>
I did all my work alone. Not because I had to, but because it would be good for gaining skills. This project followed me into university.
<br> <br>
This is what lead us to now. Finally, I present to you, SJTAG!

## Big credit to [@LiraNuna](https://github.com/liranuna)
Got me interested in S+Core and provided me with a lot of base knowledge on the system.

## Contact
Contact me at `me@nhornby.com` or `@azureian` on Discord.