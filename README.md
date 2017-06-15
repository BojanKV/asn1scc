Status of building the compiler and executing all C and Ada tests under Linux:

[![Build and Test Status of ASN1SCC on Circle CI](https://circleci.com/gh/ttsiodras/asn1scc.svg?&style=shield&circle-token=fcc32f415742887faa6ad69826b1cf25426df086)](https://circleci.com/gh/ttsiodras/asn1scc/tree/master)

Status of building the compiler under Windows:

[![Build Status of ASN1SCC on AppVeyor](https://ci.appveyor.com/api/projects/status/github/ttsiodras/asn1scc)](https://ci.appveyor.com/project/ttsiodras/asn1scc)

Status of building the compiler under OS/X:

[![Build Status of ASN1SCC on Travis CI](https://travis-ci.org/ttsiodras/asn1scc.svg?branch=master)](https://travis-ci.org/ttsiodras/asn1scc?branch=master)

*For the impatient: if you already know what ASN.1 and ASN1SCC is, and
just want to run the ASN1SCC compiler:*

    docker pull ttsiodras/asn1scc
    docker run -it ttsiodras/asn1scc

*...and follow the instructions shown.*

Executive summary
=================

This is the source code of the ASN1SCC compiler - an ASN.1 compiler that
targets C and Ada, while placing specific emphasis on embedded systems.
You can read a comprehensive paper about it
[here (PDF)](http://web1.see.asso.fr/erts2012/Site/0P2RUC89/7C-4.pdf),
or a blog post with hands-on examples
[here](https://www.thanassis.space/asn1.html).
Suffice to say, if you are developing for embedded systems, it will probably
interest you.

Compilation
===========

## Common for all OSes

First, install the Java JRE. This is a compile-time only dependency,
required to execute ANTLR.

Then depending on your OS:

### Under Windows

Install:

1. A version of Visual Studio with support for F# .

2. Open `Asn1.sln` and build the `Asn1f2` project (right-click/build)

## Under OSX

1. Install the [Mono MDK](http://www.mono-project.com).

2. Execute `xbuild` (or `msbuild` in Mono 5.0 and newer) in ASN1SCC's directory.

3. Built compiler will be available in `Asn1f2/bin/Debug` folder.

## Under Linux

1. Install the [mono](http://www.mono-project.com) development tools. Under
   Debian Jessie for example (as of Sep/2014):

    ```
    $ mono -V
    Mono JIT compiler version 3.2.8 (Debian 3.2.8+dfsg-7)
    Copyright (C) 2002-2014 Novell, Inc, Xamarin Inc and Contributors.
            TLS:           __thread
            SIGSEGV:       altstack
            Notifications: epoll
            Architecture:  x86
            Disabled:      none
            Misc:          softdebug 
            LLVM:          supported, not enabled.
            GC:            sgen
    ```

2. Use the `fsharpc` compiler inside your distro, or just checkout and compile
   the Open Source F# compiler...

    ```
    git clone https://github.com/fsharp/fsharp && \
        cd fsharp && \
        ./configure && \
        make && sudo make install 
    ```

3. Execute

    ```
    xbuild
    ```

Depending on the version of Mono that you are using, you may need to specify
a specific target .NET framework version:

    ```
    xbuild /p:TargetFrameworkVersion="v4.5"
    ```

On Mono version 5.0 and newer use `msbuild` instead of `xbuild`.

4. Built compiler will be available in `Asn1f2/bin/Debug/` folder.

5. Run tests (if you want to):

    ```
    cd Tests
    make
    ```

Note that in order to run the tests you need both GCC and GNAT.
The tests will process hundreds of ASN.1 grammars, generate C and
Ada source code, compile it, run it, and check the coverage results.

Continuous integration and Docker image
=======================================

ASN1SCC is setup to use CircleCI for continuous integration. Upon every
commit or merge request, the packaged circle.yml instructs CircleCI to...

    (a) build ASN1SCC with the new code
    (b) then run all the tests and check the coverage results.

But where is that build being made? Inside what environment?

CircleCI offers only 3 build environments: OSX, Ubuntu 12 and Ubuntu 14.
Till recently (March/2017) Ubuntu 14 met all the dependencies that were
needed to build and run the tests. But the work being done to enhance the
Ada backend with the new SPARK annotations, requires the latest GNAT;
which is simply not installable in Ubuntu 14.

Thankfully, CircleCI also supports Docker images.

We have therefore setup the build, so that it creates (on the fly)
a Debian Docker image based on the latest version of Debian stable
(the soon to be announced Debian Stretch). Both the ASN1SCC build and
the test run are then executed inside the Docker image.

Needless to say, the Docker image can be used for development as well;
simply execute...

    docker build -t asn1scc .

...and your Docker install will build an "asn1scc" Docker image, pre-setup
with all the build-time dependencies to compile ASN1SCC and run its 
test suite. To do so, you'll need to run this:

    $ docker run -it -v $(pwd):/root/asn1scc asn1scc

    (Your Docker image starts up)

    # cd /root/asn1scc 
    # xbuild /p:TargetFrameworkVersion="v4.5"
    ...

    ASN1SCC is built at this point - and if you want to run the tests:

    # cd Tests
    # make
    ...

This same sequence of commands is executed in CircleCI to check for
regressions; with the added benefit that after building the image for
the first time, CircleCI is configured to cache the Docker image (see
circle.yml for details). This means that upon new commits in ASN1SCC,
CircleCI will re-use the Docker image that was made in previous runs,
and therefore avoid re-installing all the build environment tools every
time. The develop-test cycles are therefore as fast as they can be.

Usage
=====

The compiler has many features - it is documented in
[Chapter 10 of the TASTE manual](http://download.tuxfamily.org/taste/snapshots/doc/taste-documentation-current.pdf),
and you can see some simple usage examples in a related
[blog post](https://www.thanassis.space/asn1.html).

You can also read about
[how the compiler has been used in the TASTE project](http://www.semantix.gr/assert/)
to target safety-critical systems - and maybe also check out the
official [TASTE project site](http://taste.tuxfamily.org).

Credits
=======
George Mamais (gmamais@gmail.com), Thanassis Tsiodras (ttsiodras@gmail.com)
