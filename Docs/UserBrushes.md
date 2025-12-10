# User Brushes v1 in Open Brush (Beta)
**Note: This feature is still in beta testing!**

User Brushes in Open Brush allow end users to create new variants on existing brushes, use them in
their sketches and, optionally have them embedded in their sketches so that other Open Brush users 
will be able to view them without having to download the new brushes manually.

## What User Brushes v1 can do:
* Change the brush parameters - for instance size, repsonsivity to pressure, etc.
* Replace textures used on brushes.
* Change the material parameters on a brush.
* Switch the shader used on one brush to the one used on another.

## What User Brushes v1 can't do right now, but should be able to once out of beta:
* Save User brushes from a sketch to your library
* Override the sounds used for a brush.

## What User Brushes v1 can't do:
* Add new shaders, or change the way a shader works.
* Add new behaviours to strokes.
* Change the way geometry from a brush is generated.

# Prerequisites
In order to run the create brush script, you will [need Python installed](https://www.python.org/downloads/).

This has only been tested on Windows.

# IMPORTANT! - A note about compatibility
Sketches that you make with this beta version of Open Brush will not open correctly on Tilt Brush
or Open Brush (until this comes out of beta). The user brushes certainly won't display properly,
and it is possible the apps might crash or not display the rest of the sketch.

Once User Brushes comes out of beta I hope all your work will work done during the beta will work
file, but I can't guarantee it! I may need to make breaking changes, depending on what we
discover when testing it. **DO NOT USE THIS BETA ON WORK YOU CAN'T AFFORD TO LOSE!**

# Getting Started
In order to create a new brush you need to open a command shell in the Support/bin folder in the
Open Brush install folder. Where exactly this is will depend a little on where you got Open Brush
from.

The script for creating user brushes is called `userbrush.py`. You can get it to list all the 
brushes available to base your brush on:


```
> python userbrush.py list
Bubbles
CelVinyl
ChromaticWave
CoarseBristles
Comet
DiamondHull
Disco
Dots
DoubleTaperedFlat
DoubleTaperedMarker
DuctTape
Electricity
Embers
Fire
Flat
Highlighter
Hypercolor
HyperGrid
Icing
Ink
Light
LightWire
Lofted
Marker
MatteHull
NeonPulse
OilPaint
Paper
Petal
Rainbow
ShinyHull
Smoke
Snow
SoftHighlighter
Spikes
Splatter
Stars
Streamers
TaperedFlat
TaperedMarker
ThickPaint
Toon
UnlitHull
VelvetInk
Waveform
WetPaint
WigglyGraphite
Wire
```
You should choose one of these brushes to create your user brush from. Let's assume you want to
make a *Snake* brush based off the *Icing* brush:

```
> python userbrush.py create Snake Icing
Created brush config at C:\Users\Tim\Documents\Open Brush\Brushes\Snake_9f78ecb3-ba5a-4f91-8484-b03944998f8e\Brush.cfg.
```
This creates a new folder called *Snake_9f78ecb3-ba5a-4f91-8484-b03944998f8e* in the
*Open Brush\Brushes* folder, and puts a file called *Brush.cfg* in there. I know the folder name is
cumbersome, but it prevents problems if two people both make a brush called *Snake*.

## The Brush.cfg file
Brush.cfg is a JSON file that contains all the settings for your brush. It starts off containing
all the values from whatever brush you based your new brush off, in this case *Icing*. At the end
of the file there is a duplicate of all these values under the section `Original_Base_Brush_Values`,
which isn't used by Open Brush, but is useful for reference in case you have changed something and
want to change it back to how it was.

Here is an outline of the *Brush.cfg* for our Snake brush, with some sections trimmed for brevity:
```json
{
    "VariantOf": "2f212815-f4d3-c1a4-681a-feeaf9c6dc37",
    "GUID": "9f78ecb3-ba5a-4f91-8484-b03944998f8e",
    "Author": "",
    "Name": "Snake",
    "Description": "Snake",
    "CopyRestrictions": "EmbedAndShare",
    "ButtonIcon": "",
    "Audio": { ... },
    "Material": { ... },
    "Size": { ... },
    "Color": { ... },
    "Particle": { ... },
    "QuadBatch": { ... },
    "Tube": { ... },
    "Misc": { ... },
    "Export": { ... },
    "Simplification": { ... },
    "BrushDescriptionVersion": 1,
    "Comments": "",
    "Original_Base_Brush_Values": { ... },
}
```

* **VariantOf** is the GUID of the brush you based your brush on. In this case, *Icing*.
* **GUID** is the GUID of your new brush. **This must always be unique!** Do not under any
           circumstances copy a Brush.cfg from another brush without creating a new GUID
           for it, as it is the system Open Brush uses to tell brushes apart.
* **Author** is not currently used, but you can put your name in there.
* **Name** the name of the brush.
* **Description** often the same as *Name* - displayed on the brush panel.
* **CopyRestrictions** should either be `EmbedAndShare`, `EmbedAndDoNotShare`, or `DoNotEmbed`.
           These control whether a brush will be included in a sketch that uses it, and whether
           the brush will be available to use or save if it is embedded in a sketch.
* **ButtonIcon** a filename of a `jpg` or `png` file in the same folder that will be used as the
           icon in the brush panel. Normally 128x128.

The remaining sections are settings that alter the appearance or behaviour of the brush.

At this point if we run Open Brush and scroll to the end of the brushes, we should be able to see
a new brush at the end called *Snake*, although it will still have the *Icing* icon.

## Changing the icon
To add an icon, we just have to create a new texture for it and save it in the brush folder.
Let's call it icon.png, and make it 128x128 in size, which is the same resolution as the built-in
ones.

After that, we just need to change the `ButtonIcon` field to point to the image:
```json
{
    "VariantOf": "2f212815-f4d3-c1a4-681a-feeaf9c6dc37",
    "GUID": "9f78ecb3-ba5a-4f91-8484-b03944998f8e",
    "Author": "",
    "Name": "Snake",
    "Description": "Snake",
    "ExtraDescription": "",
    "CopyRestrictions": "EmbedAndShare",
    "ButtonIcon": "icon.png",
     ...
```

... and now the brush will have its own icon in the panel, although stroked drawn with it will
still look like *Icing*.

## Changing the brush
Let's keep working on the Snake Brush. We want to be able to draw thicker strokes with the brush,
so we'll change some values in the `Size` section:
```json
"Size": {
    "BrushSizeRange": [
        0.025,
        0.5
    ],
    "PressureSizeRange": [
        0.1,
        1.0
    ],
    "SizeVariance": 0.0,
    "PreviewPressureSizeMin": 0.001
},
```
`BrushSizeRange` is the range of sizes that the brush can be changed to by moving the thumbstick -
we can expand the range by changing this value, so we'll alter it to go from 0.01 - 2.0.
```json
"BrushSizeRange": [
   0.025,
   0.5
],
```

## Altering the material
Looking at the `Material` section of *Brush.cfg*:
```json
"Material": {
    "Shader": "Brush/StandardSingleSided",
    "FloatProperties": {
        "_Shininess": 0.15,
        "_Cutoff": 0.5
    },
    "ColorProperties": {
        "_Color": [
            1.0,
            1.0,
            1.0,
            1.0
        ],
        "_SpecColor": [
            0.0,
            0.0,
            0.0,
            0.0
        ]
    },
    "VectorProperties": {},
    "TextureProperties": {
        "_MainTex": "",
        "_BumpMap": ""
    },
    "TextureAtlasV": 1,
    "TileRate": 0.25,
    "UseBloomSwatchOnColorPicker": false
},
```
You can see that there are various things we can override. For our *Snake* brush, we will make
the brush look more shiny by changing the `_Shininess` value to 0.6, and the `_SpecColor` value
to `[0.2, 0.2, 0.2, 0.0]`.

Next we get color and normal maps for snakeskin. The color map is just greyscale so that it takes
on the color that is assigned it by the user. They are called `color.jpg` and `normal.jpg`. We
fill in the `TextureProperties` section of `Material`:
```json
"TextureProperties": {
    "_MainTex": "color.jpg",
    "_BumpMap": "normal.jpg"
},
```
Once this is done we have a working *Snake* brush!

## .brush files
If you want to distribute a brush by itself (not in a sketch), you can zip the entire folder up,
and rename the .zip to a .brush file. If it is put in the *Brushes* folder it will be loaded just
like any other brush.

## Copy Restrictions
When an artist creates a brush, they may or may not want to grant others the ability to use their
brush. Perhaps they feel the brush is part of what makes their art unique, or perhaps they like
to make brushes explicitly for others to use.

Therefore, when you create a brush, you can choose the extent that you want it shared. There are
three levels of sharing:

### DoNotEmbed
In this setting, the brush is never embedded in to a sketch at all. It is available for those who
have the brush in their *Brushes* folder, but that's it. However, this means that someone looking
at the sketch who does that have that brush will just see all of its strokes rendered as the base
brush the user brush was based upon.

### EmbedAndDoNotShare
With this setting, the brush will get embedded in to any sketch that uses it, but it will only be
available to draw with for those who also have it in their *Brushes* folder. People downloading
and viewing the sketch will see the strokes with it rendered in all their full glory, but will not
be able to recreate them themselves.

### EmbedAndShare
With this setting, the brush will be embedded in any sketches that use it, and anyone who loads
the sketch will have the brush appear in their brush panel for them to use. They will also be able
to save those brushes from their panel into their Brushes folder. (This feature not implemented
yet.)

# I need some help!
You can get help on the Icosa discord on the #testing-and-bugs channel.

# I found a bug!
Please [add an issue on the TimAidley/open-brush](https://github.com/TimAidley/open-brush/issues)
fork. Once the first round of testing is done, this will probably be moved in to the main Open
Brush repository and I will copy any open issues across.


