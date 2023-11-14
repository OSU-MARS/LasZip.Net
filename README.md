### Overview
LasZip.Net is a reimplementation of [LASzip](https://rapidlasso.de/laszip/) entirely in C#. This repo's primary purpose is keeping LASzip's 
C# implementation in sync with its C++ version. Towards that end, a copy of [the C++ version](https://github.com/LAStools/LAStools/tree/master/LASzip/src) 
is included in this repo as a tracking mechanism. As LASzip changes its updated files can be dropped into the C++ reference directory and 
diffs in the C++ then indicate where corresponding updates need to be made in C#. C# .cs files are therefore tagged with the names of the
corresponding C++ files. It's debatable whether this is preferable to a managed C++ or p/invoke layer over LASzip but it's what was chosen 
in 2014 and the present repo maintains the approach.

### Differences from LASzip
LasZip.Net is intended to be file level interoperable with any other compliant .las or .laz reader or writer. While both the C++ and C# 
codebases offer similar APIs and code layouts, differences do exist between the two as a result of differences in C# and C++ coding practices.
The primary differences are casing conventions and in throwing exceptions rather than requiring callers constantly check return values.
In a few cases method signatures differ slightly as, for example, C++ functions for setting properties collapse to property setters in C# 
and parameters controlling use of `delete` in C++ do not apply to C# garbage collection.

Internally, LasZip.Net is more rigorous in following the convention of one type per file. Additionally,

- Rather than LASzip's colletion of streams, `System.Buffers.Binary.BinaryPrimitives` is used to handle serialization to and from .las and .laz 
  files' little endian format to the processor's endianness (nearly always little). This also fixes a few holes where LASzip fails to convert to 
  or from little endian.
- Error handling code in `catch` blocks is not ported as, in C#, there's no reason to mask the underlying exception. This also means 
  LasZip.Net delegates handling of corrupted chunks of points to its caller rather than skipping to the next chunk as LASzip does.
- The LASzip build defines `LASZIPDLL_EXPORTS`. Only C++ code which is meaningful under this definition is ported, resulting in the omission
  of a few methods present in the LASzip source but, if called against LASzip.dll, mostly just return `false` to indicate they don't do anything.

### Known Issues

- Rather than use exceptions, LASzip APIs rely on constant caller error checking. Since this is unnatural in C#, where exceptions are 
  expected to be used to offload callers from error checking, LasZip.Net typically throws where an error would be recorded by LASzip. This
  is a reasonable, but formally, breaking change. Additionally, a broad range of APIs haven't yet been cleaned and either still return
  codes for their callers to check, instead of making an informative local throw, or always return success rather than having a `void`
  signature.
- LASzip logs warnings to either internal strings or stderr and also contains some verbose logging. These systems have not yet been
  converted to standard .NET tracing.
- The LASzip C++ sources have a certain tendency to silently ignore user input. As this is rarely desirable behavior, code changes in 
  LasZip.Net favor throwing in such situations. As a result, LasZip.Net may detect data slicing or bugs in its callers where LASzip does 
  not.
- The LASzip C++ sources omit many checks for null and therefore have many potential failure points. These are automatically flagged as 
  nullability issues in C# and are slowly being addressed, either by fixing them or by updating code style in cases where Visual Studio
  2022 (17.7.6 and newer) incorrectly reports a nullability issue.
- The LASzip C++ sources sometimes use double precision floating point numbers as intermediate representations when serializing and deserializing
  integers (8—64 bit, both signed and unsigned) and may therefore be lossy due to numerical rounding on paths where error free integer
  propagation would reasonably be expected. This has not yet been addressed in the C# sources.
- The LASzip C++ sources often use signed and unsigned integers, particularly 32 bit ones, interchangeably. In C# this leads to frequent
  explicit casts to convert between types, moreso because C# APIs often support only signed integers as indices into arrays or similar
  data structures. This confusion of signatures has not yet been rationalized and is sometimes exposed to callers in public C# APIs.
- The LASzip C++ sources not uncommonly contain duplicate instances of the same code or near duplicates which appear intended to produce
  identical output. Some of these have been rationalized in C#, in a few cases creating small but―strictly―breaking changes, and many 
  haven't yet been rationalized.

### Evolution from earlier versions
This repo is forked from [@shintadono's initial implementation of laszip.net](https://github.com/shintadono/laszip.net), which was last
updated in December 2017 and ported most of the code in LASzip 2.2.0.140907 which, per the build number, is from September 7, 2014. 
[LAS 1.4 R13](https://www.asprs.org/divisions-committees/lidar-division/laser-las-file-format-exchange-activities) was released at that time
but LASzip's layered, chunked compression was not. As of October 2023, LAS 1.4 R15, July 2019, is the current version of the LAS specification.

Compared to builds of laszip.net from 2017 and earlier the main changes in this repo are

- Realignment towards standard C# coding practices. Naming, exceptions, properties, nullability, initialization, static methods where `this`
  is not required, extension classes, repo file layout, and explicit declaration of property, method, and type accessbility.
- Support for LAX indexing.
- Unit test coverage and opportunistic bug fixes. `TryReadPoint()`, for example, now correctly returns `false` when all points have been read 
  from a .laz file.
