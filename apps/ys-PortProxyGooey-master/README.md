# PortProxyGooey

A manager for netsh interface portproxy, which is to evaluate TCP/IP port redirect on windows.

This is a fork of the original PortProxyGUI; I suppose you could (*cheesily*) think of it as "PortProxyGUI on Nitrous", or steroids, or something like that.

I made the changes strictly for my own purposes, and just for the helluvit thought I'd also share it with the world in case you all may also find it useful.

My specific purposes are basically using it as a tool for the combination uses of Windows, WSL, and Docker, so they can co-exist as peacefully and easily as possible (*cough*, yea right).

For the users of the original v1.4.0, every single thing in it is still in Gooey; plus all of the fixes and requests that I saw in it's issue queue as of this release. I'll try to stay up any any changes the original makes as well, as long as they're in harmoney with each other.

- That all said, I have literally a quadzillion other projects that I'm always working on, so unless this gets a lot of user activity, once I'm satisfied with it for my own uses, it may just stop development in the blink of an eye so that I can focus my energies on some other project.
- It's currently still a work in progress, has some bugs and unfinished code and features, so keep that in mind while using it.

Here're just a few current screenshots of v2 (a.k.a PortProxyGooey), bear in mind, the screenshots most likely won't always be updated with every single change/fix/version; I'm usually pretty lazy in that regard.

## Screenshots

<img src="https://raw.githubusercontent.com/STaRDoGG/PortProxyGUI/master/screenshots/01.png" width="50%" height="50%"><img src="https://raw.githubusercontent.com/STaRDoGG/PortProxyGUI/master/screenshots/02.png" width="50%" height="50%">



<br/>


## v2.0.0 - G00ey

  - Forked from PortProxyGUI, and had too many specific ideas I needed for my own purposes that I figured there would be no way that the original author would want to merge each and every one of them, so I renamed it to "PortPorxyGooey" (*shrug*) to avoid any potential confusion now and in the future.
  It currently contains all fixes and requests in the originals' issues queue, plus way too many other things to list right here.
  - You can always view (most of) the changes in the Release Tags, [i.e. v2.0.0](https://github.com/STaRDoGG/PortProxyGUI/releases/tag/v2.0.0)

<br/>

## Information

The configuration file will be generated at:

```
C:\ProgramData\ScottElblein\PortProxyGooey\config.db
```

For the record, as this isn't released under some company name (or fake company name) or some super-rad-l33t nickname, I just decided from now on to use my own name as the root folder for any of my software releases, so don't let that spook ya, if for any reason it should.
