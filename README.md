### Overview
LasZip.Net is a reimplementation of [LASzip](https://rapidlasso.de/laszip/) entirely in C#. This repo's primary purpose is keeping LASzip's 
C# implementation in sync with its C++ version. Towards that end, a copy of [the C++ version](https://github.com/LAStools/LAStools/tree/master/LASzip/src) 
is included in this repo as a tracking mechanism. As LASzip changes its updated files can be dropped into the C++ reference directory and the
diffs in the C++ then indicate where corresponding updates need to be made in C#. C# .cs files are therefore tagged with the names of the
corresponding C++ files.

### Differences from LASzip
LasZip.Net is intended to be interoperable with any other compliant .las or .laz reader or writer. While both code bases also use similar
code layouts, some differences do exist between the C# and C++ code structure. Primarily, LasZip.Net is more rigorous in following the 
convention of one type per file and uses .NET streams rather than ports of LASzip's ByteStreamIn and ByteStreamOut class families.

### Evolution from earlier versions
This repo is forked from [@shintadono's initial implementation of laszip.net](https://github.com/shintadono/laszip.net), which was last
updated in December 2017 and ported most of the code in LASzip 2.2.0.140907 which, per the build number, is from September 7, 2014. 
[LAS 1.4 R13](https://www.asprs.org/divisions-committees/lidar-division/laser-las-file-format-exchange-activities) was released at that time
but LASzip's layered, chunked compression was not. As of October 2023, LAS 1.4 R14, March 2019, is the current version of the LAS specification.

The main changes in this repo are

- Realignment towards standard C# coding practices. Naming, exceptions, properties, accessibility, nullability, initialization, file layout.