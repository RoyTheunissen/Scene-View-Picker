[![Roy Theunissen](Documentation~/GithubHeader.jpg)](http://roytheunissen.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-brightgreen.svg)](LICENSE.md)
![GitHub Follow](https://img.shields.io/github/followers/RoyTheunissen?label=RoyTheunissen&style=social)
<a href="https://roytheunissen.com" target="blank"><picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://github.com/RoyTheunissen/RoyTheunissen/raw/master/globe_dark.png">
    <source media="(prefers-color-scheme: light)" srcset="https://github.com/RoyTheunissen/RoyTheunissen/raw/master/globe_light.png">
    <img alt="globe" src="globe_dark.png" width="20" height="20" />
</picture></a>
<a href="https://bsky.app/profile/roytheunissen.com" target="blank"><picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://github.com/RoyTheunissen/RoyTheunissen/raw/master/bluesky_dark.png">
    <source media="(prefers-color-scheme: light)" srcset="https://github.com/RoyTheunissen/RoyTheunissen/raw/master/bluesky_light.png">
    <img alt="bluesky" src="bluesky_dark.png" width="20" height="20" />
</picture></a>
<a href="https://www.youtube.com/c/r_m_theunissen" target="blank"><picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://github.com/RoyTheunissen/RoyTheunissen/raw/master/youtube_dark.png">
    <source media="(prefers-color-scheme: light)" srcset="https://github.com/RoyTheunissen/RoyTheunissen/raw/master/youtube_light.png">
    <img alt="youtube" src="youtube_dark.png" width="20" height="20" />
</picture></a> 
<a href="https://www.tiktok.com/@roy_theunissen" target="blank"><picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://github.com/RoyTheunissen/RoyTheunissen/raw/master/tiktok_dark.png">
    <source media="(prefers-color-scheme: light)" srcset="https://github.com/RoyTheunissen/RoyTheunissen/raw/master/tiktok_light.png">
    <img alt="tiktok" src="tiktok_dark.png" width="20" height="20" />
</picture></a>

_Unity extension to allow you to assign Object fields by picking it from the scene view_

## About the Project

Sometimes scene hierarchies get complex and you get in a frustrating situation where you need to assign a field and you know exactly where it is in the scene, but it's hard to point out in the hierarchy. I figured: why not make something to allow you to assign fields by pointing where the object is in the scene? That's exactly what this is.

[Video](https://youtu.be/SVsugZvvoHA)

![Example](Documentation~/Example.gif)

## Getting Started

- Add the package to your Unity project (tips on how to install it are in the Installation section)
- Open a script with fields derived from Object
- Click the new button next to the hierarchy picker
  - Left Click to pick the desired object from the scene
  - Middle Click to pick from nearby objects when they are cluttered together
  - Right Click to cancel

## Extras
- If you want a callback whenever a field is assigned via picking, add the `[PickCallback("OnPicked")]` attribute
  - You can specify the name of a method to be called
  - This method can be parameterless or have two parameters of the same type of the field
  - In the latter case, the previous and current value are provided

## Installation

### Package Manager

Go to `Edit > Project Settings > Package Manager`. Under 'Scoped Registries' make sure there is an OpenUPM entry.

If you don't have one: click the `+` button and enter the following values:

- Name: `OpenUPM` <br />
- URL: `https://package.openupm.com` <br />

Then under 'Scope(s)' press the `+` button and add `com.roytheunissen`.

It should look something like this: <br />
![image](https://user-images.githubusercontent.com/3997055/185363839-37b3bb3d-f70c-4dbd-b30d-cc8a93b592bb.png)

<br />
All of my packages will now be available to you in the Package Manager in the 'My Registries' section and can be installed from there.
<br />


### Git Submodule

You can check out this repository as a submodule into your project's Assets folder. This is recommended if you intend to contribute to the repository yourself

### OpenUPM
The package is available on the [openupm registry](https://openupm.com). It's recommended to install it via [openupm-cli](https://github.com/openupm/openupm-cli).

```
openupm add com.roytheunissen.sceneviewpicker
```

### Manifest
You can also install via git URL by adding this entry in your **manifest.json** (make sure to end with a comma if you're adding this at the top)
```
"com.roytheunissen.sceneviewpicker": "https://github.com/RoyTheunissen/Scene-View-Picker.git"
```

### Unity Package Manager
From Window->Package Manager, click on the + sign and Add from git: 
```
https://github.com/RoyTheunissen/Scene-View-Picker.git
```


## Contact
[Roy Theunissen](https://roytheunissen.com)

[roy.theunissen@live.nl](mailto:roy.theunissen@live.nl)
