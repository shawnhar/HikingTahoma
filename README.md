# HikingTahoma

Simple static website about hiking on Mount Rainier. Contains authored source 
data (in the source folder) plus code (in the builder folder) for generating 
the actual site (which is published at www.hikingtahoma.com).

The workflow is annoying because I wanted to use Win2D in the builder app, but 
was too lazy to figure out how to make that work with a regular console app. 
So, the builder is a UWP :-( As a hack until I figure out something better, it 
just reads and writes its local cache folder. Therefore to build the site the 
workflow is:

- Run put.bat
- Run the builder UWP
- Run get.bat

Published website files will now be in the .\HikingTahoma folder.

Bucket emoji designed by [OpenMoji](https://openmoji.org/) - the open-source emoji and 
icon project. License: [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0).
