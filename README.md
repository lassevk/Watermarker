# README

This tool is a very simple tool that I use in my image workflow.

Specifically, I am using [Hazel][1] for Macos to monitor the folder I export images to, and all .TIFF images
are processed using this Watermarker tool.

The tool does the following:

* Blurs out a section at the bottom of the photo
* Overlays the section with 2 lines of text, with text on the left and from the right edge
* Line 1 on the left contains my copyright information
* Line 2 on the left contains camera make and model, lens make and model, as well as exposure information such as aperture, focal length, iso, shutterspeed
* Line 1 on the right contains the location of the photo, in map coordinates, if present in the .TIFF image
* Line 2 on the right contains the capture date and time
* The result is saved back as a .JPG file alongside the .TIFF file
* The .TIFF file is then deleted

Hazel also monitors for .JPG files and will sort these into sub-folders for year, month and day.

### DISCLAIMER
There is very little in terms of customization, as the tool has been written for me and my needs. If anyone would like to use this tool, I would appreciate having a discussion about necessary changes to accomodate this. I've already logged one issue with some requests,
I still need to decide if I want to go down the route of making this a general tool for everyone. You are of course free to fork and change the code yourself, though there may be an issue with some nuget packages I am using, but let me know, we'll figure something out.
