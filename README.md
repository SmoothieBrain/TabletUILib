# TabletUILib
UI Library for A Township Tale
Hellooo, I saw people wanted to use my mod BetterCam for its UI code (thank you VMan for the original idea) so I'm making it easier than that!
Instead of getting AI to look at the UI code you can make it easy on yourself and use this library.

# Usage
Reference TabletUILib.dll to your project.
```csharp
   [assembly: MelonAdditionalDependencies("TabletUILib")]
   ```
add a dependency for the lib,

Here is a quick example on how to make bubbles.
```csharp
TabletUI.AddBubble("better cam", "CAM")   // makes the bubble on the wheel
    .App("settings", "SET", Home)         //button 1 opens the tablet ui
    .Toggle("effects", "FX", on);         //button 2 on/off switch
```
The .App and .Toggle add sub bubbles.
```csharp This will add a bubble.
TabletUI.AddBubble()
```
